using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Snapshot;

public sealed class FrameSnapshotListDispatcherTests
{
    [Fact]
    public void List_subscription_receives_original_list_without_array_conversion()
    {
        var dispatcher = new FrameSnapshotDispatcher();
        IFrameSnapshotListDispatcher listDispatcher = dispatcher;
        IReadOnlyList<ActorTransformData> source = new List<ActorTransformData>
        {
            new ActorTransformData(17, 1f, 2f, 3f, 0.5f, 1f),
            new ActorTransformData(23, -1f, 4f, 2f, 1.5f, 0.8f),
        };
        IReadOnlyList<ActorTransformData> received = null;

        dispatcher.SubscribeList<ActorTransformData>(
            MobaOpCodes.Snapshot.ActorTransform,
            (_, data) => received = data);

        listDispatcher.DispatchActorTransform(12, in source);

        Assert.Same(source, received);
        Assert.Equal(17, received[0].ActorId);
        Assert.Equal(23, received[1].ActorId);
    }

    [Fact]
    public void List_dispatch_keeps_array_subscriber_compatible()
    {
        var dispatcher = new FrameSnapshotDispatcher();
        IReadOnlyList<SkillStateData> source = new List<SkillStateData>
        {
            new SkillStateData(10, 1, 1001),
            new SkillStateData(20, 2, 2002),
        };
        SkillStateData[] received = null;
        dispatcher.Subscribe<SkillStateData[]>(
            MobaOpCodes.Snapshot.SkillState,
            (_, data) => received = data);

        dispatcher.DispatchSkillState(3, in source);

        Assert.NotNull(received);
        Assert.Equal(new[] { 10, 20 }, System.Array.ConvertAll(received, state => state.ActorId));
    }

    [Fact]
    public void Array_dispatch_reaches_list_subscription()
    {
        var dispatcher = new FrameSnapshotDispatcher();
        IReadOnlyList<ActorDespawnData> received = null;
        dispatcher.SubscribeList<ActorDespawnData>(
            MobaOpCodes.Snapshot.ActorDespawn,
            (_, data) => received = data);
        var source = new[] { new ActorDespawnData(42) };

        dispatcher.DispatchActorDespawn(5, in source);

        Assert.Same(source, received);
        Assert.Equal(42, received[0].ActorId);
    }

    [Fact]
    public void List_dispatch_reuses_array_for_legacy_array_subscriber()
    {
        var dispatcher = new FrameSnapshotDispatcher();
        IFrameSnapshotListDispatcher listDispatcher = dispatcher;
        var source = new[] { new ActorTransformData(9, 1f, 2f, 3f, 0f, 1f) };
        IReadOnlyList<ActorTransformData> sourceList = source;
        ActorTransformData[] received = null;
        dispatcher.Subscribe<ActorTransformData[]>(
            MobaOpCodes.Snapshot.ActorTransform,
            (_, data) => received = data);

        listDispatcher.DispatchActorTransform(7, in sourceList);

        Assert.Same(source, received);
    }

    [Fact]
    public void Snapshot_view_adapter_forwards_list_backed_snapshot()
    {
        var dispatcher = new FrameSnapshotDispatcher();
        var sink = new CapturingViewSink();
        using var adapter = new SnapshotViewAdapter(dispatcher, sink);
        IReadOnlyList<ActorTransformData> source = new List<ActorTransformData>
        {
            new ActorTransformData(31, 4f, 5f, 6f, 0f, 1f),
        };

        dispatcher.DispatchActorTransform(8, in source);

        Assert.Same(source, sink.LastTransformSnapshot.ActorTransforms);
        Assert.Equal(31, sink.LastTransformSnapshot.ActorTransforms[0].ActorId);
    }

    private sealed class CapturingViewSink : BaseBattleViewEventSink
    {
        public FrameSnapshotData LastTransformSnapshot { get; private set; }

        public override void OnActorTransformSnapshot(in FrameSnapshotData snapshot)
        {
            LastTransformSnapshot = snapshot;
        }

        protected override void OnEnterGame(in EnterGameData data) { }
        protected override void OnActorTransform(int actorId, float x, float y, float z, float rotationY, float scale) { }
        protected override void OnProjectileEvent(in ProjectileEventData data) { }
        protected override void OnAreaEvent(in AreaEventData data) { }
        protected override void OnDamageEvent(int attackerId, int targetId, int sourceId, int damageType, int damageValue, int targetHpAfter, bool isKill) { }
        protected override void OnStateHash(int frameIndex, uint stateHash) { }
        protected override void OnActorSpawn(in ActorSpawnData data) { }
    }
}
