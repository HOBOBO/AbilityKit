using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
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
    public void Import_preserves_hfsm_selection_without_stopping_movement()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(201);
        actor.AddActorBrain(1, 99, 7, 8, 0L);
        actor.AddMoveInput(.3f, .8f);
        actors.Register(201, actor);

        var catalog = new TestBrainCatalog(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.StateMachine, "idle"));
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
        Assert.Equal(.3f, actor.moveInput.Dx);
        Assert.Equal(.8f, actor.moveInput.Dz);
    }

    [Fact]
    public void Import_restores_snapshot_capable_behavior_on_same_instance()
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

        provider.ValidateImport(new FrameIndex(12), payload);
        Assert.Equal(99, decision.Value);
        Assert.Equal(0, decision.RestoreCount);

        provider.Import(new FrameIndex(12), payload);

        Assert.Equal(oldRuntimeId, actor.actorBrain.BehaviorInstanceId);
        Assert.True(brains.TryGetBehavior(actor.actorBrain.BehaviorInstanceId, out var restoredRuntime));
        Assert.Same(runtime, restoredRuntime);
        Assert.Equal(42, Assert.IsType<SnapshotDecision>(restoredRuntime.Decision).Value);
    }

    [Fact]
    public void Capture_failure_propagates_but_does_not_disable_ordinary_business()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(301);
        actors.Register(301, actor);
        using var brains = new MobaBrainService(actors,
            new TestBrainCatalog(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "stateful")),
            null, new MobaBrainDecisionDriverRegistry(new[] { new SnapshotDecisionDriver() }));
        Assert.True(brains.ActivateBrain(actor, 2, 4, 5));
        var runtime = brains.EnsureBehavior(actor);
        Assert.IsType<SnapshotDecision>(runtime.Decision).ThrowCapture = true;
        var provider = new MobaBrainRollbackProvider(actors, brains);
        Assert.Throws<InvalidOperationException>(() => provider.Export(new FrameIndex(7)));
        brains.Tick(.03f, 8);
        Assert.Equal(BehaviorPhase.Running, runtime.Phase);
        Assert.Equal(8, runtime.CurrentFrame);
    }

    [Fact]
    public void Paused_runtime_is_restored_and_reused_without_lifecycle_or_movement_changes()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(301);
        actors.Register(301, actor);
        using var brains = new MobaBrainService(actors,
            new TestBrainCatalog(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "stateful")),
            null, new MobaBrainDecisionDriverRegistry(new[] { new SnapshotDecisionDriver() }));
        Assert.True(brains.ActivateBrain(actor, 2, 4, 5));
        var runtime = brains.EnsureBehavior(actor);
        brains.Tick(.037f, 7);
        runtime.Pause();
        actor.ReplaceMoveInput(.6f, -.8f);
        var provider = new MobaBrainRollbackProvider(actors, brains);
        var frame = new FrameIndex(7);
        var payload = provider.Export(frame);
        var ended = 0;
        runtime.OnComplete += _ => ended++;
        runtime.OnInterrupt += (_, _) => ended++;
        runtime.Resume();
        brains.Tick(.013f, 8);
        provider.ValidateImport(frame, payload);
        Assert.Equal(BehaviorPhase.Running, runtime.Phase);
        provider.Import(frame, payload);
        Assert.Same(runtime, brains.EnsureBehavior(actor));
        Assert.Equal(BehaviorPhase.Paused, runtime.Phase);
        Assert.Equal(7, runtime.CurrentFrame);
        Assert.Equal(.6f, actor.moveInput.Dx);
        Assert.Equal(-.8f, actor.moveInput.Dz);
        Assert.Equal(0, ended);
        Assert.Equal(payload, provider.Export(frame));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Coordinator_accepts_extra_plain_actor_but_rejects_extra_runtime_before_import(bool withBrain)
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var actors = new MobaActorRegistry();
        using var brains = new MobaBrainService(actors,
            new TestBrainCatalog(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "stateful")),
            null, new MobaBrainDecisionDriverRegistry(new[] { new SnapshotDecisionDriver() }));
        var entityProvider = new MobaEntitasEntityRollbackProvider(context, ids, actors, null, brains: brains);
        var brainProvider = new MobaBrainRollbackProvider(actors, brains);
        var providers = new RollbackRegistry();
        providers.Register(entityProvider);
        providers.Register(brainProvider);
        var coordinator = new RollbackCoordinator(providers, new RollbackSnapshotRingBuffer(2));
        var frame = new FrameIndex(3);
        var checkpoint = coordinator.Capture(frame);
        var future = context.CreateEntity();
        future.AddActorId(ids.Next());
        actors.Register(future.actorId.Value, future);
        if (withBrain) Assert.True(brains.ActivateBrain(future, 2, 4, 5));

        Assert.Equal(!withBrain, coordinator.TryRestore(checkpoint, out var result));
        if (withBrain)
        {
            Assert.Equal(MobaBrainRollbackProvider.DefaultKey, result.ProviderKey);
            Assert.Equal(0, result.ProviderCount);
            Assert.True(future.isEnabled);
            Assert.Same(future, actors.Entries.Single().Value);
        }
        else
        {
            Assert.False(future.isEnabled);
            Assert.Empty(actors.Entries);
            Assert.Equal(checkpoint.Entries.Single(e => e.Key == brainProvider.Key).Payload, brainProvider.Export(frame));
        }
    }

    [Theory]
    [InlineData("replacement")]
    [InlineData("missing")]
    [InlineData("entity-generation")]
    public void Changed_identity_is_rejected_without_decision_restore(string change)
    {
        var actors = new MobaActorRegistry();
        var context = new ActorContext();
        var actor = context.CreateEntity();
        actor.AddActorId(301);
        actors.Register(301, actor);
        using var brains = new MobaBrainService(actors,
            new TestBrainCatalog(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "stateful")),
            null, new MobaBrainDecisionDriverRegistry(new[] { new SnapshotDecisionDriver() }));
        Assert.True(brains.ActivateBrain(actor, 2, 4, 5));
        var original = brains.EnsureBehavior(actor);
        var decision = Assert.IsType<SnapshotDecision>(original.Decision);
        var provider = new MobaBrainRollbackProvider(actors, brains);
        var payload = provider.Export(new FrameIndex(7));
        if (change == "replacement") Assert.True(brains.ActivateBrain(actor, 2, 4, 5));
        if (change == "missing") brains.DeactivateBrain(actor);
        if (change == "entity-generation")
        {
            var replacement = context.CreateEntity();
            replacement.AddActorId(301);
            actors.Register(301, replacement);
        }
        Assert.ThrowsAny<System.Exception>(() => provider.ValidateImport(new FrameIndex(7), payload));
        Assert.ThrowsAny<System.Exception>(() => provider.Import(new FrameIndex(7), payload));
        Assert.Equal(0, decision.RestoreCount);
    }

    [Fact]
    public void Creation_failure_suppression_is_state_and_is_restored_without_retrying_driver()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(301);
        actor.AddActorBrain(2, 301, 4, 5, 0);
        actors.Register(301, actor);
        var driver = new FailingDriver();
        using var brains = new MobaBrainService(actors,
            new TestBrainCatalog(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "stateful")),
            null, new MobaBrainDecisionDriverRegistry(new[] { driver }));
        Assert.Null(brains.EnsureBehavior(actor));
        var provider = new MobaBrainRollbackProvider(actors, brains);
        var frame = new FrameIndex(7);
        var payload = provider.Export(frame);
        actor.ReplaceActorBrain(2, 301, 4, 6, 0);
        Assert.Null(brains.EnsureBehavior(actor));
        Assert.Equal(2, driver.Attempts);
        provider.ValidateImport(frame, payload);
        provider.Import(frame, payload);
        Assert.Equal(5, actor.actorBrain.SourceId);
        Assert.Null(brains.EnsureBehavior(actor));
        Assert.Equal(2, driver.Attempts);
        Assert.Equal(payload, provider.Export(frame));
    }

    [Fact]
    public void Import_rejects_legacy_v1_brain_payload_without_mutation()
    {
        var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        actor.AddActorId(401);
        actors.Register(401, actor);
        var catalog = new TestBrainCatalog(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.StateMachine, "idle"));
        var profiles = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson("""
            [{"id":"idle","startState":"idle","states":[{"id":"idle","kind":"actionState","behaviorRoot":{"kind":"action","type":"noop"}}]}]
            """, profiles);
        var brains = new MobaBrainService(actors, catalog, null,
            MobaBrainDecisionDriverRegistry.CreateDefault(), profiles);
        var provider = new MobaBrainRollbackProvider(actors, brains);
        var payload = MemoryPackSerializer.Serialize(new LegacyBrainRollbackPayload(1,
            new[] { new LegacyBrainRollbackEntry(401, true, 1, 401, 7, 8) }));

        Assert.ThrowsAny<System.Exception>(() => provider.Import(new FrameIndex(1), payload));
        Assert.False(actor.hasActorBrain);
    }

    [Fact]
    public void Import_rejects_legacy_v2_empty_roster_payload_without_mutation()
    {
        var actors = new MobaActorRegistry();
        using var brains = new MobaBrainService(actors, null, null);
        var provider = new MobaBrainRollbackProvider(actors, brains);
        var frame = new FrameIndex(1);
        var before = provider.Export(frame);
        /* 旧 v2 的空名单与旧 v1 元素布局无关，包络仅含 Version 和 Entries。 */
        var legacy = MemoryPackSerializer.Serialize(new LegacyBrainRollbackPayload(2, Array.Empty<LegacyBrainRollbackEntry>()));
        Assert.ThrowsAny<Exception>(() => provider.ValidateImport(frame, legacy));
        Assert.ThrowsAny<Exception>(() => provider.Import(frame, legacy));
        Assert.Equal(before, provider.Export(frame));
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

    private sealed class SnapshotDecision : IBehaviorDecision, IBehaviorSnapshotValidator
    {
        public string DecisionType => "Snapshot";
        public string CurrentState => "Running";
        public string SnapshotType => "test.snapshot.v1";
        public int Value { get; set; }
        public int RestoreCount { get; private set; }
        public bool ThrowCapture { get; set; }

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world) =>
            DecisionResult.Continue(CurrentState);

        public byte[] CaptureSnapshot() => ThrowCapture
            ? throw new InvalidOperationException("Injected capture failure.") : System.BitConverter.GetBytes(Value);
        public void ValidateSnapshot(byte[] payload)
        {
            if (payload == null || payload.Length != 4) throw new System.InvalidOperationException("Invalid test decision state.");
        }
        public void RestoreSnapshot(byte[] payload)
        {
            RestoreCount++;
            Value = System.BitConverter.ToInt32(payload, 0);
        }
    }

    private sealed class FailingDriver : IMobaBrainDecisionDriver
    {
        public string Kind => MobaBrainDriverKeys.BehaviorTree;
        public int Attempts { get; private set; }
        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            Attempts++;
            decision = null!;
            return false;
        }
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
