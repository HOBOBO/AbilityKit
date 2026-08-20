using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.StateMachine;
using Xunit;
using AbilityKit.Ability.Behavior;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaBrainRollbackProviderTests
{
    [Fact]
    public void Import_rebuilds_brain_selection_without_reusing_runtime_instance_id()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(201);
        actor.AddActorBrain(1, 99, 7, 8, 123L);
        actors.Register(201, actor);

        var catalog = new TestBrainCatalog(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "idle"));
        var profiles = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson("""
            [{
              "id":"idle",
              "startState":"idle",
              "states":[{
                "id":"idle",
                "kind":"actionState",
                "behaviorRoot":{"kind":"action","type":"noop"}
              }]
            }]
            """, profiles);
        var brains = new MobaBrainService(
            actors,
            catalog,
            config: null,
            MobaBrainDecisionDriverRegistry.CreateDefault(),
            profiles);
        var provider = new MobaBrainRollbackProvider(actors, brains);
        var payload = provider.Export(new FrameIndex(10));

        provider.Import(new FrameIndex(10), payload);

        Assert.True(actor.hasActorBrain);
        Assert.Equal(1, actor.actorBrain.BrainId);
        Assert.Equal(99, actor.actorBrain.OwnerActorId);
        Assert.Equal(7, actor.actorBrain.SourceKind);
        Assert.Equal(8, actor.actorBrain.SourceId);
        Assert.Equal(0L, actor.actorBrain.BehaviorInstanceId);
    }

    [Fact]
    public void Import_restores_snapshot_capable_behavior_after_recreation()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(301);
        actors.Register(301, actor);
        var catalog = new TestBrainCatalog(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "stateful"));
        var registry = new MobaBrainDecisionDriverRegistry(new[] { new SnapshotDecisionDriver() });
        var brains = new MobaBrainService(actors, catalog, null, registry);
        Assert.True(brains.ActivateBrain(actor, 2, 4, 5));
        var oldRuntimeId = actor.actorBrain.BehaviorInstanceId;
        Assert.True(brains.TryGetBehavior(oldRuntimeId, out var runtime));
        var decision = Assert.IsType<SnapshotDecision>(runtime.Decision);
        decision.Value = 42;
        var provider = new MobaBrainRollbackProvider(actors, brains);
        var payload = provider.Export(new FrameIndex(12));
        decision.Value = 99;

        provider.Import(new FrameIndex(12), payload);

        Assert.NotEqual(oldRuntimeId, actor.actorBrain.BehaviorInstanceId);
        Assert.True(brains.TryGetBehavior(actor.actorBrain.BehaviorInstanceId, out var restoredRuntime));
        Assert.Equal(42, Assert.IsType<SnapshotDecision>(restoredRuntime.Decision).Value);
    }

    [Fact]
    public void Import_accepts_legacy_v1_brain_payload()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(401);
        actors.Register(401, actor);
        var catalog = new TestBrainCatalog(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "idle"));
        var profiles = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson("""
            [{"id":"idle","startState":"idle","states":[{"id":"idle","kind":"actionState","behaviorRoot":{"kind":"action","type":"noop"}}]}]
            """, profiles);
        var brains = new MobaBrainService(actors, catalog, null,
            MobaBrainDecisionDriverRegistry.CreateDefault(), profiles);
        var provider = new MobaBrainRollbackProvider(actors, brains);
        var payload = MemoryPackSerializer.Serialize(new LegacyBrainRollbackPayload(1,
            new[] { new LegacyBrainRollbackEntry(401, true, 1, 401, 7, 8) }));

        provider.Import(new FrameIndex(1), payload);

        Assert.True(actor.hasActorBrain);
        Assert.Equal(1, actor.actorBrain.BrainId);
        Assert.Equal(7, actor.actorBrain.SourceKind);
        Assert.Equal(8, actor.actorBrain.SourceId);
    }

    private sealed class TestBrainCatalog : IMobaActorBrainCatalog
    {
        private readonly MobaActorBrainDefinition _definition;

        public TestBrainCatalog(MobaActorBrainDefinition definition) => _definition = definition;

        public IReadOnlyList<MobaActorBrainDefinition> Definitions => new[] { _definition };

        public bool TryGet(int brainId, out MobaActorBrainDefinition definition)
        {
            definition = _definition;
            return brainId == _definition.BrainId;
        }

        public void Dispose()
        {
        }
    }

    private sealed class SnapshotDecisionDriver : IMobaBrainDecisionDriver
    {
        public string Kind => MobaBrainDriverKeys.BehaviorTree;

        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = new SnapshotDecision();
            return true;
        }
    }

    private sealed class SnapshotDecision : IBehaviorDecision, IBehaviorRuntimeSnapshot
    {
        public string DecisionType => "Snapshot";
        public string CurrentState => "Running";
        public string SnapshotType => "test.snapshot.v1";
        public int Value { get; set; }

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world) =>
            DecisionResult.Continue(CurrentState);

        public byte[] CaptureSnapshot() => System.BitConverter.GetBytes(Value);
        public void RestoreSnapshot(byte[] payload) => Value = System.BitConverter.ToInt32(payload, 0);
    }

}

[MemoryPackable]
public readonly partial struct LegacyBrainRollbackPayload
{
    [MemoryPackOrder(0)] public readonly int Version;
    [MemoryPackOrder(1)] public readonly LegacyBrainRollbackEntry[] Entries;
    [MemoryPackConstructor]
    public LegacyBrainRollbackPayload(int version, LegacyBrainRollbackEntry[] entries) { Version = version; Entries = entries; }
}

[MemoryPackable]
public readonly partial struct LegacyBrainRollbackEntry
{
    [MemoryPackOrder(0)] public readonly int ActorId;
    [MemoryPackOrder(1)] public readonly bool HasBrain;
    [MemoryPackOrder(2)] public readonly int BrainId;
    [MemoryPackOrder(3)] public readonly int OwnerActorId;
    [MemoryPackOrder(4)] public readonly int SourceKind;
    [MemoryPackOrder(5)] public readonly int SourceId;
    public LegacyBrainRollbackEntry(int actorId, bool hasBrain, int brainId, int ownerActorId, int sourceKind, int sourceId)
    {
        ActorId = actorId; HasBrain = hasBrain; BrainId = brainId; OwnerActorId = ownerActorId;
        SourceKind = sourceKind; SourceId = sourceId;
    }
}
