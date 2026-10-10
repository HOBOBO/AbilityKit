using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaBuffTimerRollbackPreflightTests
{
    [Fact]
    public void Missing_buff_rejects_restore_before_any_provider_import()
    {
        using var actors = new MobaActorRegistry();
        var context = new ActorContext();
        try
        {
            var actor = context.CreateEntity();
            actor.AddActorId(10);
            var buff = new BuffRuntime
            {
                BuffId = 1,
                SourceId = 10,
                SourceContextId = 101,
                Remaining = 5f,
                IntervalRemainingSeconds = 1f,
                StackCount = 1,
            };
            actor.AddBuffs(new List<BuffRuntime> { buff });
            actors.Register(10, actor);
            var probe = new Probe { Value = 1 };
            var registry = new RollbackRegistry();
            registry.Register(probe);
            registry.Register(new MobaBuffTimerRollbackProvider(actors));
            var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
            var snapshot = coordinator.Capture(new FrameIndex(1));

            actor.buffs.Active.Clear();
            probe.Value = 2;
            Assert.False(coordinator.TryRestore(snapshot, out var result));
            Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
            Assert.Equal(MobaBuffTimerRollbackProvider.DefaultKey, result.ProviderKey);
            Assert.Equal(2, probe.Value);
            Assert.Empty(actor.buffs.Active);
        }
        finally
        {
            context.DestroyAllEntities();
        }
    }

    [Fact]
    public void Invalid_projection_identity_rejects_restore_before_any_provider_import()
    {
        using var actors = new MobaActorRegistry();
        var context = new ActorContext();
        try
        {
            var actor = context.CreateEntity();
            actor.AddActorId(10);
            actor.AddBuffs(new List<BuffRuntime>
            {
                new()
                {
                    BuffId = 1,
                    SourceId = 10,
                    SourceContextId = 101,
                    Remaining = 5f,
                    StackCount = 1,
                },
            });
            actors.Register(10, actor);
            var probe = new Probe { Value = 1 };
            var registry = new RollbackRegistry();
            registry.Register(probe);
            var buffs = new MobaBuffTimerRollbackProvider(actors);
            registry.Register(buffs);
            var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
            var valid = MemoryPackSerializer.Deserialize<MobaBuffTimerRollbackPayload>(
                buffs.Export(new FrameIndex(1)));
            var original = Assert.Single(valid.Entries);
            var invalid = new MobaBuffTimerRollbackEntry(original.ActorId, original.BuffId,
                original.RemainingRaw, original.IntervalRemainingRaw, original.StackCount,
                original.SourceActorId, original.SourceContextId, original.RuntimeContextId,
                original.RuntimeContextVersion, false, 0, original.ContinuousMaxStack, 42);
            var payload = MemoryPackSerializer.Serialize(
                new MobaBuffTimerRollbackPayload(valid.Version, new[] { invalid }));
            var snapshot = coordinator.Capture(new FrameIndex(1));
            var entries = snapshot.Entries;
            for (var i = 0; i < entries.Length; i++)
                if (entries[i].Key == MobaBuffTimerRollbackProvider.DefaultKey)
                    entries[i] = new WorldRollbackSnapshotEntry(entries[i].Key, payload);

            probe.Value = 2;
            Assert.False(coordinator.TryRestore(snapshot, out var result));
            Assert.Equal(MobaBuffTimerRollbackProvider.DefaultKey, result.ProviderKey);
            Assert.Equal(2, probe.Value);
        }
        finally
        {
            context.DestroyAllEntities();
        }
    }

    [Fact]
    public void Changed_continuous_projection_identity_is_rejected_before_buff_mutation()
    {
        using var actors = new MobaActorRegistry();
        var context = new ActorContext();
        try
        {
            var actor = context.CreateEntity();
            actor.AddActorId(10);
            var config = new BuffMO(new BuffDTO { Id = 1, DurationMs = 5000, MaxStacks = 1 });
            var requirements = new ContinuousTagRequirements();
            var buff = new BuffRuntime
            {
                BuffId = 1,
                SourceId = 10,
                SourceContextId = 101,
                Remaining = 5f,
                StackCount = 1,
                TagRequirements = requirements,
            };
            var continuous = new BuffContinuousRuntime(config, 10, 10, 5f, requirements);
            buff.Continuous = continuous;
            continuous.BindRuntime(buff);
            continuous.BindSourceContext(buff.SourceContextId);
            continuous.Activate();
            actor.AddBuffs(new List<BuffRuntime> { buff });
            actors.Register(10, actor);
            var provider = new MobaBuffTimerRollbackProvider(actors);
            var valid = MemoryPackSerializer.Deserialize<MobaBuffTimerRollbackPayload>(
                provider.Export(new FrameIndex(1)));
            var original = Assert.Single(valid.Entries);
            var invalid = new MobaBuffTimerRollbackEntry(original.ActorId, original.BuffId,
                original.RemainingRaw, original.IntervalRemainingRaw, original.StackCount,
                original.SourceActorId, original.SourceContextId, original.RuntimeContextId,
                original.RuntimeContextVersion, true, original.ContinuousState,
                original.ContinuousMaxStack, original.ModifierSourceId + 1);
            var payload = MemoryPackSerializer.Serialize(
                new MobaBuffTimerRollbackPayload(valid.Version, new[] { invalid }));

            buff.Remaining = 3f;
            Assert.Throws<System.InvalidOperationException>(() =>
                provider.ValidateImport(new FrameIndex(1), payload));
            Assert.Equal(3f, buff.Remaining);
        }
        finally
        {
            context.DestroyAllEntities();
        }
    }

    private sealed class Probe : IRollbackStateProvider
    {
        public int Value { get; set; }
        public int Key => 1;
        public string Name => "Probe";
        public byte[] Export(FrameIndex frame) => System.BitConverter.GetBytes(Value);
        public void Import(FrameIndex frame, byte[] payload) => Value = System.BitConverter.ToInt32(payload);
    }
}
