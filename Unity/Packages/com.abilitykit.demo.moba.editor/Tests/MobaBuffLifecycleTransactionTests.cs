using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
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
        public void RepositoryIndex_PreservesFirstMatchAndListScopedInvalidation()
        {
            var first = new BuffRuntime
            {
                BuffId = BuffId,
                SourceId = SourceActorId,
                SourceContextId = SourceContextId,
            };
            var second = new BuffRuntime
            {
                BuffId = BuffId,
                SourceId = SourceActorId,
                SourceContextId = SourceContextId + 1,
            };
            var firstList = new List<BuffRuntime> { first, second };
            var otherList = new List<BuffRuntime>
            {
                new BuffRuntime
                {
                    BuffId = BuffId + 1,
                    SourceId = SourceActorId + 1,
                    SourceContextId = SourceContextId + 2,
                },
            };

            Assert.That(
                BuffRepository.TryGetRuntime(
                    firstList,
                    BuffRuntimeKey.MatchBuff(BuffId),
                    out var byBuff,
                    out var byBuffIndex),
                Is.True);
            Assert.That(byBuff, Is.SameAs(first));
            Assert.That(byBuffIndex, Is.Zero);
            Assert.That(
                BuffRepository.TryGetRuntime(
                    firstList,
                    BuffRuntimeKey.MatchBuffAndSource(BuffId, SourceActorId),
                    out var bySource,
                    out var bySourceIndex),
                Is.True);
            Assert.That(bySource, Is.SameAs(first));
            Assert.That(bySourceIndex, Is.Zero);
            Assert.That(
                BuffRepository.TryGetRuntime(
                    firstList,
                    BuffRuntimeKey.MatchInstance(BuffId, SourceActorId, SourceContextId + 1),
                    out var byInstance,
                    out var byInstanceIndex),
                Is.True);
            Assert.That(byInstance, Is.SameAs(second));
            Assert.That(byInstanceIndex, Is.EqualTo(1));

            Assert.That(
                BuffRepository.TryGetRuntime(
                    otherList,
                    BuffRuntimeKey.MatchBuff(BuffId + 1),
                    out var other,
                    out var otherIndex),
                Is.True);
            firstList.RemoveAt(0);
            BuffRepository.MarkDirty(firstList);

            Assert.That(
                BuffRepository.TryGetRuntime(
                    firstList,
                    BuffRuntimeKey.MatchBuff(BuffId),
                    out var remaining,
                    out var remainingIndex),
                Is.True);
            Assert.That(remaining, Is.SameAs(second));
            Assert.That(remainingIndex, Is.Zero);
            Assert.That(
                BuffRepository.TryGetRuntime(
                    otherList,
                    BuffRuntimeKey.MatchBuff(BuffId + 1),
                    out var unchangedOther,
                    out var unchangedOtherIndex),
                Is.True);
            Assert.That(unchangedOther, Is.SameAs(other));
            Assert.That(unchangedOtherIndex, Is.EqualTo(otherIndex));
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
        public void EndRuntime_NoneReason_NormalizesReasonAndMapsContinuousCompletion()
        {
            var configs = CreateConfigs();
            var buff = GetBuff(configs);
            var runtime = CreateRuntime(buff);
            var list = new List<BuffRuntime> { runtime };
            _target.AddBuffs(list);
            var continuous = new RecordingContinuousManager();
            var recorder = new DiagnosticDraftRecorder();
            var hooks = new MobaRuntimeLifecycleHookService();
            var lifecycleRecorder = new LifecycleRecorder();
            hooks.Register(lifecycleRecorder);
            var bindings = new BuffRuntimeBindingCoordinator(
                hooks,
                new BuffContinuousBindingService(continuous, null),
                null);
            var endFlow = new BuffEndFlow(
                configs,
                null,
                new BuffLifecycleNotifier(
                    null,
                    null,
                    null,
                    new MobaBattleObservationRecorderAdapter(recorder)),
                bindings);

            var ended = endFlow.EndRuntime(
                _target,
                list,
                0,
                runtime,
                SourceActorId,
                TraceLifecycleReason.None);

            Assert.That(ended, Is.True);
            Assert.That(list, Is.Empty);
            Assert.That(continuous.EndReasons[0], Is.EqualTo(ContinuousEndReason.Completed));
            Assert.That(recorder.Drafts, Has.Count.EqualTo(1));
            Assert.That(recorder.Drafts[0].Payload.TryGetBuffLifecycle(out var removed), Is.True);
            Assert.That(removed.RemoveReason, Is.EqualTo((int)TraceLifecycleReason.Expired));
            Assert.That(lifecycleRecorder.EndedCount, Is.EqualTo(1));
            Assert.That(runtime.BuffId, Is.Zero);
        }

        [Test]
        public void EndRuntime_WhenContinuousEndThrows_ContinuesRemainingCleanupAndRethrowsFirstFailure()
        {
            var configs = CreateConfigs();
            var buff = GetBuff(configs);
            var runtime = CreateRuntime(buff);
            var list = new List<BuffRuntime> { runtime };
            _target.AddBuffs(list);
            _target.AddEffectListeners(new List<EffectListenerRuntime>
            {
                new EffectListenerRuntime { SourceContextId = SourceContextId },
            });
            var continuous = new RecordingContinuousManager(throwOnEnd: true);
            var recorder = new DiagnosticDraftRecorder();
            var hooks = new MobaRuntimeLifecycleHookService();
            var lifecycleRecorder = new LifecycleRecorder();
            hooks.Register(lifecycleRecorder);
            var bindings = new BuffRuntimeBindingCoordinator(
                hooks,
                new BuffContinuousBindingService(continuous, null),
                null);
            var endFlow = new BuffEndFlow(
                configs,
                null,
                new BuffLifecycleNotifier(
                    null,
                    null,
                    null,
                    new MobaBattleObservationRecorderAdapter(recorder)),
                bindings);

            var error = Assert.Throws<InvalidOperationException>(() =>
                endFlow.EndRuntime(
                    _target,
                    list,
                    0,
                    runtime,
                    SourceActorId,
                    TraceLifecycleReason.Dispelled));

            Assert.That(error.Message, Is.EqualTo("continuous end failed"));
            Assert.That(continuous.EndReasons[0], Is.EqualTo(ContinuousEndReason.Interrupted));
            Assert.That(list, Is.Empty);
            Assert.That(_target.effectListeners.Active, Is.Empty);
            Assert.That(recorder.Drafts, Has.Count.EqualTo(1));
            Assert.That(lifecycleRecorder.EndedCount, Is.EqualTo(1));
            Assert.That(runtime.BuffId, Is.Zero);
            Assert.That(runtime.Continuous, Is.Null);
        }

        [Test]
        public void DrainPending_WhenBudgetExhausted_PreservesUnconsumedTail()
        {
            var service = CreateBuffService(CreateConfigs());
            EnqueueApply(service, SourceActorId);
            EnqueueApply(service, SourceActorId + 1);

            service.DrainPending(1);

            Assert.That(GetCollectionCount(service, "_pending"), Is.EqualTo(1));
            Assert.That(_target.buffs.Active, Has.Count.EqualTo(1));

            service.DrainPending(1);

            Assert.That(GetCollectionCount(service, "_pending"), Is.Zero);
            Assert.That(_target.buffs.Active, Has.Count.EqualTo(2));
            Assert.That(_target.buffs.Active[1].SourceId, Is.EqualTo(SourceActorId + 1));
        }

        [Test]
        public void DrainPending_WhenCommandThrows_ContinuesWithNextCommand()
        {
            var hooks = new MobaRuntimeLifecycleHookService();
            var throwingHook = new ThrowOnceActivatedHook();
            hooks.Register(throwingHook);
            var exceptions = new ExceptionPolicyRecorder();
            var service = CreateBuffService(CreateConfigs(), hooks, exceptions);
            EnqueueApply(service, SourceActorId);
            EnqueueApply(service, SourceActorId + 1);

            service.DrainPending(2);

            Assert.That(exceptions.Exceptions, Has.Count.EqualTo(1));
            Assert.That(exceptions.Exceptions[0].Message, Is.EqualTo("activation hook failed"));
            Assert.That(GetCollectionCount(service, "_pending"), Is.Zero);
            Assert.That(_target.buffs.Active, Has.Count.EqualTo(2));
            Assert.That(_target.buffs.Active[1].SourceId, Is.EqualTo(SourceActorId + 1));
        }

        [Test]
        public void DrainPending_WhenExecutionAppendsCommand_ConsumesTailWithinSameBudget()
        {
            var service = new MobaBuffService();
            var hooks = new MobaRuntimeLifecycleHookService();
            var appendHook = new AppendApplyOnceHook(service);
            hooks.Register(appendHook);
            ConfigureBuffService(service, CreateConfigs(), hooks, null);
            EnqueueApply(service, SourceActorId);

            service.DrainPending(2);

            Assert.That(appendHook.ActivatedCount, Is.EqualTo(2));
            Assert.That(GetCollectionCount(service, "_pending"), Is.Zero);
            Assert.That(_target.buffs.Active, Has.Count.EqualTo(2));
            Assert.That(_target.buffs.Active[1].SourceId, Is.EqualTo(SourceActorId + 1));
        }

        [Test]
        public void ImmediateApply_ReturnsExecutionResultAndClearsAwaitedState()
        {
            var service = CreateBuffService(CreateConfigs());

            var succeeded = service.ApplyBuffImmediate(ActorId, BuffId, SourceActorId, 0);
            var rejected = service.ApplyBuffImmediate(ActorId, BuffId + 1, SourceActorId, 0);

            Assert.That(succeeded, Is.True);
            Assert.That(rejected, Is.False);
            Assert.That(GetCollectionCount(service, "_pending"), Is.Zero);
            Assert.That(GetCollectionCount(service, "_awaitedCommandSeqs"), Is.Zero);
            Assert.That(GetCollectionCount(service, "_commandResults"), Is.Zero);
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

        [Test]
        public void LifecycleNotifier_CollectsAppliedRefreshStackAndRemovedPayloads()
        {
            var buff = GetBuff(CreateConfigs());
            var runtime = CreateRuntime(buff);
            runtime.Remaining = 8.5f;
            runtime.IntervalRemainingSeconds = 0.75f;
            runtime.StackCount = 1;
            runtime.ModifierBindings.Add(new AbilityKit.Demo.Moba.Components.BuffModifierBinding());
            var recorder = new DiagnosticDraftRecorder();
            var notifier = new BuffLifecycleNotifier(
                null,
                null,
                null,
                new MobaBattleObservationRecorderAdapter(recorder));

            notifier.AppliedNew(buff, SourceActorId, ActorId, 10f, runtime);
            notifier.AppliedExisting(buff, SourceActorId, ActorId, 9f, runtime, 1, true);
            runtime.StackCount = 2;
            notifier.AppliedExisting(buff, SourceActorId, ActorId, 8f, runtime, 1, true);
            notifier.Removed(buff, SourceActorId, ActorId, runtime, TraceLifecycleReason.Dispelled);

            Assert.That(recorder.Drafts, Has.Count.EqualTo(4));
            Assert.That(recorder.Drafts[0].Kind, Is.EqualTo(BattleDiagnosticEventKind.BuffAdded));
            Assert.That(recorder.Drafts[3].Kind, Is.EqualTo(BattleDiagnosticEventKind.BuffRemoved));
            Assert.That(recorder.Drafts[0].SourceActorId, Is.EqualTo(SourceActorId));
            Assert.That(recorder.Drafts[0].TargetActorId, Is.EqualTo(ActorId));
            Assert.That(recorder.Drafts[0].ConfigId, Is.EqualTo(BuffId));
            Assert.That(recorder.Drafts[0].ContextId, Is.EqualTo(SourceContextId));

            Assert.That(recorder.Drafts[0].Payload.TryGetBuffLifecycle(out var applied), Is.True);
            Assert.That(recorder.Drafts[1].Payload.TryGetBuffLifecycle(out var refreshed), Is.True);
            Assert.That(recorder.Drafts[2].Payload.TryGetBuffLifecycle(out var stacked), Is.True);
            Assert.That(recorder.Drafts[3].Payload.TryGetBuffLifecycle(out var removed), Is.True);
            Assert.That(new[] { applied.Stage, refreshed.Stage, stacked.Stage, removed.Stage }, Is.EqualTo(new[]
            {
                BattleDiagnosticBuffLifecycleStage.Applied,
                BattleDiagnosticBuffLifecycleStage.Refreshed,
                BattleDiagnosticBuffLifecycleStage.StackChanged,
                BattleDiagnosticBuffLifecycleStage.Removed,
            }));
            Assert.That(applied.DurationMilliseconds, Is.EqualTo(10000));
            Assert.That(applied.RemainingMilliseconds, Is.EqualTo(8500));
            Assert.That(applied.IntervalRemainingMilliseconds, Is.EqualTo(750));
            Assert.That(applied.ModifierBindingCount, Is.EqualTo(1));
            Assert.That(applied.ModifierSourceId, Is.Not.Zero);
            Assert.That(refreshed.PreviousStackCount, Is.EqualTo(1));
            Assert.That(refreshed.StackCount, Is.EqualTo(1));
            Assert.That(stacked.PreviousStackCount, Is.EqualTo(1));
            Assert.That(stacked.StackCount, Is.EqualTo(2));
            Assert.That(removed.RemoveReason, Is.EqualTo((int)TraceLifecycleReason.Dispelled));
        }

        [Test]
        public void RemoveBuffsWithTag_DefaultLegacyPolicy_RemainsDispellable()
        {
            var configs = CreateDispelConfigs(
                CreateDispelBuffDto(BuffId, BuffDispelPolicy.LegacyTag));
            var runtime = CreateRuntime(GetBuff(configs));
            _target.AddBuffs(new List<BuffRuntime> { runtime });
            var service = CreateBuffService(configs);

            var removed = service.RemoveBuffsWithTagImmediate(
                ActorId,
                MobaGameplayTagCatalog.Debuff.Slow,
                SourceActorId,
                removeAll: true,
                TraceLifecycleReason.Dispelled);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(_target.buffs.Active, Is.Empty);
        }

        [Test]
        public void RemoveBuffsWithTag_MixedPolicies_RejectsInvalidCandidatesAndContinuesScanning()
        {
            const int requestedCategory = 7;
            var configs = CreateDispelConfigs(
                CreateDispelBuffDto(BuffId, BuffDispelPolicy.Dispellable, requestedCategory),
                CreateDispelBuffDto(BuffId + 1, BuffDispelPolicy.Undispellable),
                CreateDispelBuffDto(BuffId + 2, BuffDispelPolicy.Dispellable, requestedCategory + 1),
                CreateDispelBuffDto(
                    BuffId + 3,
                    BuffDispelPolicy.Dispellable,
                    requestedCategory,
                    new[] { MobaGameplayTagCatalog.State.ControlImmune }));
            var active = new List<BuffRuntime>
            {
                CreateRuntime(GetBuff(configs, BuffId)),
                CreateRuntime(GetBuff(configs, BuffId + 1)),
                CreateRuntime(GetBuff(configs, BuffId + 2)),
                CreateRuntime(GetBuff(configs, BuffId + 3)),
            };
            _target.AddBuffs(active);
            var diagnostics = new MobaBattleDiagnosticsService();
            var service = CreateBuffService(configs);
            SetPrivateField(
                service,
                "_tags",
                new FixedEffectiveTagQueryService(MobaGameplayTagCatalog.State.ControlImmune));
            SetPrivateField(service, "_diagnostics", diagnostics);

            var removed = service.RemoveBuffsWithTagImmediate(
                ActorId,
                MobaGameplayTagCatalog.Debuff.Slow,
                requestedCategory,
                sourceActorId: 0,
                removeAll: true,
                TraceLifecycleReason.Dispelled);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(active, Has.Count.EqualTo(3));
            Assert.That(active.Exists(runtime => runtime.BuffId == BuffId), Is.False);
            Assert.That(active.Exists(runtime => runtime.BuffId == BuffId + 1), Is.True);
            Assert.That(active.Exists(runtime => runtime.BuffId == BuffId + 2), Is.True);
            Assert.That(active.Exists(runtime => runtime.BuffId == BuffId + 3), Is.True);
            AssertWarning(diagnostics, "buff.dispel.undispellable");
            AssertWarning(diagnostics, "buff.dispel.categoryMismatch");
            AssertWarning(diagnostics, "buff.dispel.immunityBlocked");
        }

        [Test]
        public void RemoveBuffsWithTag_WhenTagQueryServiceIsMissing_FailsClosedWithStableDiagnostic()
        {
            const int requestedCategory = 7;
            var configs = CreateDispelConfigs(
                CreateDispelBuffDto(
                    BuffId,
                    BuffDispelPolicy.Dispellable,
                    requestedCategory,
                    new[] { MobaGameplayTagCatalog.State.ControlImmune }));
            var active = new List<BuffRuntime> { CreateRuntime(GetBuff(configs)) };
            _target.AddBuffs(active);
            var diagnostics = new MobaBattleDiagnosticsService();
            var service = CreateBuffService(configs);
            SetPrivateField(service, "_diagnostics", diagnostics);

            var removed = service.RemoveBuffsWithTagImmediate(
                ActorId,
                MobaGameplayTagCatalog.Debuff.Slow,
                requestedCategory,
                sourceActorId: 0,
                removeAll: true,
                TraceLifecycleReason.Dispelled);

            Assert.That(removed, Is.Zero);
            Assert.That(active, Has.Count.EqualTo(1));
            Assert.That(active[0].BuffId, Is.EqualTo(BuffId));
            AssertWarning(diagnostics, "buff.dispel.tagQueryUnavailable");
        }

        [Test]
        public void ContinuousIntervalHandler_CollectsIntervalPayloadFromBoundRuntime()
        {
            var configs = CreateConfigs();
            var buff = GetBuff(configs);
            var runtime = CreateRuntime(buff);
            runtime.Remaining = 6.25f;
            runtime.IntervalRemainingSeconds = 0.5f;
            runtime.StackCount = 1;
            runtime.ModifierBindings.Add(new AbilityKit.Demo.Moba.Components.BuffModifierBinding());
            var continuous = runtime.Continuous as BuffContinuousRuntime;
            Assert.That(continuous, Is.Not.Null);
            var periodicConfig = continuous.Config as IMobaContinuousPeriodicConfig;
            Assert.That(periodicConfig, Is.Not.Null);
            var recorder = new DiagnosticDraftRecorder();
            var handler = new BuffContinuousIntervalHandler(
                configs,
                null,
                null,
                null,
                null,
                new MobaBattleObservationRecorderAdapter(recorder));
            var executionContext = default(MobaCombatExecutionContext);

            handler.OnInterval(continuous, periodicConfig, in executionContext);

            Assert.That(recorder.Drafts, Has.Count.EqualTo(1));
            var draft = recorder.Drafts[0];
            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.BuffAdded));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Buff));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(SourceActorId));
            Assert.That(draft.TargetActorId, Is.EqualTo(ActorId));
            Assert.That(draft.ConfigId, Is.EqualTo(BuffId));
            Assert.That(draft.RootContextId, Is.EqualTo(SourceContextId));
            Assert.That(draft.ContextId, Is.EqualTo(SourceContextId));
            Assert.That(draft.PayloadVersion, Is.EqualTo(BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion));
            Assert.That(draft.Payload.TryGetBuffLifecycle(out var interval), Is.True);
            Assert.That(interval.Stage, Is.EqualTo(BattleDiagnosticBuffLifecycleStage.Interval));
            Assert.That(interval.StackCount, Is.EqualTo(1));
            Assert.That(interval.PreviousStackCount, Is.EqualTo(1));
            Assert.That(interval.DurationMilliseconds, Is.Zero);
            Assert.That(interval.RemainingMilliseconds, Is.EqualTo(6250));
            Assert.That(interval.IntervalRemainingMilliseconds, Is.EqualTo(500));
            Assert.That(interval.MaxStacks, Is.EqualTo(buff.MaxStacks));
            Assert.That(interval.ModifierBindingCount, Is.EqualTo(1));
            Assert.That(interval.ModifierSourceId, Is.EqualTo(continuous.ModifierSourceId));
            Assert.That(interval.ModifierSourceId, Is.Not.Zero);
            Assert.That(interval.RemoveReason, Is.Zero);
        }

        private MobaBuffService CreateBuffService(
            MobaConfigDatabase configs,
            MobaRuntimeLifecycleHookService hooks = null,
            IMobaBattleExceptionPolicy exceptions = null)
        {
            var service = new MobaBuffService();
            ConfigureBuffService(service, configs, hooks, exceptions);
            return service;
        }

        private void ConfigureBuffService(
            MobaBuffService service,
            MobaConfigDatabase configs,
            MobaRuntimeLifecycleHookService hooks,
            IMobaBattleExceptionPolicy exceptions)
        {
            var lifecycle = CreateLifecycle(
                configs,
                hooks ?? new MobaRuntimeLifecycleHookService(),
                new BuffContinuousBindingService(null, null));
            SetPrivateField(service, "_actors", _lookup);
            SetPrivateField(service, "_configs", configs);
            SetPrivateField(service, "_lifecycle", lifecycle);
            SetPrivateField(service, "_exceptions", exceptions);
        }

        private static void EnqueueApply(MobaBuffService service, int sourceActorId)
        {
            var method = typeof(MobaBuffService).GetMethod(
                "EnqueueApply",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var args = new object[]
            {
                ActorId,
                BuffId,
                sourceActorId,
                0,
                default(BuffOriginContext),
                0L,
                false,
            };
            Assert.That(method.Invoke(service, args), Is.EqualTo(true));
        }

        private static int GetCollectionCount(object target, string fieldName)
        {
            var value = GetPrivateField(target, fieldName);
            var count = value.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(count, Is.Not.Null, fieldName + ".Count");
            return (int)count.GetValue(value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
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

        private static MobaConfigDatabase CreateDispelConfigs(params BuffDTO[] buffs)
        {
            var configs = new MobaConfigDatabase();
            var result = configs.ReloadFromDtoArrays(
                new Dictionary<Type, Array> { [typeof(BuffDTO)] = buffs },
                strict: false);
            Assert.That(result.Succeeded, Is.True, result.Error);
            return configs;
        }

        private static BuffDTO CreateDispelBuffDto(
            int id,
            BuffDispelPolicy policy,
            int category = 0,
            string[] blockedByTags = null)
        {
            return new BuffDTO
            {
                Id = id,
                Name = "dispel-test-" + id,
                DurationMs = 10000,
                MaxStacks = 1,
                OnAddEffects = Array.Empty<int>(),
                OnRemoveEffects = Array.Empty<int>(),
                OnIntervalEffects = Array.Empty<int>(),
                TriggerIds = Array.Empty<int>(),
                TagNames = new[] { MobaGameplayTagCatalog.Debuff.Slow },
                Modifiers = Array.Empty<ContinuousModifierDTO>(),
                DispelPolicy = (int)policy,
                DispelCategory = category,
                DispelBlockedByTagNames = blockedByTags ?? Array.Empty<string>(),
            };
        }

        private static BuffMO GetBuff(MobaConfigDatabase configs, int buffId = BuffId)
        {
            Assert.That(configs.TryGetBuff(buffId, out var buff), Is.True);
            return buff;
        }

        private static void AssertWarning(MobaBattleDiagnosticsService diagnostics, string key)
        {
            var warnings = diagnostics.GetWarningsSnapshot();
            for (var i = 0; i < warnings.Count; i++)
            {
                if (warnings[i].Key == key && warnings[i].Count == 1) return;
            }

            Assert.Fail("Expected diagnostic warning with key " + key + ".");
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

        private sealed class FixedEffectiveTagQueryService : IMobaEffectiveTagQueryService
        {
            private readonly GameplayTagContainer _tags;

            public FixedEffectiveTagQueryService(params string[] tagNames)
            {
                _tags = MobaGameplayTagCatalog.ToContainer(tagNames) ?? new GameplayTagContainer();
            }

            public GameplayTagContainer GetEffectiveTags(int ownerActorId) => _tags;

            public bool CanActivate(int ownerActorId, ContinuousTagRequirements requirements) => true;

            public bool ShouldRemove(int ownerActorId, ContinuousTagRequirements requirements) => false;

            public void MarkDirty(int ownerActorId)
            {
            }

            public bool RemoveActor(int ownerActorId) => true;
        }

        private sealed class DiagnosticDraftRecorder : IMobaBattleDiagnosticEventSink
        {
            public List<MobaBattleDiagnosticEventDraft> Drafts { get; } =
                new List<MobaBattleDiagnosticEventDraft>();

            public bool TryCollect(in MobaBattleDiagnosticEventDraft draft)
            {
                Drafts.Add(draft);
                return true;
            }
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

        private sealed class LifecycleRecorder : IMobaRuntimeLifecycleHook
        {
            public int EndedCount { get; private set; }

            public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent)
            {
                if (lifecycleEvent.Kind == MobaRuntimeLifecycleEventKind.Ended)
                {
                    EndedCount++;
                }
            }
        }

        private sealed class ThrowOnceActivatedHook : IMobaRuntimeLifecycleHook
        {
            private bool _thrown;

            public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent)
            {
                if (_thrown || lifecycleEvent.Kind != MobaRuntimeLifecycleEventKind.Activated) return;
                _thrown = true;
                throw new InvalidOperationException("activation hook failed");
            }
        }

        private sealed class AppendApplyOnceHook : IMobaRuntimeLifecycleHook
        {
            private readonly MobaBuffService _service;
            private bool _appended;

            public AppendApplyOnceHook(MobaBuffService service)
            {
                _service = service;
            }

            public int ActivatedCount { get; private set; }

            public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent)
            {
                if (lifecycleEvent.Kind != MobaRuntimeLifecycleEventKind.Activated) return;
                ActivatedCount++;
                if (_appended) return;
                _appended = true;
                EnqueueApply(_service, SourceActorId + 1);
            }
        }

        private sealed class ExceptionPolicyRecorder : IMobaBattleExceptionPolicy
        {
            public List<Exception> Exceptions { get; } = new List<Exception>();

            public void Handle(Exception exception, in MobaBattleExceptionContext context, MobaBattleExceptionSeverity severity)
            {
                Exceptions.Add(exception);
            }

            public bool TryHandle(Exception exception, in MobaBattleExceptionContext context, MobaBattleExceptionSeverity severity)
            {
                Exceptions.Add(exception);
                return true;
            }
        }

        private sealed class RecordingContinuousManager : IContinuousManager
        {
            private readonly bool _throwOnEnd;

            public RecordingContinuousManager(bool throwOnEnd = false)
            {
                _throwOnEnd = throwOnEnd;
            }

            public List<ContinuousEndReason> EndReasons { get; } = new List<ContinuousEndReason>();
            public int ActiveCount => 0;
            public int TotalCount => 0;

            public bool Register(IContinuous continuous) => true;
            public void Unregister(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.CleanedUp) { }
            public bool TryActivate(IContinuous continuous) => true;
            public bool TryPause(IContinuous continuous) => true;
            public bool TryResume(IContinuous continuous) => true;

            public bool TryEnd(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.Completed)
            {
                EndReasons.Add(reason);
                if (_throwOnEnd) throw new InvalidOperationException("continuous end failed");
                return true;
            }

            public bool TryInterrupt(IContinuous continuous, string reason) => true;
            public IReadOnlyList<IContinuous> GetOwnerContinuous(long ownerId) => Array.Empty<IContinuous>();
            public IReadOnlyList<IContinuous> GetOwnerActiveContinuous(long ownerId) => Array.Empty<IContinuous>();
            public void InterruptAll(long ownerId, string reason) { }
            public void PauseAll(long ownerId) { }
            public void ResumeAll(long ownerId) { }
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
