using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;
using AbilityKit.Demo.Moba.Services.Combat.Transactions;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaHealthCommitTests
    {
        private const int TargetActorId = 801;
        private const float MaxHp = 100f;

        private Contexts _contexts;
        private ActorIdIndex _actorIndex;

        [SetUp]
        public void SetUp()
        {
            _contexts = new Contexts();
            _actorIndex = new ActorIdIndex(_contexts);
        }

        [TearDown]
        public void TearDown()
        {
            _actorIndex.Dispose();
            _contexts.Reset();
        }

        [Test]
        public void TriggerEventMappings_IncludeHealthAndHealPipelinePayloads()
        {
            var registry = new MobaEventSubscriptionRegistry();

            Assert.That(registry.TryGetArgsType(DamagePipelineEvents.HealthCommitted, out var healthType), Is.True);
            Assert.That(healthType, Is.EqualTo(typeof(MobaHealthChangeResult)));
            Assert.That(registry.TryGetArgsType(HealPipelineEvents.BeforeApply, out var beforeHealType), Is.True);
            Assert.That(beforeHealType, Is.EqualTo(typeof(MobaHealRequest)));
            Assert.That(registry.TryGetArgsType(HealPipelineEvents.AfterApply, out var afterHealType), Is.True);
            Assert.That(afterHealType, Is.EqualTo(typeof(MobaHealthChangeResult)));
        }

        [Test]
        public void ActionSnapshot_ObservesExistingRealDamageAndHealCommitEvents()
        {
            var registry = new MobaActorRegistry();
            var eventBus = new EventBus();
            var entities = new MobaEntityManager(eventBus);
            var actor = CreateRegisteredActor(registry, entities, 80f);
            var actors = new MobaActorLookupService(_actorIndex, registry, entities, _contexts);
            var snapshots = new MobaDamageEventSnapshotService(new MobaLogicWorldRunGateService());
            var damage = new MobaDamageService(actors, snapshots, eventBus: eventBus);
            using (var trace = new MobaTraceRegistry())
            using (var store = new MobaActionExecutionSnapshotStore(trace, registry, null, eventBus, 8, () => true))
            {
                var id = trace.CreateRootContext(MobaTraceKind.EffectAction, 301, TargetActorId, TargetActorId);
                var origin = new MobaGameplayOrigin(TargetActorId, TargetActorId, MobaTraceKind.EffectAction,
                    301, id, id, id, id);
                store.OnActionStarted(id, 0, 301, TargetActorId, TargetActorId, 10);
                damage.CommitDamage(TargetActorId, TargetActorId, 0, Fixed64.FromSingle(20f), origin: origin);
                damage.CommitHeal(TargetActorId, TargetActorId, 0, 5f, origin: origin);
                store.OnActionEnded(id, 0, 301, false, false, 10);
                Assert.That(trace.TryGetNodeSnapshot(id, out var node), Is.True);
                var facts = store.Read(((MobaTraceMetadata)node.Metadata).ActionSnapshot);
                Assert.That(facts.SourceBefore.Hp, Is.EqualTo(80f));
                Assert.That(facts.SourceAfter.Hp, Is.EqualTo(65f));
                Assert.That(facts.Commits.Count, Is.EqualTo(2));
                Assert.That(facts.Commits[0].Kind, Is.EqualTo((int)MobaHealthChangeKind.Damage));
                Assert.That(facts.Commits[1].Kind, Is.EqualTo((int)MobaHealthChangeKind.Heal));
                Assert.That(facts.CommitsComplete, Is.True);
                Assert.That(facts.Outcome, Is.EqualTo(BattleDiagnosticActionOutcome.Failed));
                Assert.That(actor.GetMobaAttrs().Hp, Is.EqualTo(65f));
            }
        }

        [Test]
        public void BattlePayloadAccessor_ReadsCalculationAndBoxedHealthResultFields()
        {
            var registry = new PayloadAccessorRegistry();
            var accessor = new MobaBattlePayloadAccessor();
            registry.RegisterIntAccessor<AttackCalcInfo>(accessor, MobaBattlePayloadAccessor.SupportsAttackCalcInfoField);
            registry.RegisterDoubleAccessor<AttackCalcInfo>(accessor, MobaBattlePayloadAccessor.SupportsAttackCalcInfoField);
            registry.RegisterIntAccessor<MobaHealthChangeResult>(accessor, MobaBattlePayloadAccessor.SupportsHealthChangeResultField);
            registry.RegisterDoubleAccessor<MobaHealthChangeResult>(accessor, MobaBattlePayloadAccessor.SupportsHealthChangeResultField);

            var calculation = new AttackCalcInfo(new AttackInfo());
            calculation.RawDamage.BaseValue = 80f;
            calculation.MitigatedDamage.BaseValue = 60f;
            calculation.ShieldAbsorb.BaseValue = 15f;
            calculation.HpDamage.BaseValue = 45f;
            Assert.That(registry.TryGetDouble(
                in calculation,
                MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.RawDamage),
                out var rawDamage), Is.True);
            Assert.That(rawDamage, Is.EqualTo(80d));
            Assert.That(registry.TryGetDouble(
                in calculation,
                MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.HpDamage),
                out var hpDamage), Is.True);
            Assert.That(hpDamage, Is.EqualTo(45d));

            var origin = default(MobaGameplayOrigin);
            object boxed = new MobaHealthChangeResult(
                MobaHealthChangeKind.Heal,
                sourceActorId: 11,
                targetActorId: 12,
                valueType: 2,
                reasonKind: 3,
                reasonParam: 4,
                requestedValue: 50f,
                appliedValue: 20f,
                oldHp: 80f,
                targetHp: 100f,
                targetMaxHp: 100f,
                in origin);
            Assert.That(registry.TryGetDouble(
                in boxed,
                MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.RequestedValue),
                out var requested), Is.True);
            Assert.That(requested, Is.EqualTo(50d));
            Assert.That(registry.TryGetDouble(
                in boxed,
                MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.AppliedValue),
                out var applied), Is.True);
            Assert.That(applied, Is.EqualTo(20d));
            Assert.That(registry.TryGetDouble(
                in boxed,
                MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.OverhealValue),
                out var overheal), Is.True);
            Assert.That(overheal, Is.EqualTo(30d));
            Assert.That(registry.TryIsFieldSupported(
                typeof(MobaHealthChangeResult),
                MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.OverhealValue),
                out var supported), Is.True);
            Assert.That(supported, Is.True);
        }

        [Test]
        public void TriggerPlanContextFactory_PreservesNumericExtensionRegistries()
        {
            var numericDomains = new NumericVarDomainRegistry();
            var numericFunctions = new NumericRpnFunctionRegistry();
            var dependencies = new MobaTriggerPlanRuntimeDependencies(
                services: null,
                eventBus: new EventBus(),
                functions: new FunctionRegistry(),
                actions: new ActionRegistry(),
                payloads: new PayloadAccessorRegistry(),
                numericDomains: numericDomains,
                numericFunctions: numericFunctions);
            var factory = new MobaTriggerPlanExecutionContextFactory(
                dependencies,
                new MobaTriggerPlanEffectResolver(null, null));
            var control = new ExecutionControl();
            control.Reset();

            var context = factory.Create(control);

            Assert.That(context.NumericDomains, Is.SameAs(numericDomains));
            Assert.That(context.NumericFunctions, Is.SameAs(numericFunctions));
        }

        [Test]
        public void CommitDamage_ClampsAtZeroAndPublishesCommittedResultAfterHpMutation()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 30f, eventBus, out var target);
            MobaHealthChangeResult observed = default;
            var eventCount = 0;
            using var subscription = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                result =>
                {
                    eventCount++;
                    observed = result;
                    Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(0f));
                });

            var result = service.CommitDamage(
                attackerActorId: 7,
                targetActorId: TargetActorId,
                damageType: 2,
                value: 50f,
                reasonKind: 3,
                reasonParam: 4);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Kind, Is.EqualTo(MobaHealthChangeKind.Damage));
            Assert.That(result.RequestedValue, Is.EqualTo(50f));
            Assert.That(result.AppliedValue, Is.EqualTo(30f));
            Assert.That(result.OldHp, Is.EqualTo(30f));
            Assert.That(result.TargetHp, Is.EqualTo(0f));
            Assert.That(result.TargetMaxHp, Is.EqualTo(MaxHp));
            Assert.That(result.BecameDead, Is.True);
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(observed.TargetHp, Is.EqualTo(result.TargetHp));
        }

        [Test]
        public void DamageCommit_IsNotPartOfThePublicBusinessApi()
        {
            const BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;

            Assert.That(typeof(MobaDamageService).GetMethod("ApplyDamage", publicInstance), Is.Null);
            Assert.That(typeof(MobaDamageService).GetMethod("CommitDamage", publicInstance), Is.Null);
            Assert.That(typeof(MobaDamageService).GetMethod("ApplyHeal", publicInstance), Is.Null);
            Assert.That(typeof(MobaDamageService).GetMethod("CommitHeal", publicInstance), Is.Null);
            Assert.That(typeof(MobaDamageService).GetMethod("CommitHealCore", publicInstance), Is.Null);
            Assert.That(typeof(DamagePipelineService).GetMethod(nameof(DamagePipelineService.Execute), publicInstance), Is.Not.Null);
            Assert.That(typeof(HealPipelineService).GetMethod(nameof(HealPipelineService.Execute), publicInstance), Is.Not.Null);
        }

        [Test]
        public void DamageStageRegistry_ProtectsCoreOrderAndStablySortsExtensions()
        {
            var registry = new MobaDamageStageRegistry();
            registry.RegisterExtension("extension.first", 1500, new TestDamageStage("damage.test.first"));
            registry.RegisterExtension("extension.second", 1500, new TestDamageStage("damage.test.second"));
            registry.RegisterExtension("extension.before_final", 3500, new TestDamageStage("damage.test.before_final"));

            var stages = registry.GetStages();

            Assert.That(StageIds(stages), Is.EqualTo(new[]
            {
                MobaDamageStageRegistry.BaseStageId,
                "extension.first",
                "extension.second",
                MobaDamageStageRegistry.MitigationStageId,
                MobaDamageStageRegistry.ShieldStageId,
                "extension.before_final",
                MobaDamageStageRegistry.FinalStageId,
            }));
            Assert.That(registry.Validate().Succeeded, Is.True);
        }

        [Test]
        public void DamageStageRegistry_RejectsDuplicatesIllegalOrdersAndLateMutation()
        {
            var duplicateId = new MobaDamageStageRegistry();
            duplicateId.RegisterExtension("extension.same", 1500, new TestDamageStage("damage.test.one"));
            Assert.Throws<InvalidOperationException>(() =>
                duplicateId.RegisterExtension("extension.same", 1600, new TestDamageStage("damage.test.two")));
            Assert.Throws<InvalidOperationException>(() =>
                duplicateId.RegisterExtension("extension.other", 1600, new TestDamageStage("damage.test.one")));

            var illegalOrder = new MobaDamageStageRegistry();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                illegalOrder.RegisterExtension("extension.after_final", MobaDamageStageOrders.Final + 1, new TestDamageStage("damage.test.after_final")));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                illegalOrder.RegisterExtension("extension.core_collision", MobaDamageStageOrders.Mitigation, new TestDamageStage("damage.test.core_collision")));

            var frozen = new MobaDamageStageRegistry();
            frozen.GetStages();
            Assert.Throws<InvalidOperationException>(() =>
                frozen.RegisterExtension("extension.late", 1500, new TestDamageStage("damage.test.late")));
        }

        [Test]
        public void CommitHeal_ClampsAtMaxHpAndPublishesHealResult()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 80f, eventBus, out var target);
            var eventCount = 0;
            var pipelineEventCount = 0;
            using var subscription = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                result =>
                {
                    eventCount++;
                    Assert.That(result.Kind, Is.EqualTo(MobaHealthChangeKind.Heal));
                    Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(MaxHp));
                });
            using var before = eventBus.Subscribe(
                new EventKey<MobaHealRequest>(TriggeringIdUtil.GetEventEid(HealPipelineEvents.BeforeApply)),
                _ => pipelineEventCount++);
            using var after = eventBus.Subscribe(
                new EventKey<MobaHealthChangeResult>(TriggeringIdUtil.GetEventEid(HealPipelineEvents.AfterApply)),
                _ => pipelineEventCount++);

            var result = service.CommitHeal(
                healerActorId: 7,
                targetActorId: TargetActorId,
                healType: 5,
                value: 50f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Kind, Is.EqualTo(MobaHealthChangeKind.Heal));
            Assert.That(result.AppliedValue, Is.EqualTo(20f));
            Assert.That(result.OldHp, Is.EqualTo(80f));
            Assert.That(result.TargetHp, Is.EqualTo(MaxHp));
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(pipelineEventCount, Is.EqualTo(2));
        }

        [Test]
        public void HealPipeline_ValidatesPublishesAndCommitsThroughOneEntry()
        {
            var eventBus = new EventBus();
            var commitPort = CreateService(initialHp: 80f, eventBus, out var target);
            var pipeline = new HealPipelineService(commitPort, eventBus);
            var events = new List<string>();
            using var before = eventBus.Subscribe(
                new EventKey<MobaHealRequest>(TriggeringIdUtil.GetEventEid(HealPipelineEvents.BeforeApply)),
                _ => events.Add("before"));
            using var committed = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                _ => events.Add("committed"));
            using var after = eventBus.Subscribe(
                new EventKey<MobaHealthChangeResult>(TriggeringIdUtil.GetEventEid(HealPipelineEvents.AfterApply)),
                _ => events.Add("after"));
            var request = new MobaHealRequest(7, TargetActorId, 5, 50f, reasonKind: 3, reasonParam: 4);

            var result = pipeline.Execute(in request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.RequestedValue, Is.EqualTo(50f));
            Assert.That(result.AppliedValue, Is.EqualTo(20f));
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(MaxHp));
            Assert.That(events, Is.EqualTo(new[] { "before", "committed", "after" }));
        }

        [Test]
        public void HealPipeline_InvalidValueDoesNotPublishOrCommit()
        {
            var eventBus = new EventBus();
            var commitPort = CreateService(initialHp: 80f, eventBus, out var target);
            var pipeline = new HealPipelineService(commitPort, eventBus);
            var eventCount = 0;
            using var before = eventBus.Subscribe(
                new EventKey<MobaHealRequest>(TriggeringIdUtil.GetEventEid(HealPipelineEvents.BeforeApply)),
                _ => eventCount++);
            using var committed = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                _ => eventCount++);
            var request = new MobaHealRequest(7, TargetActorId, 5, float.NaN);

            var result = pipeline.Execute(in request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(80f));
            Assert.That(eventCount, Is.Zero);
        }

        [Test]
        public void HealTransaction_InterceptorCanModifyValueBeforeCommit()
        {
            var transactions = new MobaCombatTransactionPipeline();
            transactions.Register(new DelegateTransactionInterceptor<MobaHealTransaction>(
                (transaction, stage, _) =>
                {
                    if (stage == MobaCombatTransactionStage.Modify) transaction.SetValue(5f);
                }));
            var service = CreateService(initialHp: 80f, new EventBus(), out var target, transactions);

            var result = service.CommitHeal(7, TargetActorId, 5, 50f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.RequestedValue, Is.EqualTo(5f));
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(85f));
        }

        [Test]
        public void HealTransaction_InterceptorCanCancelBeforeCommit()
        {
            var transactions = new MobaCombatTransactionPipeline();
            transactions.Register(new DelegateTransactionInterceptor<MobaHealTransaction>(
                (transaction, stage, _) =>
                {
                    if (stage == MobaCombatTransactionStage.Validate) transaction.Cancel("blocked by test rule");
                }));
            var service = CreateService(initialHp: 80f, new EventBus(), out var target, transactions);

            var result = service.CommitHeal(7, TargetActorId, 5, 50f);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(80f));
        }

        [Test]
        public void CombatTransaction_CancelledValidationRunsRollbackStage()
        {
            var stages = new List<MobaCombatTransactionStage>();
            var pipeline = new MobaCombatTransactionPipeline();
            pipeline.Register(new DelegateTransactionInterceptor<TestRevertibleTransaction>(
                (transaction, stage, _) =>
                {
                    stages.Add(stage);
                    if (stage == MobaCombatTransactionStage.Validate) transaction.Cancel("validation failed");
                }));
            var transaction = new TestRevertibleTransaction();

            var result = pipeline.TryExecute(transaction, _ => true, _ => true);

            Assert.That(result, Is.False);
            Assert.That(transaction.RollbackCount, Is.EqualTo(1));
            Assert.That(stages, Is.EqualTo(new[]
            {
                MobaCombatTransactionStage.Prepare,
                MobaCombatTransactionStage.Modify,
                MobaCombatTransactionStage.Validate,
                MobaCombatTransactionStage.Rollback,
            }));
        }

        [Test]
        public void EffectMagnitude_AttributeRealtimeSnapshotAndTimeDecayUseRuntimeContext()
        {
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var actor = CreateRegisteredActor(registry, entities, initialHp: 80f);
            actor.GetMobaAttrs().SetBase(BattleAttributeType.PHYSICS_ATTACK, 40f);
            var actors = new MobaActorLookupService(_actorIndex, registry, entities, _contexts);
            var frameTime = new FrameTime();
            frameTime.AlignTo(new FrameIndex(20), 0.1f);
            var services = new TestWorldResolver()
                .Add<MobaActorLookupService>(actors)
                .Add<IFrameTime>(frameTime);
            var runtimeBlackboard = new MobaSkillRuntimeBlackboard();
            var boards = new MobaSkillRuntimeBlackboardResolver(runtimeBlackboard, 101L, TargetActorId, 301L);
            var execCtx = new ExecCtx<IWorldResolver>(services, null, null, null, boards, null, null, null, null, default, null);
            var executionContext = new MobaCombatExecutionContext(null, default, default, default, default, frame: 10);
            var fixedSource = MagnitudeSource.Fixed(10f);
            var attributeSource = MagnitudeSource.Attribute(
                ModifierKey.FromPacked((uint)BattleAttributeType.PHYSICS_ATTACK), 0.5f);
            var realtime = new MobaEffectMagnitudeSpec(
                in fixedSource, in attributeSource,
                MobaEffectMagnitudeCombine.Add,
                MobaEffectSourceRole.AttributionActor,
                MobaEffectEvaluationPolicy.Realtime);

            Assert.That(MobaEffectMagnitudeResolver.TryEvaluate(
                in realtime, in executionContext, in execCtx,
                TargetActorId, TargetActorId, TargetActorId, default,
                out var realtimeValue, out _, out var failure), Is.True, failure);
            Assert.That(realtimeValue, Is.EqualTo(30f));

            var captureTarget = new BlackboardWriteTarget(
                MobaSkillRuntimeTriggerBoards.Effect, 92001, BlackboardKeyType.Double, "effect");
            var snapshot = new MobaEffectMagnitudeSpec(
                in fixedSource, in attributeSource,
                MobaEffectMagnitudeCombine.Add,
                MobaEffectSourceRole.AttributionActor,
                MobaEffectEvaluationPolicy.Snapshot,
                captureTarget);
            Assert.That(BlackboardMutation.TrySetNumeric(boards, in captureTarget, 0d, out failure), Is.True, failure);
            Assert.That(MobaEffectMagnitudeResolver.TryEvaluate(
                in snapshot, in executionContext, in execCtx,
                TargetActorId, TargetActorId, TargetActorId, default,
                out var capturedValue, out _, out failure), Is.True, failure);
            actor.GetMobaAttrs().SetBase(BattleAttributeType.PHYSICS_ATTACK, 100f);
            Assert.That(MobaEffectMagnitudeResolver.TryEvaluate(
                in snapshot, in executionContext, in execCtx,
                TargetActorId, TargetActorId, TargetActorId, default,
                out var reusedValue, out _, out failure), Is.True, failure);
            Assert.That(capturedValue, Is.EqualTo(30f));
            Assert.That(reusedValue, Is.EqualTo(30f));

            var decaySource = MagnitudeSource.TimeDecay(100f, 2f);
            var decay = new MobaEffectMagnitudeSpec(in decaySource, default);
            Assert.That(MobaEffectMagnitudeResolver.TryEvaluate(
                in decay, in executionContext, in execCtx,
                0, 0, 0, default,
                out var decayValue, out _, out failure), Is.True, failure);
            Assert.That(decayValue, Is.EqualTo(50f).Within(0.001f));

            actor.AddOwnerLink(701, 702);
            var ownerSpec = new MobaEffectMagnitudeSpec(
                in fixedSource, default,
                sourceRole: MobaEffectSourceRole.Owner,
                evaluationPolicy: MobaEffectEvaluationPolicy.Snapshot);
            Assert.That(MobaEffectMagnitudeResolver.TryEvaluate(
                in ownerSpec, in executionContext, in execCtx,
                0, TargetActorId, 0, default,
                out _, out var ownerCapture, out failure), Is.True, failure);
            var rootOwnerSpec = new MobaEffectMagnitudeSpec(
                in fixedSource, default,
                sourceRole: MobaEffectSourceRole.RootOwner,
                evaluationPolicy: MobaEffectEvaluationPolicy.Snapshot);
            Assert.That(MobaEffectMagnitudeResolver.TryEvaluate(
                in rootOwnerSpec, in executionContext, in execCtx,
                0, TargetActorId, 0, default,
                out _, out var rootOwnerCapture, out failure), Is.True, failure);
            Assert.That(ownerCapture.SourceActorId, Is.EqualTo(701));
            Assert.That(rootOwnerCapture.SourceActorId, Is.EqualTo(702));

            var targetCaptureTarget = new BlackboardWriteTarget(
                MobaSkillRuntimeTriggerBoards.Target, 92002, BlackboardKeyType.Double, "target");
            var targetSnapshot = new MobaEffectMagnitudeSpec(
                in fixedSource, default,
                evaluationPolicy: MobaEffectEvaluationPolicy.Snapshot,
                captureTarget: targetCaptureTarget);
            Assert.That(MobaEffectMagnitudeResolver.TryEvaluate(
                in targetSnapshot, in executionContext, in execCtx,
                0, TargetActorId, 802, default,
                out _, out _, out failure), Is.True, failure);
            var secondTargetBoards = new MobaSkillRuntimeBlackboardResolver(runtimeBlackboard, 101L, 802, 301L);
            Assert.That(secondTargetBoards.TryResolve(MobaSkillRuntimeTriggerBoards.Target, out var secondTargetBoard), Is.True);
            Assert.That(secondTargetBoard.TryGetDouble(targetCaptureTarget.KeyId, out var secondTargetCapture), Is.True);
            Assert.That(secondTargetCapture, Is.EqualTo(10d));

            registry.Dispose();
            entities.Dispose();
        }

        [Test]
        public void DamageTransaction_ModifiesDamageAndPreservesStagesAndShieldAbsorption()
        {
            var eventBus = new EventBus();
            var transactions = new MobaCombatTransactionPipeline();
            transactions.Register(new DelegateTransactionInterceptor<MobaDamageTransaction>(
                (transaction, stage, _) =>
                {
                    if (stage == MobaCombatTransactionStage.Modify) transaction.SetBaseDamage(20f);
                }));
            var pipeline = CreateDamagePipeline(80f, eventBus, transactions, out var target, out var shields);
            shields.AddShield(TargetActorId, new ShieldLayer
            {
                ShieldId = 101,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(5f),
                MaxValue = Fixed64.FromSingle(5f),
                InitialValue = Fixed64.FromSingle(5f),
                AbsorbRatio = Fixed64.One,
                StackingPolicy = ShieldStackingPolicy.Independent,
                ConsumePolicy = ShieldConsumePolicy.PriorityThenOldest,
            });
            var stages = new List<string>();
            using var beforeCalc = eventBus.Subscribe(
                new EventKey<AttackInfo>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.BeforeCalc)),
                _ => stages.Add("before_calc"));
            using var afterApply = eventBus.Subscribe(
                new EventKey<DamageResult>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply)),
                _ => stages.Add("after_apply"));
            var attack = new AttackInfo
            {
                AttackerActorId = 7,
                TargetActorId = TargetActorId,
                DamageType = DamageType.Physical,
            };
            attack.BaseDamage.BaseValue = 50f;

            var result = pipeline.Execute(attack);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value, Is.EqualTo(15f));
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(65f));
            Assert.That(shields.GetTotalRemaining(TargetActorId), Is.Zero);
            Assert.That(stages, Is.EqualTo(new[] { "before_calc", "after_apply" }));
        }

        [TestCase(0, BattleDiagnosticActionDamageOutcome.NoHpDamage, BattleDiagnosticDamageStage.Completed)]
        [TestCase(1, BattleDiagnosticActionDamageOutcome.FullyAbsorbed, BattleDiagnosticDamageStage.Completed)]
        [TestCase(2, BattleDiagnosticActionDamageOutcome.Applied, BattleDiagnosticDamageStage.Completed)]
        [TestCase(3, BattleDiagnosticActionDamageOutcome.Rejected, BattleDiagnosticDamageStage.TargetMissing)]
        [TestCase(4, BattleDiagnosticActionDamageOutcome.Rejected, BattleDiagnosticDamageStage.TransactionRejected)]
        [TestCase(5, BattleDiagnosticActionDamageOutcome.Rejected, BattleDiagnosticDamageStage.InvalidRequest)]
        [TestCase(6, BattleDiagnosticActionDamageOutcome.Rejected, BattleDiagnosticDamageStage.HealthCommitRejected)]
        [TestCase(7, BattleDiagnosticActionDamageOutcome.Rejected, BattleDiagnosticDamageStage.ShieldCommitRejected)]
        public void ActionSnapshot_ObservesRealDamageTerminalBranchesWithoutInferringHpCommit(int scenario,
            BattleDiagnosticActionDamageOutcome expected, BattleDiagnosticDamageStage stage)
        {
            var eventBus = new EventBus();
            var collector = new MobaBattleDiagnosticEventCollector(new BattleDiagnosticSessionScope("damage-facts", "world", 1), frameProvider: () => 10);
            var transactions = new MobaCombatTransactionPipeline();
            if (scenario == 4)
                transactions.Register(new DelegateTransactionInterceptor<MobaDamageTransaction>((transaction, current, _) =>
                { if (current == MobaCombatTransactionStage.Validate) transaction.Cancel("blocked by test rule"); }));
            var pipeline = CreateDamagePipeline(scenario == 6 ? 0f : 80f, eventBus, transactions,
                out var target, out var shields, collector);
            if (scenario == 1 || scenario == 2 || scenario == 7)
                shields.AddShield(TargetActorId, new ShieldLayer
                {
                    ShieldId = 101, SourceActorId = 7, CurrentValue = Fixed64.FromSingle(scenario == 2 ? 5f : 20f),
                    MaxValue = Fixed64.FromSingle(20f), InitialValue = Fixed64.FromSingle(20f), AbsorbRatio = Fixed64.One,
                    StackingPolicy = ShieldStackingPolicy.Independent, ConsumePolicy = ShieldConsumePolicy.PriorityThenOldest
                });
            using var beforeApply = eventBus.Subscribe(new EventKey<AttackCalcInfo>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.BeforeApply)),
                _ => { if (scenario == 7) shields.RemoveActor(TargetActorId); });
            using var trace = new MobaTraceRegistry();
            using var store = new MobaActionExecutionSnapshotStore(trace, null, collector, eventBus);
            var id = trace.CreateRootContext(MobaTraceKind.EffectAction, 301, 7, TargetActorId);
            var attack = new AttackInfo
            {
                AttackerActorId = 7, TargetActorId = scenario == 3 ? TargetActorId + 1 : scenario == 5 ? 0 : TargetActorId,
                DamageType = DamageType.Physical,
                Origin = new MobaGameplayOrigin(7, TargetActorId, MobaTraceKind.EffectAction, 301, id, id, id, id)
            };
            attack.BaseDamage.BaseValue = scenario == 0 ? 0f : 20f;
            store.OnActionStarted(id, 0, 301, 7, TargetActorId, 10);
            var result = pipeline.Execute(attack);
            store.OnActionEnded(id, 0, 301, true, false, 10);
            Assert.That(trace.TryGetNodeSnapshot(id, out var node), Is.True);
            var facts = store.Read(((MobaTraceMetadata)node.Metadata).ActionSnapshot);
            Assert.That(facts.DamageResults.Count, Is.EqualTo(1));
            Assert.That(facts.DamageResults[0].Outcome, Is.EqualTo(expected));
            Assert.That(facts.DamageResults[0].Calculation.Stage, Is.EqualTo(stage));
            Assert.That(facts.DamageCoverageContinuous, Is.True);
            Assert.That(facts.Commits.Count, Is.EqualTo(scenario == 2 ? 1 : 0));
            Assert.That(facts.Outcome, Is.EqualTo(BattleDiagnosticActionOutcome.Completed));
            if (scenario == 4) Assert.That(facts.DamageResults[0].Detail, Does.Contain("blocked by test rule"));
            if (scenario <= 2)
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(scenario == 2 ? 65f : 80f));
            }
            else Assert.That(result, Is.Null);
            if (scenario == 1) Assert.That(shields.GetTotalRemaining(TargetActorId), Is.Zero);
        }

        [TestCase(0, BattleDiagnosticDamageStage.ExecutionFailed, 0)]
        [TestCase(1, BattleDiagnosticDamageStage.ExecutionFailed, 1)]
        [TestCase(2, BattleDiagnosticDamageStage.PostCommitNotificationFailed, 1)]
        public void ActionSnapshot_ExecutionExceptionsPreservePartialOrCompletedCommitFacts(int scenario,
            BattleDiagnosticDamageStage expected, int hpCommitCount)
        {
            var eventBus = new EventBus();
            var collector = new MobaBattleDiagnosticEventCollector(new BattleDiagnosticSessionScope("damage-exception", "world", 1), frameProvider: () => 10);
            var transactions = new MobaCombatTransactionPipeline();
            if (scenario == 2)
                transactions.Register(new DelegateTransactionInterceptor<MobaDamageTransaction>((transaction, current, context) =>
                { if (current == MobaCombatTransactionStage.Complete) throw new InvalidOperationException("complete failure"); }));
            var pipeline = CreateDamagePipeline(80f, eventBus, transactions, out var target, out _, collector);
            using var beforeApply = eventBus.Subscribe(new EventKey<AttackCalcInfo>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.BeforeApply)),
                _ => { if (scenario == 0) throw new InvalidOperationException("before apply failure"); });
            using var afterApply = eventBus.Subscribe(new EventKey<DamageResult>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply)),
                _ => { if (scenario == 1) throw new InvalidOperationException("after apply failure"); });
            using var trace = new MobaTraceRegistry();
            using var store = new MobaActionExecutionSnapshotStore(trace, null, collector, eventBus);
            var id = trace.CreateRootContext(MobaTraceKind.EffectAction, 301, 7, TargetActorId);
            var attack = new AttackInfo { AttackerActorId = 7, TargetActorId = TargetActorId, DamageType = DamageType.Physical,
                Origin = new MobaGameplayOrigin(7, TargetActorId, MobaTraceKind.EffectAction, 301, id, id, id, id) };
            attack.BaseDamage.BaseValue = 20f;
            store.OnActionStarted(id, 0, 301, 7, TargetActorId, 10);
            Assert.Throws<InvalidOperationException>(() => pipeline.Execute(attack));
            store.OnActionEnded(id, 0, 301, false, false, 10);
            Assert.That(trace.TryGetNodeSnapshot(id, out var node), Is.True);
            var facts = store.Read(((MobaTraceMetadata)node.Metadata).ActionSnapshot);
            Assert.That(facts.Commits.Count, Is.EqualTo(hpCommitCount));
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(hpCommitCount == 0 ? 80f : 60f));
            Assert.That(facts.DamageResults[facts.DamageResults.Count - 1].Calculation.Stage, Is.EqualTo(expected));
            Assert.That(facts.DamageResults[facts.DamageResults.Count - 1].Calculation.HasCalculation, Is.False);
            Assert.That(facts.DamageResults.Count, Is.EqualTo(scenario == 2 ? 2 : 1));
            Assert.That(facts.Outcome, Is.EqualTo(BattleDiagnosticActionOutcome.Failed));
        }

        [Test]
        public void DamageTransaction_ValidationCancellationDoesNotEnterDamagePipeline()
        {
            var eventBus = new EventBus();
            var transactions = new MobaCombatTransactionPipeline();
            var stages = new List<MobaCombatTransactionStage>();
            transactions.Register(new DelegateTransactionInterceptor<MobaDamageTransaction>(
                (transaction, stage, _) =>
                {
                    stages.Add(stage);
                    if (stage == MobaCombatTransactionStage.Validate) transaction.Cancel("blocked by test rule");
                }));
            var pipeline = CreateDamagePipeline(80f, eventBus, transactions, out var target, out _);
            var eventCount = 0;
            using var attackCreated = eventBus.Subscribe(
                new EventKey<AttackInfo>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AttackCreated)),
                _ => eventCount++);
            var attack = new AttackInfo
            {
                AttackerActorId = 7,
                TargetActorId = TargetActorId,
                DamageType = DamageType.Physical,
            };
            attack.BaseDamage.BaseValue = 50f;

            var result = pipeline.Execute(attack);

            Assert.That(result, Is.Null);
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(80f));
            Assert.That(eventCount, Is.Zero);
            Assert.That(stages, Is.EqualTo(new[]
            {
                MobaCombatTransactionStage.Prepare,
                MobaCombatTransactionStage.Modify,
                MobaCombatTransactionStage.Validate,
                MobaCombatTransactionStage.Rollback,
            }));
        }

        [Test]
        public void CommitHeal_DeadTargetWithoutPermission_DoesNotMutateOrPublish()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 0f, eventBus, out var target);
            var eventCount = 0;
            using var subscription = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                _ => eventCount++);

            var result = service.CommitHeal(
                healerActorId: 7,
                targetActorId: TargetActorId,
                healType: 5,
                value: 40f);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(0f));
            Assert.That(eventCount, Is.Zero);
        }

        [Test]
        public void CommitHeal_DeadTargetWithPermission_CommitsRespawnResult()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 0f, eventBus, out var target);
            MobaHealthChangeResult observed = default;
            using var subscription = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                result => observed = result);

            var result = service.CommitHeal(
                healerActorId: 7,
                targetActorId: TargetActorId,
                healType: 5,
                value: 40f,
                allowDeadTarget: true);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Kind, Is.EqualTo(MobaHealthChangeKind.Respawn));
            Assert.That(result.AppliedValue, Is.EqualTo(40f));
            Assert.That(result.OldHp, Is.EqualTo(0f));
            Assert.That(result.TargetHp, Is.EqualTo(40f));
            Assert.That(result.BecameDead, Is.False);
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(40f));
            Assert.That(observed.Kind, Is.EqualTo(MobaHealthChangeKind.Respawn));
        }

        [Test]
        public void CommitDamage_SubscriberFailurePropagatesAfterHpMutation()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 60f, eventBus, out var target);
            using var subscription = eventBus.Subscribe<MobaHealthChangeResult>(
                CreateHealthCommittedKey(),
                _ => throw new InvalidOperationException("health subscriber failed"));

            var error = Assert.Throws<InvalidOperationException>(() => service.CommitDamage(
                attackerActorId: 7,
                targetActorId: TargetActorId,
                damageType: 2,
                value: 25f));

            Assert.That(error.Message, Is.EqualTo("health subscriber failed"));
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(35f));
        }

        [Test]
        public void TryRespawn_HealthSubscriberFailure_StillResetsDeathStateAfterCommit()
        {
            var eventBus = new EventBus();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(eventBus);
            var target = CreateRegisteredActor(registry, entities, initialHp: 30f);
            var actors = new MobaActorLookupService(_actorIndex, registry, entities, _contexts);
            var rules = new MobaCombatRulesService(actors);
            var snapshots = new MobaDamageEventSnapshotService(new MobaLogicWorldRunGateService());
            var damage = new MobaDamageService(actors, snapshots, rules, eventBus: eventBus);
            var deaths = new MobaUnitDeathSubscriber(eventBus, entities);
            var lifecycle = new MobaUnitLifecycleService(actors, entities, deaths, damage);
            var dieCount = 0;
            using var dieSubscription = eventBus.Subscribe(
                CreateUnitDieKey(),
                _ => dieCount++);

            damage.CommitDamage(7, TargetActorId, 2, 30f);
            Assert.That(dieCount, Is.EqualTo(1));

            using (eventBus.Subscribe<MobaHealthChangeResult>(
                       CreateHealthCommittedKey(),
                       result =>
                       {
                           if (result.Kind == MobaHealthChangeKind.Respawn)
                           {
                               throw new InvalidOperationException("respawn health subscriber failed");
                           }
                       }))
            {
                var error = Assert.Throws<InvalidOperationException>(() =>
                    lifecycle.TryRespawn(TargetActorId, healthRatio: 0.5f));
                Assert.That(error.Message, Is.EqualTo("respawn health subscriber failed"));
            }

            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(50f));
            damage.CommitDamage(7, TargetActorId, 2, 50f);
            Assert.That(dieCount, Is.EqualTo(2));

            lifecycle.Dispose();
            deaths.Dispose();
            registry.Dispose();
            entities.Dispose();
        }

        private MobaDamageService CreateService(
            float initialHp,
            EventBus eventBus,
            out ActorEntity target,
            MobaCombatTransactionPipeline transactions = null)
        {
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            target = CreateRegisteredActor(registry, entities, initialHp);
            var actors = new MobaActorLookupService(_actorIndex, registry, entities, _contexts);
            var rules = new MobaCombatRulesService(actors);
            var snapshots = new MobaDamageEventSnapshotService(new MobaLogicWorldRunGateService());
            return new MobaDamageService(actors, snapshots, rules, eventBus: eventBus, transactions: transactions);
        }

        private DamagePipelineService CreateDamagePipeline(
            float initialHp,
            EventBus eventBus,
            MobaCombatTransactionPipeline transactions,
            out ActorEntity target,
            out MobaShieldService shields,
            IMobaBattleDiagnosticEventSink collector = null)
        {
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            target = CreateRegisteredActor(registry, entities, initialHp);
            var actors = new MobaActorLookupService(_actorIndex, registry, entities, _contexts);
            var rules = new MobaCombatRulesService(actors);
            var snapshots = new MobaDamageEventSnapshotService(new MobaLogicWorldRunGateService());
            var damage = new MobaDamageService(actors, snapshots, rules, eventBus: eventBus, transactions: transactions);
            shields = new MobaShieldService();
            return new DamagePipelineService(actors, damage, eventBus, shields: shields, eventCollector: collector, transactions: transactions);
        }

        private sealed class DelegateTransactionInterceptor<TTransaction> : IMobaCombatTransactionInterceptor<TTransaction>
            where TTransaction : class, IMobaCombatTransaction
        {
            private readonly Action<TTransaction, MobaCombatTransactionStage, MobaCombatTransactionContext> _callback;

            public DelegateTransactionInterceptor(Action<TTransaction, MobaCombatTransactionStage, MobaCombatTransactionContext> callback)
                => _callback = callback;

            public void OnStage(TTransaction transaction, MobaCombatTransactionStage stage, in MobaCombatTransactionContext context)
                => _callback(transaction, stage, context);
        }

        private sealed class TestRevertibleTransaction : MobaCombatTransactionBase, IMobaRevertibleCombatTransaction
        {
            public TestRevertibleTransaction() : base(0L) { }
            public int RollbackCount { get; private set; }
            public void Rollback() => RollbackCount++;
        }

        private sealed class TestWorldResolver : IWorldResolver
        {
            private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

            public TestWorldResolver Add<T>(T service)
            {
                _services[typeof(T)] = service;
                return this;
            }

            public object Resolve(Type serviceType) => _services[serviceType];
            public T Resolve<T>() => (T)Resolve(typeof(T));
            public bool TryResolve(Type serviceType, out object instance) => _services.TryGetValue(serviceType, out instance);
            public bool TryResolve<T>(out T instance)
            {
                if (_services.TryGetValue(typeof(T), out var raw) && raw is T typed)
                {
                    instance = typed;
                    return true;
                }
                instance = default;
                return false;
            }
        }

        private ActorEntity CreateRegisteredActor(
            MobaActorRegistry registry,
            MobaEntityManager entities,
            float initialHp)
        {
            var entity = _contexts.actor.CreateEntity();
            var attributeContext = new AttributeContext();
            var attributeGroup = attributeContext.GetOrCreateGroup("health-commit-test");
            attributeGroup.SetBase(MobaAttributeIds.MAX_HP, MaxHp);
            var resources = new ResourceContainer
            {
                Map = new Dictionary<ResourceType, ResourceState>
                {
                    [ResourceType.Hp] = new ResourceState
                    {
                        Current = Fixed64.FromSingle(initialHp),
                        LastMax = Fixed64.FromSingle(MaxHp),
                        MaxAttribute = MobaAttributeIds.MAX_HP,
                    },
                },
            };
            var spec = CreateSpec();

            entity.AddActorId(TargetActorId);
            entity.AddTeam(spec.Info.Team);
            entity.AddEntityMainType(spec.Info.MainType);
            entity.AddUnitSubType(spec.Info.UnitSubType);
            entity.AddOwnerPlayerId(spec.Info.OwnerPlayer);
            entity.AddAttributeGroup(attributeGroup, attributeContext);
            entity.AddResourceContainer(resources, true);
            new MobaActorSpawnRegistrar(registry, entities).Register(
                entity,
                in spec,
                registerActor: true,
                registerEntityManager: true,
                registerEntityManagerFromEntity: true);
            return entity;
        }

        private static EventKey<MobaHealthChangeResult> CreateHealthCommittedKey()
        {
            return new EventKey<MobaHealthChangeResult>(
                TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted));
        }

        private static EventKey<UnitDieEventPayload> CreateUnitDieKey()
        {
            return new EventKey<UnitDieEventPayload>(
                TriggeringIdUtil.GetEventEid(MobaUnitTriggering.Events.Die));
        }

        private static MobaActorBuildSpec CreateSpec()
        {
            var transform = Transform3.Identity;
            var info = new MobaEntityInfo(
                TargetActorId,
                MobaEntityKind.Hero,
                in transform,
                (Team)1,
                EntityMainType.Unit,
                UnitSubType.Hero,
                new PlayerId("health-commit-test"),
                templateId: 1001);
            return new MobaActorBuildSpec(
                in info,
                MobaActorBuildSourceKind.PlayerLoadout,
                sourceId: 1001,
                ownerActorId: 0);
        }

        private static string[] StageIds(IReadOnlyList<MobaDamageStageDescriptor> stages)
        {
            var ids = new string[stages.Count];
            for (var i = 0; i < stages.Count; i++) ids[i] = stages[i].Id;
            return ids;
        }

        private sealed class TestDamageStage : IMobaDamagePipelineStage
        {
            public TestDamageStage(string eventId)
            {
                EventId = eventId;
            }

            public string EventId { get; }

            public void Execute(AttackCalcInfo calc)
            {
            }
        }
    }
}
