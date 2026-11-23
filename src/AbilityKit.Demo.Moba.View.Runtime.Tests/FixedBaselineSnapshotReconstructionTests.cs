using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class FixedBaselineSnapshotReconstructionTests
{
    [Fact]
    public void RebuildsFromImmutableBaselineAndDropsMissingTransientActor()
    {
        var generation = Guid.NewGuid();
        var state = new MobaAuthoritativeSnapshotState();
        Commit(state, Decode(generation, 1, 0, 10, null, Actor(1, 0), Actor(2, 20), Actor(3, 30)));
        var moved = Commit(state, Decode(generation, 3, 1, 200, new List<int> { 3 }, Actor(1, 2), Actor(4, 40)));
        Assert.Equal(new[] { (1, 2f), (2, 20f), (4, 40f) }, Poses(moved));
        var returned = Commit(state, Decode(generation, 4, 1, 201, null));
        var fullReference = Decode(generation, 5, 0, 201, null, Actor(1, 0), Actor(2, 20), Actor(3, 30));
        Assert.Equal(Poses(fullReference), Poses(returned));
        Assert.Equal(3, state.ActorCount);
        Assert.Equal(42, returned.EventWatermark);
        Assert.Equal(new byte[] { 7, 8 }, returned.Payload);
    }

    [Fact]
    public void MissingMalformedAndRetiredBaselinesDoNotCommit()
    {
        var oldGeneration = Guid.NewGuid();
        var newGeneration = Guid.NewGuid();
        var state = new MobaAuthoritativeSnapshotState();
        Commit(state, Decode(oldGeneration, 1, 0, 10, null, Actor(1, 0)));
        var absent = Decode(oldGeneration, 8, 7, 11, null, Actor(1, 1));
        Assert.Equal(MobaSnapshotAdmissionStatus.BaselineRequired, state.PrepareFixed(in absent, out _));
        var duplicateActor = Decode(oldGeneration, 2, 1, 11, null, Actor(1, 1), Actor(1, 2));
        Assert.Equal(MobaSnapshotAdmissionStatus.BaselineRequired, state.PrepareFixed(in duplicateActor, out _));
        Assert.Equal(1, state.ActorCount);
        Commit(state, Decode(newGeneration, 1, 0, 12, null, Actor(1, 9)));
        Assert.Equal(MobaSnapshotAdmissionStatus.StaleOrDuplicate, state.PrepareFixed(in absent, out _));
        var retiredFull = Decode(oldGeneration, 9, 0, 13, null, Actor(1, 20));
        Assert.Equal(MobaSnapshotAdmissionStatus.StaleOrDuplicate, state.PrepareFixed(in retiredFull, out _));
        var newDelta = Decode(newGeneration, 2, 1, 13, null);
        Assert.Equal(9f, Assert.Single(Commit(state, newDelta).Actors).X);
        state.ResetForRecovery();
        Assert.Equal(MobaSnapshotAdmissionStatus.StaleOrDuplicate, state.PrepareFixed(in retiredFull, out _));
        var deltaWithoutFull = Decode(newGeneration, 3, 1, 14, null);
        Assert.Equal(MobaSnapshotAdmissionStatus.BaselineRequired, state.PrepareFixed(in deltaWithoutFull, out _));
        Commit(state, Decode(newGeneration, 4, 0, 14, null, Actor(1, 10)));
    }

    [Fact]
    public void ExactOrderAllowsSameFrameAndSkippedTicksWithoutWeakeningLegacyAdmission()
    {
        var admission = new MobaSnapshotAdmission(maxDeltaFrameGap: 2);
        admission.Reset(9);
        Assert.True(admission.Admit(9, 10, true, 1).Accepted);
        Assert.True(admission.Admit(9, 10, true, 1, explicitBaselineAndOrderValidated: true).Accepted);
        Assert.True(admission.Admit(9, 20, false, 1, explicitBaselineAndOrderValidated: true).Accepted);
        Assert.Equal(MobaSnapshotAdmissionStatus.StaleOrDuplicate,
            admission.Admit(9, 19, true, 1, explicitBaselineAndOrderValidated: true).Status);
        Assert.Equal(MobaSnapshotAdmissionStatus.FrameGapTooLarge, admission.Admit(9, 30, false, 1).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecoveryOrder_AllowsEarlierFullAfterUncommittedCandidate(bool hasCommittedBaseline)
    {
        var generation = Guid.NewGuid();
        var reconstruction = new MobaAuthoritativeSnapshotState();
        var admission = new MobaSnapshotAdmission();
        admission.Reset(9);
        if (hasCommittedBaseline)
        {
            var baseline = Decode(generation, 1, 0, 10, null, Actor(1, 10));
            Assert.True(admission.CheckFixed(in baseline).Accepted);
            Assert.True(admission.CommitFixed(in baseline).Accepted);
            Commit(reconstruction, baseline);
        }

        var uncommitted = Decode(generation, 3, hasCommittedBaseline ? 1 : 0, 12, null, Actor(1, 12));
        Assert.Equal(MobaSnapshotAdmissionStatus.Accepted, reconstruction.PrepareFixed(in uncommitted, out _));
        Assert.True(admission.CheckFixed(in uncommitted).Accepted);
        admission.RequireFullBaseline();
        reconstruction.ResetForRecovery();

        Assert.False(admission.HasBaseline);
        Assert.Equal(hasCommittedBaseline ? 10 : 0, admission.LastAcceptedFrame);
        var earlier = Decode(generation, 2, 0, 11, null, Actor(1, 11));
        Assert.Equal(MobaSnapshotAdmissionStatus.Accepted, reconstruction.PrepareFixed(in earlier, out _));
        Assert.True(admission.CheckFixed(in earlier).Accepted);
        Assert.True(admission.CommitFixed(in earlier).Accepted);
        Assert.Equal(11f, Assert.Single(Commit(reconstruction, earlier).Actors).X);
        Assert.Equal(11, admission.LastAcceptedFrame);
    }

    private static GatewayStateSyncSnapshot Commit(MobaAuthoritativeSnapshotState state, GatewayStateSyncSnapshot snapshot)
    {
        Assert.Equal(MobaSnapshotAdmissionStatus.Accepted, state.PrepareFixed(in snapshot, out var materialized));
        state.CommitFixed(in snapshot, in materialized);
        return materialized;
    }

    private static (int Actor, float X)[] Poses(GatewayStateSyncSnapshot snapshot) =>
        snapshot.Actors.Select(actor => (actor.ActorId, actor.X)).ToArray();

    private static WireStateSyncActorSnapshot Actor(int id, float x) => new() { ActorId = id, X = x };

    private static GatewayStateSyncSnapshot Decode(Guid generation, long id, long baseline, int frame,
        List<int>? removed, params WireStateSyncActorSnapshot[] actors)
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 9,
            Frame = frame,
            IsFullSnapshot = baseline == 0,
            SchemaVersion = FixedBaselineStateSyncProtocol.SchemaVersion,
            Actors = actors.ToList(),
            RemovedActorIds = removed,
            EventEpoch = "smoke-events",
            EventWatermark = 42,
            Payload = FixedBaselineStateSyncProtocol.Encode(generation, id, baseline, 1, new byte[] { 7, 8 })
        };
        return GatewayStateSyncSnapshotDecoder.Decode(WireRoomGatewayBinary.Serialize(in wire));
    }
}
