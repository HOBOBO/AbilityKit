using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Lifecycle;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaBuffLifecycleTransactionTests
    {
        private const int ActorId = 921;
        private const int BuffId = 922;
        private const int SourceActorId = 923;
        private const long SourceContextId = 924L;

        private Contexts _contexts;
        private ActorIdIndex _index;
        private MobaActorRegistry _registry;
        private MobaEntityManager _entities;
        private MobaActorLookupService _lookup;
        private ActorEntity _target;

        [SetUp]
        public void SetUp()
        {
            _contexts = new Contexts();
            _index = new ActorIdIndex(_contexts);
            _registry = new MobaActorRegistry();
            _entities = new MobaEntityManager(null);
            _lookup = new MobaActorLookupService(_index, _registry, _entities, _contexts);
            _target = _contexts.actor.CreateEntity();
            _target.AddActorId(ActorId);
        }

        [TearDown]
        public void TearDown()
        {
            _index.Dispose();
            _contexts.actor.DestroyAllEntities();
            _registry.Dispose();
            _entities.Dispose();
        }

        [Test]
        public void ReplaceAt_WhenExpectedRuntimeDoesNotMatch_LeavesListUnchanged()
        {
            var existing = new BuffRuntime { BuffId = BuffId };
            var unexpected = new BuffRuntime { BuffId = BuffId + 1 };
            var replacement = new BuffRuntime { BuffId = BuffId };
            var list = new List<BuffRuntime> { existing };

            var replaced = BuffRepository.ReplaceAt(list, 0, unexpected, replacement);

            Assert.That(replaced, Is.False);
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0], Is.SameAs(existing));
        }

        [Test]
        public void EndRuntime_WhenExpectedRuntimeDoesNotMatch_ReturnsFalseWithoutCleanup()
        {
            var buff = GetBuff(CreateConfigs());
            var existing = CreateRuntime(buff);
            var unexpected = CreateRuntime(buff);
            var list = new List<BuffRuntime> { existing };
            _target.AddBuffs(list);
            var endFlow = new BuffEndFlow(null, null, null, null);

            var ended = endFlow.EndRuntime(
                _target,
                list,
                0,
                unexpected,
                SourceActorId,
                TraceLifecycleReason.Dispelled);

            Assert.That(ended, Is.False);
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0], Is.SameAs(existing));
            Assert.That(existing.BuffId, Is.EqualTo(BuffId));
            Assert.That(unexpected.BuffId, Is.EqualTo(BuffId));
        }

        [Test]
        public void EndRuntime_WhenCommitPostHookThrows_RemovesAndFullyCleansRuntime()
        {
            var buff = GetBuff(CreateConfigs());
            var runtime = CreateRuntime(buff);
            var list = new List<BuffRuntime> { runtime };
            _target.AddBuffs(list);
            _target.AddEffectListeners(new List<EffectListenerRuntime>
            {
                new EffectListenerRuntime { SourceContextId = SourceContextId },
            });

            var hooks = new MobaRuntimeLifecycleHookService();
            hooks.Register(new ThrowingEndHook(() =>
            {
                Assert.That(list, Is.Empty);
                Assert.That(runtime.Continuous, Is.Null);
                Assert.That(_target.effectListeners.Active, Is.Empty);
            }));
            var bindings = new BuffRuntimeBindingCoordinator(
                hooks,
                new BuffContinuousBindingService(null, null),
                null);
            var endFlow = new BuffEndFlow(null, null, null, bindings);

            var error = Assert.Throws<InvalidOperationException>(() =>
                endFlow.EndRuntime(
                    _target,
                    list,
                    0,
                    runtime,
                    SourceActorId,
                    TraceLifecycleReason.Dispelled));

            Assert.That(error.Message, Is.EqualTo("end hook failed"));
            Assert.That(list, Is.Empty);
            Assert.That(runtime.BuffId, Is.Zero);
            Assert.That(runtime.SourceContextId, Is.Zero);
            Assert.That(runtime.Continuous, Is.Null);
            Assert.That(runtime.ModifierBindings, Is.Null);
        }

        [Test]
        public void ApplyReplace_WhenCandidatePrecommitSucceeds_AtomicallyReplacesSlot()
        {
            var configs = CreateConfigs();
            var buff = GetBuff(configs);
            var existing = CreateRuntime(buff);
            var list = new List<BuffRuntime> { existing };
            _target.AddBuffs(list);
            var hooks = new MobaRuntimeLifecycleHookService();
            hooks.Register(new ReplacementActivationHook(list, existing));
            var lifecycle = CreateLifecycle(configs, hooks, new BuffContinuousBindingService(null, null));
            var request = CreateRequest();

            var applied = lifecycle.Apply(in request);

            Assert.That(applied, Is.True);
            Assert.That(lifecycle.LastReject.HasValue, Is.False);
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0], Is.Not.SameAs(existing));
            Assert.That(list[0].BuffId, Is.EqualTo(BuffId));
            Assert.That(list[0].SourceContextId, Is.EqualTo(SourceContextId));
            Assert.That(list[0].Continuous, Is.Not.Null);
            Assert.That(existing.BuffId, Is.Zero);
        }

        [Test]
        public void ApplyReplace_WhenFailedHookThrows_StillCleansCandidate()
        {
            var configs = CreateConfigs();
            var buff = GetBuff(configs);
            var existing = CreateRuntime(buff);
            var list = new List<BuffRuntime> { existing };
            _target.AddBuffs(list);
            var hook = new ThrowingFailedHook();
            var hooks = new MobaRuntimeLifecycleHookService();
            hooks.Register(hook);
            var lifecycle = CreateLifecycle(
                configs,
                hooks,
                new BuffContinuousBindingService(new RejectingContinuousManager(), null));
            var request = CreateRequest();

            var error = Assert.Throws<InvalidOperationException>(() => lifecycle.Apply(in request));

            Assert.That(error.Message, Is.EqualTo("failed hook failed"));
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0], Is.SameAs(existing));
            Assert.That(hook.Runtime, Is.Not.Null);
            Assert.That(hook.Runtime.BuffId, Is.Zero);
            Assert.That(hook.Runtime.SourceContextId, Is.Zero);
            Assert.That(hook.Runtime.Continuous, Is.Null);
            Assert.That(hook.Runtime.ModifierBindings, Is.Null);
        }

        [Test]
        public void ApplyReplace_WhenCandidateContinuousActivationFails_PreservesExistingRuntime()
        {
            var configs = CreateConfigs();
            var buff = GetBuff(configs);
            var existing = CreateRuntime(buff);
            var list = new List<BuffRuntime> { existing };
            _target.AddBuffs(list);
            var lifecycle = CreateLifecycle(
                configs,
                new MobaRuntimeLifecycleHookService(),
                new BuffContinuousBindingService(new RejectingContinuousManager(), null));
            var request = CreateRequest();

            var applied = lifecycle.Apply(in request);

            Assert.That(applied, Is.False);
            Assert.That(lifecycle.LastReject.Kind, Is.EqualTo(BuffLifecycleRejectCode.ApplyContinuousActivationFailed));
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0], Is.SameAs(existing));
            Assert.That(existing.BuffId, Is.EqualTo(BuffId));
            Assert.That(existing.SourceContextId, Is.EqualTo(SourceContextId));
            Assert.That(existing.Continuous, Is.Not.Null);
        }

        private BuffLifecycleExecutor CreateLifecycle(
            MobaConfigDatabase configs,
            MobaRuntimeLifecycleHookService hooks,
            BuffContinuousBindingService continuousBindings)
        {
            return new BuffLifecycleExecutor(
                configs,
                _lookup,
                hooks,
                null,
                null,
                new BuffRepository(),
                null,
                null,
                null,
                null,
                new BuffStackingPolicyApplier(),
                continuousBindings,
                null,
                null,
                null);
        }

        private static MobaConfigDatabase CreateConfigs()
        {
            var configs = new MobaConfigDatabase();
            var result = configs.ReloadFromDtoArrays(
                new Dictionary<Type, Array>
                {
                    [typeof(BuffDTO)] = new[]
                    {
                        new BuffDTO
                        {
                            Id = BuffId,
                            Name = "buff-lifecycle-transaction-test",
                            DurationMs = 10000,
                            IntervalMs = 1000,
                            StackingPolicy = (int)BuffStackingPolicy.Replace,
                            MaxStacks = 1,
                            OnAddEffects = Array.Empty<int>(),
                            OnRemoveEffects = Array.Empty<int>(),
                            OnIntervalEffects = Array.Empty<int>(),
                            TriggerIds = Array.Empty<int>(),
                            TagNames = Array.Empty<string>(),
                            Modifiers = Array.Empty<ContinuousModifierDTO>(),
                        },
                    },
                },
                strict: false);
            Assert.That(result.Succeeded, Is.True, result.Error);
            return configs;
        }

        private static BuffMO GetBuff(MobaConfigDatabase configs)
        {
            Assert.That(configs.TryGetBuff(BuffId, out var buff), Is.True);
            return buff;
        }

        private static BuffRuntime CreateRuntime(BuffMO buff)
        {
            var runtime = new BuffStackingPolicyApplier().CreateNewRuntime(buff, SourceActorId, 10f);
            runtime.SourceContextId = SourceContextId;
            var continuous = new BuffContinuousBindingService(null, null);
            Assert.That(continuous.EnsureActive(runtime, buff, SourceActorId, ActorId, 10f, null), Is.True);
            runtime.ModifierBindings = new List<AbilityKit.Demo.Moba.Components.BuffModifierBinding>();
            return runtime;
        }

        private static BuffApplyRequest CreateRequest()
        {
            return new BuffApplyRequest
            {
                TargetActorId = ActorId,
                BuffId = BuffId,
                SourceActorId = SourceActorId,
                SourceContextId = SourceContextId,
            };
        }

        private sealed class ThrowingEndHook : IMobaRuntimeLifecycleHook
        {
            private readonly Action _assertCommitted;

            public ThrowingEndHook(Action assertCommitted)
            {
                _assertCommitted = assertCommitted;
            }

            public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent)
            {
                if (lifecycleEvent.Kind != MobaRuntimeLifecycleEventKind.Ended) return;
                _assertCommitted();
                throw new InvalidOperationException("end hook failed");
            }
        }

        private sealed class ThrowingFailedHook : IMobaRuntimeLifecycleHook
        {
            public BuffRuntime Runtime { get; private set; }

            public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent)
            {
                if (lifecycleEvent.Kind != MobaRuntimeLifecycleEventKind.Failed) return;
                Runtime = lifecycleEvent.Runtime as BuffRuntime;
                Assert.That(Runtime, Is.Not.Null);
                throw new InvalidOperationException("failed hook failed");
            }
        }

        private sealed class ReplacementActivationHook : IMobaRuntimeLifecycleHook
        {
            private readonly List<BuffRuntime> _list;
            private readonly BuffRuntime _existing;

            public ReplacementActivationHook(List<BuffRuntime> list, BuffRuntime existing)
            {
                _list = list;
                _existing = existing;
            }

            public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent)
            {
                if (lifecycleEvent.Kind != MobaRuntimeLifecycleEventKind.Activated) return;
                Assert.That(_list, Has.Count.EqualTo(1));
                Assert.That(_list[0], Is.SameAs(lifecycleEvent.Runtime));
                Assert.That(_list[0], Is.Not.SameAs(_existing));
            }
        }

        private sealed class RejectingContinuousManager : IContinuousManager
        {
            public int ActiveCount => 0;
            public int TotalCount => 0;

            public bool Register(IContinuous continuous) => false;
            public void Unregister(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.CleanedUp) { }
            public bool TryActivate(IContinuous continuous) => false;
            public bool TryPause(IContinuous continuous) => false;
            public bool TryResume(IContinuous continuous) => false;
            public bool TryEnd(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.Completed) => false;
            public bool TryInterrupt(IContinuous continuous, string reason) => false;
            public IReadOnlyList<IContinuous> GetOwnerContinuous(long ownerId) => Array.Empty<IContinuous>();
            public IReadOnlyList<IContinuous> GetOwnerActiveContinuous(long ownerId) => Array.Empty<IContinuous>();
            public void InterruptAll(long ownerId, string reason) { }
            public void PauseAll(long ownerId) { }
            public void ResumeAll(long ownerId) { }
        }
    }
}
