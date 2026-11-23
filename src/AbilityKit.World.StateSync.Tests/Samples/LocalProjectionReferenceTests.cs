using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed partial class LocalProjectionReferenceTests
{
    [Fact]
    public void MovementCommitIsUsableWithoutPrediction_AndOnlyMatchingProofEnablesReplay()
    {
        var host = new LocalProjectionHost<MovementProjection, int>(
            (baseline, offset) => new MovementProjection(baseline.Position + offset),
            projection => new MovementProjection(projection.Position + 10));
        var scope = new ReconciliationScope(1, 17, Guid.NewGuid(), 5, 1, 7, 1);
        var steppedTicks = new List<int>();
        var reconciler = new LocalMovementReconciler<int, int>(scope, Step);
        Assert.True(reconciler.TryRecordInput(scope, sequence: 1, tick: 11, input: 3));
        Assert.True(reconciler.SealTick(scope, 11));
        Assert.True(reconciler.SealTick(scope, 12));

        var full = Snapshot<MovementProjection, int>.Full(1, 10, new MovementProjection(100));
        var initial = AssertCommitted(host.Receive(full));
        Assert.Equal(new MovementProjection(110), initial.State);
        Assert.Equal(0L, reconciler.AcceptedRevision);

        Assert.Equal(ReconciliationStatus.InvalidTerminalProof,
            reconciler.Reconcile(MovementAt(initial, scope), null, 12).Status);
        Assert.True(reconciler.TryGetAuthoritative(out var authority));
        Assert.Equal(110, authority);
        Assert.False(reconciler.TryGetPrediction(out _));
        Assert.Equal(0L, reconciler.TerminalSequence);
        Assert.Equal(1L, reconciler.GetPendingSequence(0));
        Assert.Empty(steppedTicks);

        var updated = AssertCommitted(host.Receive(Snapshot<MovementProjection, int>.Delta(1, 11, baselineTick: 10, change: 3)));
        Assert.Equal(new MovementProjection(113), updated.State);
        var proof = new InputTerminalProof(scope, revision: 11, tick: 11, throughSequence: 1);
        Assert.Equal(ReconciliationStatus.Predicted, reconciler.Reconcile(MovementAt(updated, scope), proof, 12).Status);
        Assert.Equal(0, reconciler.PendingCount);
        Assert.Equal(1L, reconciler.TerminalSequence);
        Assert.Equal(new[] { 12 }, steppedTicks);
        Assert.True(reconciler.TryGetPrediction(out var predicted));
        Assert.Equal(113, predicted);

        bool Step(int tick, ReadOnlySpan<int> inputs, ref int state)
        {
            steppedTicks.Add(tick);
            foreach (var input in inputs)
                state += input;
            return true;
        }
    }

    [Fact]
    public void ObjectiveUsesTheSameCommitPath_AndLaterDeltaStartsFromTheFullBaseline()
    {
        var host = ObjectiveHost();
        var initial = new ObjectiveProjection(Owner: 0, Progress: 20);
        Assert.Equal(initial, AssertCommitted(host.Receive(Snapshot<ObjectiveProjection, ObjectiveChange>.Full(1, 10, initial))).State);
        var changed = AssertCommitted(host.Receive(Snapshot<ObjectiveProjection, ObjectiveChange>.Delta(1, 11, 10, new(1, 75))));
        Assert.Equal(new ObjectiveProjection(1, 75), changed.State);

        var afterSkip = AssertCommitted(host.Receive(Snapshot<ObjectiveProjection, ObjectiveChange>.Delta(1, 13, 10, new(null, 90))));

        Assert.Equal(new ObjectiveProjection(0, 90), afterSkip.State);
        Assert.False(afterSkip.IsFull);
        Assert.Equal(13L, host.StreamState.LastAppliedSequence);
        Assert.Equal(10, host.StreamState.LastBaselineFrame);
    }

    [Fact]
    public void RejectedImport_LeavesPublishedStateAndBaselineUnchanged()
    {
        var host = ObjectiveHost();
        var before = AssertCommitted(host.Receive(Snapshot<ObjectiveProjection, ObjectiveChange>.Full(1, 10, new(0, 20))));
        var rejected = Snapshot<ObjectiveProjection, ObjectiveChange>.Full(1, 12, new(1, 101));

        Assert.Throws<InvalidOperationException>(() => host.Receive(rejected));

        Assert.Equal(before, host.LatestCommitted);
        Assert.Equal(10L, host.StreamState.LastAppliedSequence);
        Assert.Equal(10, host.StreamState.LastBaselineFrame);
        var recovered = AssertCommitted(host.Receive(Snapshot<ObjectiveProjection, ObjectiveChange>.Delta(1, 11, 10, new(2, 30))));
        Assert.Equal(new ObjectiveProjection(2, 30), recovered.State);
    }

    [Fact]
    public void OldGeneration_DoesNotReachImportOrReplaceCurrentCommittedState()
    {
        var host = ObjectiveHost();
        AssertCommitted(host.Receive(Snapshot<ObjectiveProjection, ObjectiveChange>.Full(1, 10, new(0, 20))));
        host.SwitchGeneration(2);
        var current = AssertCommitted(host.Receive(Snapshot<ObjectiveProjection, ObjectiveChange>.Full(2, 1, new(2, 50))));
        var calls = host.ImportCalls;

        Assert.Null(host.Receive(Snapshot<ObjectiveProjection, ObjectiveChange>.Full(1, 30, new(1, 100))));

        Assert.Equal(calls, host.ImportCalls);
        Assert.Equal(current, host.LatestCommitted);
        Assert.Equal(1L, host.StreamState.LastAppliedSequence);
    }

    private static LocalProjectionHost<ObjectiveProjection, ObjectiveChange> ObjectiveHost() => new(
        (baseline, change) => new ObjectiveProjection(change.Owner ?? baseline.Owner, change.Progress ?? baseline.Progress),
        projection => projection.Progress is >= 0 and <= 100
            ? projection : throw new InvalidOperationException("据点进度不在本地夹具允许的范围内。"));

    private static CommittedProjection<TState> AssertCommitted<TState>(CommittedProjection<TState>? result)
        where TState : unmanaged
    {
        Assert.True(result.HasValue);
        return result.GetValueOrDefault();
    }

    private static CommittedMovement<int> MovementAt(CommittedProjection<MovementProjection> value, ReconciliationScope scope) =>
        new(scope, value.Revision, value.Tick, value.State.Position, value.IsFull);

    private readonly record struct MovementProjection(int Position);
    private readonly record struct ObjectiveProjection(int Owner, int Progress);
    private readonly record struct ObjectiveChange(int? Owner, int? Progress);
    private readonly record struct CommittedProjection<TState>(int Generation, long Revision, int Tick, bool IsFull, TState State)
        where TState : unmanaged;

    private readonly record struct Snapshot<TState, TChange>(int Generation, SnapshotStreamEnvelope Envelope, TState State, TChange Change)
        where TState : unmanaged
        where TChange : struct
    {
        internal static Snapshot<TState, TChange> Full(int generation, int tick, TState state) =>
            new(generation, MakeEnvelope(tick, tick, SnapshotStreamSnapshotKind.FullBaseline), state, default);

        internal static Snapshot<TState, TChange> Delta(int generation, int tick, int baselineTick, TChange change) =>
            new(generation, MakeEnvelope(tick, baselineTick, SnapshotStreamSnapshotKind.Delta), default, change);

        private static SnapshotStreamEnvelope MakeEnvelope(int tick, int baselineTick, SnapshotStreamSnapshotKind kind) =>
            new(17, 1, tick, tick, kind, baselineTick, Stamp(baselineTick), Stamp(tick));

        private static uint Stamp(int tick) => (uint)tick + 1;
    }

    private sealed class LocalProjectionHost<TState, TChange>
        where TState : unmanaged
        where TChange : struct
    {
        private readonly ClientSnapshotSyncPipeline<Snapshot<TState, TChange>> _pipeline;
        private readonly Func<TState, TChange, TState> _rebuild;
        private readonly Func<TState, TState> _import;
        private int _generation = 1;
        private TState _baseline;
        private (TState Reconstructed, TState Imported)? _staged;

        internal LocalProjectionHost(Func<TState, TChange, TState> rebuild, Func<TState, TState> import)
        {
            _rebuild = rebuild;
            _import = import;
            _pipeline = new ClientSnapshotSyncPipeline<Snapshot<TState, TChange>>(
                new ClientSnapshotSyncOptions<Snapshot<TState, TChange>>(1, 1,
                    (in Snapshot<TState, TChange> snapshot) => snapshot.Envelope, StageCandidate));
        }

        internal CommittedProjection<TState>? LatestCommitted { get; private set; }
        internal int ImportCalls { get; private set; }
        internal ClientSnapshotSyncState StreamState => _pipeline.State;

        internal CommittedProjection<TState>? Receive(Snapshot<TState, TChange> snapshot) => Receive(snapshot, out _);

        internal CommittedProjection<TState>? Receive(
            Snapshot<TState, TChange> snapshot, out ClientSnapshotSyncResult? attempt)
        {
            attempt = null;
            if (snapshot.Generation != _generation)
                return null;

            try
            {
                var result = _pipeline.Apply(in snapshot);
                attempt = result;
                if (!result.Applied)
                    return null;

                var staged = _staged ?? throw new InvalidOperationException("准入成功但没有导入结果。");
                var envelope = snapshot.Envelope;
                if (envelope.IsFullBaseline)
                    _baseline = staged.Reconstructed;

                return LatestCommitted = new CommittedProjection<TState>(snapshot.Generation, StreamState.LastAppliedSequence,
                    StreamState.LastAppliedFrame, envelope.IsFullBaseline, staged.Imported);
            }
            finally
            {
                _staged = null;
            }
        }

        internal void SwitchGeneration(int generation)
        {
            _generation = generation;
            _pipeline.Reset();
            _baseline = default;
            LatestCommitted = null;
        }

        private void StageCandidate(in Snapshot<TState, TChange> snapshot)
        {
            var candidate = snapshot.Envelope.IsFullBaseline ? snapshot.State : _rebuild(_baseline, snapshot.Change);
            ImportCalls++;
            _staged = (candidate, _import(candidate));
        }
    }
}
