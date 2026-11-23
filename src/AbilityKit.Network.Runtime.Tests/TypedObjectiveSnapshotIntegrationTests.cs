using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class TypedObjectiveSnapshotIntegrationTests
{
    [Fact]
    public void SkippedDeltaRebuildsFromTheCommittedFullViewInsteadOfTheLastDelta()
    {
        var client = new ObjectiveClient();
        var baseline = Full(10, One(), Two());
        Assert.Equal(ClientSnapshotSyncStatus.AppliedFullBaseline, client.Receive(baseline).Status);
        Assert.Equal(ClientSnapshotSyncStatus.AppliedDelta,
            client.Receive(Delta(11, baseline, [new Objective(1, Owner: 2, Progress: 75)], [2])).Status);
        Assert.Equal(new[] { new Objective(1, Owner: 2, Progress: 75) }, client.View);

        var result = client.Receive(Delta(13, baseline, [new Objective(3, Owner: 1, Progress: 20)], []));

        Assert.Equal(ClientSnapshotSyncStatus.AppliedDelta, result.Status);
        Assert.Equal(new[] { One(), Two(), new Objective(3, Owner: 1, Progress: 20) }, client.View);
        Assert.Equal(13, client.State.LastAppliedFrame);
        Assert.Equal(13L, client.State.LastAppliedSequence);
        Assert.Equal(10, client.State.LastBaselineFrame);
        Assert.Equal(10, client.LocalBaselineFrame);
        Assert.Empty(client.RecoveryRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedFullDoesNotCommitNewBaselineAndAnEarlierCandidateCanRecover(bool hasBaseline)
    {
        var client = new ObjectiveClient();
        if (hasBaseline) client.Receive(Full(10, One()));
        var before = client.State;
        var existingView = client.View;
        var wanted = Full(20, Two());
        client.RejectNextApply = true;

        Assert.Throws<InvalidOperationException>(() => client.Receive(wanted));
        Assert.Same(existingView, client.View);
        Assert.Equal(before.HasAppliedSnapshot, client.State.HasAppliedSnapshot);
        Assert.Equal(before.LastAppliedSequence, client.State.LastAppliedSequence);
        Assert.Equal(before.LastAppliedFrame, client.State.LastAppliedFrame);
        Assert.Equal(before.LastBaselineFrame, client.State.LastBaselineFrame);
        Assert.Equal(before.LastBaselineHash, client.State.LastBaselineHash);
        Assert.Equal(hasBaseline ? 10 : (int?)null, client.LocalBaselineFrame);
        Assert.DoesNotContain(20, client.PublishedFrames);

        var replacement = Full(16, Two());
        Assert.True(client.Receive(replacement).Applied);
        Assert.Equal(new[] { Two() }, client.View);
        Assert.Equal(16, client.LocalBaselineFrame);
        Assert.Equal(16, client.State.LastBaselineFrame);
        Assert.Equal(16, client.PublishedFrames.Last());
    }

    [Fact]
    public void FailedDeltaLeavesBothViewsAndCursorUntouchedAndCanBeRetried()
    {
        var client = new ObjectiveClient();
        var baseline = Full(10, One(), Two());
        client.Receive(baseline);
        var delta = Delta(12, baseline, [new Objective(1, Owner: 2, Progress: 50)], [2]);
        client.RejectNextApply = true;

        Assert.Throws<InvalidOperationException>(() => client.Receive(delta));
        Assert.Equal(new[] { One(), Two() }, client.View);
        Assert.Equal(10, client.State.LastAppliedFrame);
        Assert.Equal(10L, client.State.LastAppliedSequence);
        Assert.Equal(10, client.LocalBaselineFrame);
        Assert.Equal(new[] { 10 }, client.PublishedFrames);

        Assert.Equal(ClientSnapshotSyncStatus.AppliedDelta, client.Receive(delta).Status);
        Assert.Equal(new[] { new Objective(1, Owner: 2, Progress: 50) }, client.View);
        Assert.Equal(new[] { 10, 12 }, client.PublishedFrames);
        Assert.Equal(10, client.LocalBaselineFrame);
        Assert.Empty(client.RecoveryRequests);
    }

    [Fact]
    public void MismatchedBaselineRequestsFullButMatchingDeltaStillApplies()
    {
        var client = new ObjectiveClient();
        var baseline = Full(10, One());
        client.Receive(baseline);

        var result = client.Receive(Delta(21, Full(20, Two()), [], []));
        Assert.Equal(ClientSnapshotSyncStatus.NeedsFullBaseline, result.Status);
        Assert.Equal(SnapshotStreamRecoveryReason.BaselineMismatch, result.Validation.RecoveryReason);
        Assert.Equal(SnapshotRecoveryRequestKind.FullSnapshot, Assert.Single(client.RecoveryRequests));
        Assert.Equal(1, client.MaterializeCalls);
        Assert.Equal(10, client.State.LastAppliedFrame);
        Assert.Equal(10, client.LocalBaselineFrame);
        Assert.True(client.State.NeedsFullBaselineRecovery);

        Assert.Equal(ClientSnapshotSyncStatus.AppliedDelta, client.Receive(Delta(22, baseline, [Two()], [])).Status);
        Assert.Equal(new[] { One(), Two() }, client.View);
        Assert.Equal(22, client.State.LastAppliedFrame);
        Assert.Equal(10, client.State.LastBaselineFrame);
        Assert.True(client.State.NeedsFullBaselineRecovery);
        Assert.Single(client.RecoveryRequests);

        var recoveryFull = Full(23, Two());
        client.RejectNextApply = true;
        Assert.Throws<InvalidOperationException>(() => client.Receive(recoveryFull));
        Assert.True(client.State.NeedsFullBaselineRecovery);
        Assert.Equal(22, client.State.LastAppliedFrame);
        Assert.Equal(10, client.LocalBaselineFrame);
        Assert.Equal(new[] { 10, 22 }, client.PublishedFrames);

        Assert.True(client.Receive(recoveryFull).Applied);
        Assert.False(client.State.NeedsFullBaselineRecovery);
        Assert.Equal(new[] { Two() }, client.View);
        Assert.Equal(23, client.LocalBaselineFrame);
    }

    [Fact]
    public void InvalidObjectiveRemovalDoesNotPartiallyApplyTheTypedProjection()
    {
        var client = new ObjectiveClient();
        var baseline = Full(10, One());
        client.Receive(baseline);
        var invalid = Delta(11, baseline, [Two()], [99]);

        Assert.Throws<InvalidOperationException>(() => client.Receive(invalid));
        Assert.Equal(new[] { One() }, client.View);
        Assert.Equal(10, client.State.LastAppliedFrame);
        Assert.Equal(10, client.LocalBaselineFrame);
        Assert.Equal(new[] { 10 }, client.PublishedFrames);
        Assert.True(client.Receive(Delta(11, baseline, [Two()], [])).Applied);
        Assert.Equal(new[] { One(), Two() }, client.View);
    }

    private static Objective One() => new(1, Owner: 1, Progress: 25);
    private static Objective Two() => new(2, Owner: 0, Progress: 0);

    private static ObjectiveSnapshot Full(int frame, params Objective[] values) =>
        new(frame, IsFull: true, BaselineFrame: 0, BaselineStamp: 0, values, []);

    private static ObjectiveSnapshot Delta(int frame, ObjectiveSnapshot baseline, Objective[] upserts, int[] removals) =>
        new(frame, IsFull: false, baseline.Frame, baseline.Stamp, upserts, removals);

    private sealed record Objective(int Id, int Owner, int Progress);

    private sealed record ObjectiveSnapshot(int Frame, bool IsFull, int BaselineFrame, uint BaselineStamp,
        Objective[] Upserts, int[] Removals)
    {
        public uint Stamp => (uint)Frame * 10;
    }

    private sealed class ObjectiveClient : ClientSnapshotSyncObserver<ObjectiveSnapshot>
    {
        private readonly ClientSnapshotSyncPipeline<ObjectiveSnapshot> _pipeline;
        private Dictionary<int, Objective> _baseline = new();

        public ObjectiveClient()
        {
            var options = new ClientSnapshotSyncOptions<ObjectiveSnapshot>(1, 1, CreateEnvelope, ApplyObjectiveView)
            {
                Observer = this,
                RecoveryHandler = (in ObjectiveSnapshot _, in SnapshotRecoveryRequest request) => RecoveryRequests.Add(request.Kind)
            };
            _pipeline = new ClientSnapshotSyncPipeline<ObjectiveSnapshot>(options);
        }

        public ClientSnapshotSyncState State => _pipeline.State;
        public Objective[] View { get; private set; } = [];
        public int? LocalBaselineFrame { get; private set; }
        public int MaterializeCalls { get; private set; }
        public bool RejectNextApply { get; set; }
        public List<int> PublishedFrames { get; } = new();
        public List<SnapshotRecoveryRequestKind> RecoveryRequests { get; } = new();

        public ClientSnapshotSyncResult Receive(ObjectiveSnapshot snapshot) => _pipeline.Apply(in snapshot);

        public override void OnResult(in ObjectiveSnapshot snapshot, in ClientSnapshotSyncResult result,
            in ClientSnapshotSyncState state)
        {
            if (result.Applied) PublishedFrames.Add(state.LastAppliedFrame);
        }

        private static SnapshotStreamEnvelope CreateEnvelope(in ObjectiveSnapshot snapshot) =>
            new(17, 1, snapshot.Frame, snapshot.Frame,
                snapshot.IsFull ? SnapshotStreamSnapshotKind.FullBaseline : SnapshotStreamSnapshotKind.Delta,
                snapshot.IsFull ? snapshot.Frame : snapshot.BaselineFrame,
                snapshot.IsFull ? snapshot.Stamp : snapshot.BaselineStamp,
                snapshot.Stamp);

        private void ApplyObjectiveView(in ObjectiveSnapshot snapshot)
        {
            MaterializeCalls++;
            var candidate = snapshot.IsFull ? new Dictionary<int, Objective>() : new Dictionary<int, Objective>(_baseline);
            foreach (var objective in snapshot.Upserts) candidate[objective.Id] = objective;
            foreach (var removed in snapshot.Removals)
                if (!candidate.Remove(removed)) throw new InvalidOperationException("Objective to remove is absent.");

            var view = candidate.Values.OrderBy(objective => objective.Id).ToArray();
            if (RejectNextApply)
            {
                RejectNextApply = false;
                throw new InvalidOperationException("The fixture rejected the candidate view before writing it.");
            }

            View = view;
            if (snapshot.IsFull)
            {
                _baseline = candidate;
                LocalBaselineFrame = snapshot.Frame;
            }
        }
    }
}
