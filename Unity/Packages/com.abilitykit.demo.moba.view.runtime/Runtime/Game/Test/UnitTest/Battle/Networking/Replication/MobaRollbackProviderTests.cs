using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Continuous;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;
using NUnit.Framework;

namespace AbilityKit.Game.Tests
{
    public sealed class MobaRollbackProviderTests
    {
        [Test]
        public void BuffRollback_RestoresSameBuffIdInstancesByStableIdentityAndContinuousTime()
        {
            var context = new ActorContext();
            try
            {
                var actor = context.CreateEntity();
                var first = CreateBuffRuntime(7001, 10, 101L, 1001L, 6f, 0.4f, 1);
                var second = CreateBuffRuntime(7001, 20, 202L, 1002L, 9f, 0.8f, 2);
                actor.AddBuffs(new List<BuffRuntime> { first, second });

                var actors = new MobaActorRegistry();
                actors.Register(42, actor);
                var provider = new MobaBuffTimerRollbackProvider(actors);
                var payload = provider.Export(new FrameIndex(12));

                first.SourceId = 99;
                first.StackCount = 4;
                first.RuntimeContextVersion = 8;
                first.Continuous.TickManaged(2f);
                first.Continuous.IntervalRemainingSeconds = 0.1f;
                first.Continuous.SyncManagedState();

                second.SourceId = 88;
                second.StackCount = 5;
                second.RuntimeContextVersion = 9;
                second.Continuous.TickManaged(3f);
                second.Continuous.IntervalRemainingSeconds = 0.2f;
                second.Continuous.SyncManagedState();

                provider.Import(new FrameIndex(12), payload);

                Assert.That(first.SourceId, Is.EqualTo(10));
                Assert.That(first.StackCount, Is.EqualTo(1));
                Assert.That(first.RuntimeContextVersion, Is.EqualTo(1));
                Assert.That(first.Remaining, Is.EqualTo(6f).Within(0.0001f));
                Assert.That(first.IntervalRemainingSeconds, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(first.Continuous.RemainingSeconds, Is.EqualTo(6f).Within(0.0001f));
                Assert.That(first.Continuous.IntervalRemainingSeconds, Is.EqualTo(0.4f).Within(0.0001f));

                Assert.That(second.SourceId, Is.EqualTo(20));
                Assert.That(second.StackCount, Is.EqualTo(2));
                Assert.That(second.RuntimeContextVersion, Is.EqualTo(1));
                Assert.That(second.Remaining, Is.EqualTo(9f).Within(0.0001f));
                Assert.That(second.IntervalRemainingSeconds, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(second.Continuous.RemainingSeconds, Is.EqualTo(9f).Within(0.0001f));
                Assert.That(second.Continuous.IntervalRemainingSeconds, Is.EqualTo(0.8f).Within(0.0001f));
            }
            finally
            {
                context.DestroyAllEntities();
            }
        }

        [Test]
        public void BuffRollback_FailsBeforeMutationWhenInstanceMembershipChanged()
        {
            var context = new ActorContext();
            try
            {
                var actor = context.CreateEntity();
                var first = CreateBuffRuntime(7001, 10, 101L, 1001L, 6f, 0.4f, 1);
                var second = CreateBuffRuntime(7001, 20, 202L, 1002L, 9f, 0.8f, 2);
                actor.AddBuffs(new List<BuffRuntime> { first, second });

                var actors = new MobaActorRegistry();
                actors.Register(42, actor);
                var provider = new MobaBuffTimerRollbackProvider(actors);
                var payload = provider.Export(new FrameIndex(12));

                actor.buffs.Active.Remove(second);
                first.Remaining = 3f;

                var exception = Assert.Throws<InvalidOperationException>(
                    () => provider.Import(new FrameIndex(12), payload));
                Assert.That(exception.Message, Does.Contain("membership changed"));
                Assert.That(first.Remaining, Is.EqualTo(3f), "Validation must complete before mutation.");
            }
            finally
            {
                context.DestroyAllEntities();
            }
        }

        [Test]
        public void ShieldRollback_RestoresLayerCollectionIdentityAndFullMutableState()
        {
            var context = new ActorContext();
            var shields = new MobaShieldService();
            try
            {
                var actor = context.CreateEntity();
                var actors = new MobaActorRegistry();
                actors.Register(42, actor);
                var provider = new MobaShieldRollbackProvider(actors, shields);

                var original = CreateShieldLayer(instanceId: 7, currentValue: 25f);
                shields.AddShield(42, original);
                Assert.That(shields.TryGetContainer(42, out var container), Is.True);
                container.NextInstanceId = 12;
                container.TotalRemaining = Fixed64.FromSingle(25f);
                container.Dirty = false;
                var payload = provider.Export(new FrameIndex(20));

                container.Layers[0].CurrentValue = Fixed64.FromSingle(1f);
                container.Layers[0].Priority = -1;
                container.Layers.Add(CreateShieldLayer(instanceId: 99, currentValue: 50f));
                container.NextInstanceId = 99;
                container.TotalRemaining = Fixed64.FromSingle(51f);
                container.Dirty = true;

                provider.Import(new FrameIndex(20), payload);

                Assert.That(shields.TryGetContainer(42, out var restored), Is.True);
                Assert.That(restored.NextInstanceId, Is.EqualTo(12));
                Assert.That(restored.TotalRemaining, Is.EqualTo(Fixed64.FromSingle(25f)));
                Assert.That(restored.Dirty, Is.False);
                Assert.That(restored.Layers, Has.Count.EqualTo(1));
                Assert.That(restored.Layers[0].InstanceId, Is.EqualTo(7));
                Assert.That(restored.Layers[0].CurrentValue, Is.EqualTo(Fixed64.FromSingle(25f)));
                Assert.That(restored.Layers[0].Priority, Is.EqualTo(5));
                Assert.That(restored.Layers[0].SourceContextId, Is.EqualTo(3001L));
                Assert.That(restored.Layers[0].RootContextId, Is.EqualTo(3000L));
                Assert.That(restored.Layers[0].OwnerContextId, Is.EqualTo(3002L));
                Assert.That(restored.Layers[0].UsesSharedPoolValue, Is.True);
                Assert.That(restored.Layers[0].TransferPolicy, Is.EqualTo(ShieldTransferPolicy.SplitRemaining));
            }
            finally
            {
                shields.Dispose();
                context.DestroyAllEntities();
            }
        }

        [Test]
        public void ShieldRollback_FailsBeforeMutationWhenActorCollectionChanged()
        {
            var context = new ActorContext();
            var shields = new MobaShieldService();
            try
            {
                var actors = new MobaActorRegistry();
                actors.Register(42, context.CreateEntity());
                shields.AddShield(42, CreateShieldLayer(instanceId: 7, currentValue: 25f));
                var provider = new MobaShieldRollbackProvider(actors, shields);
                var payload = provider.Export(new FrameIndex(20));

                shields.TryGetContainer(42, out var current);
                current.Layers[0].CurrentValue = Fixed64.FromSingle(3f);
                actors.Register(43, context.CreateEntity());

                var exception = Assert.Throws<InvalidOperationException>(
                    () => provider.Import(new FrameIndex(20), payload));
                Assert.That(exception.Message, Does.Contain("Actor collection changed"));
                Assert.That(current.Layers[0].CurrentValue, Is.EqualTo(Fixed64.FromSingle(3f)),
                    "Actor-set validation must complete before Shield mutation.");
            }
            finally
            {
                shields.Dispose();
                context.DestroyAllEntities();
            }
        }

        [Test]
        public void BuffStateRecoveryEntry_RestoresCapabilityHandleWithoutClaimingLiveRuntime()
        {
            var entry = new MobaBuffStateRecoveryEntry(
                targetActorId: 42,
                buffId: 7001,
                remainingSeconds: 6f,
                intervalRemainingSeconds: 0.4f,
                sourceActorId: 10,
                stackCount: 2,
                sourceContextId: 3002L,
                runtimeContextId: 4001L,
                runtimeContextVersion: 3L,
                originSourceActorId: 10,
                originTargetActorId: 42,
                originTraceKind: (int)MobaTraceKind.BuffApply,
                originConfigId: 7001,
                originImmediateContextId: 3002L,
                originParentContextId: 3002L,
                originRootContextId: 3001L,
                originOwnerContextId: 3002L,
                skillRuntimeId: 51L,
                skillRuntimeGeneration: 4,
                skillRuntimeRootTraceContextId: 3001L);
            var runtime = new BuffRuntime();

            entry.ApplyTo(runtime);

            Assert.That(runtime.ContextSource.Boundary, Is.EqualTo(MobaContextSourceBoundary.Snapshot));
            Assert.That(runtime.ContextSource.HasLiveRuntime, Is.False);
            Assert.That(runtime.SkillRuntimeHandle.RuntimeId, Is.EqualTo(51L));
            Assert.That(runtime.SkillRuntimeHandle.Generation, Is.EqualTo(4));
            Assert.That(runtime.SkillRuntimeHandle.RootTraceContextId, Is.EqualTo(3001L));
            Assert.That(runtime.SkillRuntimeRetainHandle.IsValid, Is.False);
        }

        private static BuffRuntime CreateBuffRuntime(
            int buffId,
            int sourceActorId,
            long sourceContextId,
            long runtimeContextId,
            float remaining,
            float intervalRemaining,
            int stackCount)
        {
            var config = new BuffMO(new BuffDTO
            {
                Id = buffId,
                DurationMs = 10000,
                IntervalMs = 1000,
                MaxStacks = 5,
            });
            var requirements = new ContinuousTagRequirements();
            var runtime = new BuffRuntime
            {
                BuffId = buffId,
                SourceId = sourceActorId,
                SourceContextId = sourceContextId,
                RuntimeContextId = runtimeContextId,
                RuntimeContextVersion = 1,
                Remaining = remaining,
                IntervalRemainingSeconds = intervalRemaining,
                StackCount = stackCount,
                TagRequirements = requirements,
            };
            var continuous = new BuffContinuousRuntime(config, sourceActorId, 42, remaining, requirements);
            runtime.Continuous = continuous;
            continuous.BindRuntime(runtime);
            continuous.BindSourceContext(sourceContextId);
            continuous.Refresh(sourceActorId, remaining, stackCount, config.MaxStacks, requirements);
            continuous.IntervalRemainingSeconds = intervalRemaining;
            continuous.Activate();
            continuous.SyncManagedState();
            return runtime;
        }

        private static ShieldLayer CreateShieldLayer(int instanceId, float currentValue)
        {
            return new ShieldLayer
            {
                InstanceId = instanceId,
                ShieldId = 8001,
                SourceActorId = 10,
                OwnerActorId = 11,
                TargetActorId = 42,
                SourceContextId = 3001L,
                RootContextId = 3000L,
                OwnerContextId = 3002L,
                SharedPoolId = 5,
                SharedPoolMemberId = 6,
                UsesSharedPoolValue = true,
                TransferredFromActorId = 40,
                TransferredToActorId = 42,
                TransferredAtFrame = 18,
                TransferRatio = Fixed64.FromSingle(0.5f),
                CurrentValue = Fixed64.FromSingle(currentValue),
                MaxValue = Fixed64.FromSingle(30f),
                InitialValue = Fixed64.FromSingle(30f),
                AbsorbRatio = Fixed64.FromSingle(0.75f),
                Priority = 5,
                DamageTypeMask = 3,
                StartFrame = 10,
                ExpireFrame = 100,
                RemoveWhenDepleted = false,
                StackingPolicy = ShieldStackingPolicy.Independent,
                ConsumePolicy = ShieldConsumePolicy.NewestFirst,
                SharePolicy = ShieldSharePolicy.WeightedMemberShare,
                TransferPolicy = ShieldTransferPolicy.SplitRemaining,
            };
        }
    }
}
