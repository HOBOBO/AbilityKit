using System;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Combat.Transactions;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(DamagePipelineService))]
    public sealed class DamagePipelineService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly MobaDamageService _damage;
        private readonly MobaShieldService _shields;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly IMobaDamageStageProvider _stageProvider;
        private System.Collections.Generic.IReadOnlyList<MobaDamageStageDescriptor> _validatedStages;
        private readonly MobaCombatTransactionPipeline _transactions;
        [WorldInject] private MobaExecutionContextRegistry _executionContexts = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private MobaCombatActivityService _combatActivity = null;
 
        private readonly IMobaBattleDiagnosticsService _diagnostics;
        private readonly IMobaBattleDiagnosticEventSink _eventCollector;

        public DamagePipelineService(
            MobaActorLookupService actors,
            MobaDamageService damage,
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
            MobaDamageMitigationService mitigation = null,
            MobaShieldService shields = null,
            IMobaBattleDiagnosticsService diagnostics = null,
            IMobaBattleDiagnosticEventSink eventCollector = null,
            IMobaDamageStageProvider stageProvider = null,
            MobaCombatTransactionPipeline transactions = null)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _shields = shields;
            _eventBus = eventBus;
            _diagnostics = diagnostics;
            _eventCollector = eventCollector;
            _stageProvider = stageProvider ?? new MobaDamageStageRegistry(mitigation, shields);
            _transactions = transactions;
        }

        public DamageResult Execute(AttackInfo attack)
        {
            if (attack == null) return null;
            var transaction = new MobaDamageTransaction(attack);
            DamageResult result = null;
            var coreEntered = false;
            try
            {
                var committed = _transactions == null
                    ? IsValid(transaction) && Commit(transaction)
                    : _transactions.TryExecute(transaction, IsValid, Commit);
                if (!committed && !coreEntered)
                    TryCollectDamageCalculation(transaction.Attack, null,
                        transaction.IsCancelled ? BattleDiagnosticDamageStage.TransactionRejected : BattleDiagnosticDamageStage.InvalidRequest,
                        0f, transaction.FailureReason);
                return committed ? result : null;
            }
            catch (Exception ex)
            {
                TryCollectDamageCalculation(transaction.Attack, null,
                    result != null ? BattleDiagnosticDamageStage.PostCommitNotificationFailed : BattleDiagnosticDamageStage.ExecutionFailed,
                    0f, ex.GetType().Name);
                throw;
            }

            bool Commit(MobaDamageTransaction current)
            {
                coreEntered = true;
                result = ExecuteCore(current.Attack);
                return result != null;
            }
        }

        private static bool IsValid(MobaDamageTransaction transaction)
        {
            return transaction != null && !transaction.IsCancelled && transaction.Attack != null &&
                   transaction.Attack.TargetActorId > 0;
        }

        private DamageResult ExecuteCore(AttackInfo attack)
        {
            var diagnostics = _diagnostics;
            var start = diagnostics != null ? diagnostics.GetTimestamp() : 0L;
            var combatFacts = MobaCombatExecutionFacts.Resolve(attack);
            attack.CombatFlags = combatFacts.Flags;
            attack.TryResolveCombatExecutionContext(out var sourceContext);
            var attackNode = BeginDamageExecution(
                MobaExecutionKind.DamageAttack,
                attack.ReasonParam,
                attack.AttackerActorId,
                attack.TargetActorId,
                in sourceContext,
                in combatFacts,
                out var attackContext);
            if (attackNode.ContextId != 0L)
            {
                attackContext = attackContext.WithPayload(attack);
                attack.SetExecutionContext(in attackContext);
            }

            var attackSucceeded = false;
            var healthCommitted = false;

            try
            {
                if (!_actors.TryGetActorEntity(attack.TargetActorId, out var target) || target == null)
                {
                    diagnostics?.Counter("moba.damage.targetMissing");
                    TryCollectDamageCalculation(attack, null, BattleDiagnosticDamageStage.TargetMissing, 0f);
                    return null;
                }

                Publish(DamagePipelineEvents.AttackCreated, attack);
                Publish(DamagePipelineEvents.BeforeCalc, attack);

                var calc = new AttackCalcInfo(attack);
                var calcParent = attackNode.ContextId != 0L ? attackContext : sourceContext;
                var calcNode = BeginDamageExecution(
                    MobaExecutionKind.DamageCalc,
                    attack.ReasonParam,
                    attack.AttackerActorId,
                    attack.TargetActorId,
                    in calcParent,
                    in combatFacts,
                    out var calcContext);
                if (calcNode.ContextId != 0L)
                {
                    calcContext = calcContext.WithPayload(calc);
                    calc.SetExecutionContext(in calcContext);
                }

                var calcSucceeded = false;
                try
                {
                    Publish(DamagePipelineEvents.CalcBegin, calc);
                    ApplyFormula(calc);
                    Publish(DamagePipelineEvents.BeforeApply, calc);
                    calcSucceeded = true;
                }
                finally
                {
                    EndDamageExecution(in calcNode, calcSucceeded);
                }

                var applyParent = calcNode.ContextId != 0L ? calcContext : calcParent;
                var applyNode = BeginDamageExecution(
                    MobaExecutionKind.DamageApply,
                    attack.ReasonParam,
                    attack.AttackerActorId,
                    attack.TargetActorId,
                    in applyParent,
                    in combatFacts,
                    out var applyContext);
                var applySucceeded = false;
                DamageResult result;
                try
                {
                    result = CommitDamage(attack, calc, target, in applyNode, in applyContext,
                        () => healthCommitted = true);
                    applySucceeded = result != null;
                }
                finally
                {
                    EndDamageExecution(in applyNode, applySucceeded || healthCommitted);
                }

                if (result == null) return null;

                attackSucceeded = true;
                RecordCombatActivity(result);
                Publish(DamagePipelineEvents.AfterApply, result);
                TryCollectDamage(result, calc);
                diagnostics?.Counter("moba.damage.applied");
                diagnostics?.Sample("moba.damage.value", result.Value);
                attackSucceeded = true;
                return result;
            }
            finally
            {
                EndDamageExecution(in attackNode, attackSucceeded || healthCommitted);
                diagnostics?.RecordDuration(
                    MobaBattleDiagnosticMetric.DamagePipeline,
                    start,
                    MobaBattleDiagnosticsDefaults.DamagePipelineWarnMs);
            }
        }

        private DamageResult CommitDamage(
            AttackInfo attack,
            AttackCalcInfo calc,
            global::ActorEntity target,
            in MobaExecutionContextNode applyNode,
            in MobaCombatExecutionContext applyContext,
            Action onHealthCommitted)
        {
            var diagnostics = _diagnostics;

            var shieldCommitted = calc.ShieldPlan == null || _shields == null || _shields.CommitAbsorb(calc.ShieldPlan);
                if (!shieldCommitted)
                {
                    diagnostics?.Counter("moba.damage.shieldCommitConflict");
                    TryCollectDamageCalculation(attack, calc, BattleDiagnosticDamageStage.ShieldCommitRejected, 0f);
                    return null;
                }

            var hpCommitted = false;
            var shieldFinalized = false;
            try
            {
                attack.TryGetOrigin(out var attackOrigin);
                var commitOrigin = applyNode.ContextId != 0L ? applyContext.Origin : attackOrigin;
                var hpDamage = calc.HpDamage.FixedValue;
                var committed = hpDamage > AbilityKit.Deterministic.Fixed64.Zero
                    ? _damage.CommitDamage(
                        attackerActorId: attack.AttackerActorId,
                        targetActorId: attack.TargetActorId,
                        damageType: (int)attack.DamageType,
                        value: hpDamage,
                        reasonKind: (int)attack.ReasonKind,
                        reasonParam: attack.ReasonParam,
                        origin: commitOrigin,
                        collectDiagnostic: _eventCollector == null,
                        onCommitted: () =>
                        {
                            hpCommitted = true;
                            onHealthCommitted?.Invoke();
                        })
                    : default;
                if (hpDamage > AbilityKit.Deterministic.Fixed64.Zero && !committed.Succeeded)
                {
                    diagnostics?.Counter("moba.damage.healthCommitRejected");
                    TryCollectDamageCalculation(attack, calc, BattleDiagnosticDamageStage.HealthCommitRejected, 0f);
                    return null;
                }

                var targetAttributes = target.GetMobaAttrs();
                var result = new DamageResult
                {
                    AttackerActorId = attack.AttackerActorId,
                    TargetActorId = attack.TargetActorId,

                    DamageType = attack.DamageType,
                    CritType = attack.CritType,
                    ReasonKind = attack.ReasonKind,
                    ReasonParam = attack.ReasonParam,
                    CombatFlags = attack.CombatFlags,
                    Value = committed.AppliedValue,
                    TargetHp = committed.Succeeded ? committed.TargetHp : targetAttributes.Hp,
                    TargetMaxHp = committed.Succeeded ? committed.TargetMaxHp : targetAttributes.MaxHp,
                };

                shieldFinalized = true;
                _shields?.FinalizeAbsorb(calc.ShieldPlan);

                if (applyNode.ContextId != 0L)
                {
                    var resultContext = applyContext.WithPayload(result);
                    result.SetExecutionContext(in resultContext);
                }
                else if (attack.TryGetOrigin(out var origin))
                {
                    result.SetOrigin(in origin);
                }

                return result;
            }
            finally
            {
                if (shieldCommitted && !shieldFinalized)
                {
                    if (hpCommitted || calc.HpDamage.FixedValue <= AbilityKit.Deterministic.Fixed64.Zero)
                        _shields?.FinalizeAbsorb(calc.ShieldPlan);
                    else
                        _shields?.RollbackAbsorb(calc.ShieldPlan);
                }
            }
        }

        private void ApplyFormula(AttackCalcInfo calc)
        {
            if (calc == null || calc.Attack == null) return;

            var attack = calc.Attack;
            var kind = (DamageFormulaKind)attack.FormulaKind;
            if (kind == DamageFormulaKind.None) kind = DamageFormulaKind.Standard;

            switch (kind)
            {
                case DamageFormulaKind.Standard:
                default:
                    if (_validatedStages == null)
                    {
                        var validation = _stageProvider.Validate();
                        if (!validation.Succeeded)
                            throw new InvalidOperationException("Invalid damage stage configuration: " + string.Join("; ", validation.Errors));
                        _validatedStages = _stageProvider.GetStages() ??
                            throw new InvalidOperationException("Damage stage provider returned no stages.");
                    }

                    RunStages(calc, _validatedStages);
                    break;
            }
        }

        private void RunStages(AttackCalcInfo calc, System.Collections.Generic.IReadOnlyList<MobaDamageStageDescriptor> stages)
        {
            if (calc == null || stages == null) return;

            var diagnostics = _diagnostics;
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i].Stage;
                if (stage == null) continue;

                var start = diagnostics != null ? diagnostics.GetTimestamp() : 0L;
                stage.Execute(calc);
                diagnostics?.RecordDuration(
                    MobaBattleDiagnosticMetric.DamageStage,
                    start,
                    MobaBattleDiagnosticsDefaults.DamageStageWarnMs);
                Publish(stage.EventId, calc);
            }
        }

        private void Publish(string eventId, object payload)
        {
            var eventBus = _eventBus;
            if (eventBus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var eid = TriggeringIdUtil.GetEventEid(eventId);

            var objectKey = new EventKey<object>(eid);
            var publishObject = eventBus.HasSubscribers(objectKey);

            if (payload is AttackInfo ai2)
            {
                eventBus.Publish(new EventKey<AttackInfo>(eid), in ai2);
                if (publishObject)
                {
                    object boxed = ai2;
                    eventBus.Publish(objectKey, in boxed);
                }
            }
            else if (payload is AttackCalcInfo ac2)
            {
                eventBus.Publish(new EventKey<AttackCalcInfo>(eid), in ac2);
                if (publishObject)
                {
                    object boxed = ac2;
                    eventBus.Publish(objectKey, in boxed);
                }
            }
            else if (payload is DamageResult dr2)
            {
                var typedKey = new EventKey<DamageResult>(eid);
                eventBus.Publish(typedKey, in dr2);
                if (publishObject)
                {
                    object boxed = dr2;
                    eventBus.Publish(objectKey, in boxed);
                }
            }
            else if (publishObject)
            {
                object boxed = payload;
                eventBus.Publish(objectKey, in boxed);
            }
        }

        public static MobaBattleDiagnosticEventDraft CreateDiagnosticDraft(DamageResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var origin = result.TryGetOrigin(out var resolvedOrigin)
                ? resolvedOrigin
                : default;
            var handle = origin.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var configId = result.ReasonParam != 0
                ? result.ReasonParam
                : origin.ImmediateConfigId;
            var contextId = origin.ImmediateContextId != 0L
                ? origin.ImmediateContextId
                : origin.EffectiveParentContextId;

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                result.AttackerActorId,
                result.TargetActorId,
                configId,
                origin.EffectiveRootContextId,
                contextId,
                runtime,
                summary: $"damage={result.Value:0.###}, targetHp={result.TargetHp:0.###}");
        }

        public static MobaBattleDiagnosticEventDraft CreateCalculationDraft(
            AttackInfo attack, AttackCalcInfo calc, BattleDiagnosticDamageStage stage, float appliedHpDamage,
            float? targetHp = null, string failureReason = "")
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            attack.TryGetOrigin(out var origin);
            var handle = origin.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var payloadData = new BattleDiagnosticDamageCalculationPayload(stage,
                attack.BaseDamage.FixedValue.RawValue,
                calc?.RawDamage.FixedValue.RawValue ?? 0L,
                calc?.MitigatedDamage.FixedValue.RawValue ?? 0L,
                calc?.ShieldAbsorb.FixedValue.RawValue ?? 0L,
                calc?.HpDamage.FixedValue.RawValue ?? 0L,
                AbilityKit.Deterministic.Fixed64.FromSingle(appliedHpDamage).RawValue);
            var payload = BattleDiagnosticEventPayload.FromDamageCalculation(in payloadData);
            var contextId = origin.ImmediateContextId != 0L
                ? origin.ImmediateContextId : origin.EffectiveParentContextId;
            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage, BattleDiagnosticEventChannel.DamageAndHeal,
                stage == BattleDiagnosticDamageStage.Completed
                    ? BattleDiagnosticEventOutcome.Succeeded : BattleDiagnosticEventOutcome.Failed,
                attack.AttackerActorId, attack.TargetActorId,
                attack.ReasonParam != 0 ? attack.ReasonParam : origin.ImmediateConfigId,
                origin.EffectiveRootContextId, contextId, runtime,
                payloadVersion: BattleDiagnosticDamageCalculationPayload.CurrentSchemaVersion,
                summary: $"damage calculation: {stage}; applied={appliedHpDamage:0.###}" +
                    (targetHp.HasValue ? $", targetHp={targetHp.Value:0.###}" : string.Empty) +
                    (string.IsNullOrEmpty(failureReason) ? string.Empty : "; reason=" +
                        (failureReason.Length <= 128 ? failureReason : failureReason.Substring(0, 128))), payload: payload);
        }

        private void TryCollectDamageCalculation(AttackInfo attack, AttackCalcInfo calc,
            BattleDiagnosticDamageStage stage, float appliedHpDamage, string failureReason = "")
        {
            try
            {
                var collector = _eventCollector;
                if (collector == null || attack == null ||
                    !collector.IsEnabled(BattleDiagnosticEventChannel.DamageAndHeal)) return;
                var draft = CreateCalculationDraft(attack, calc, stage, appliedHpDamage, failureReason: failureReason);
                collector.TryCollect(in draft);
            }
            catch
            {
            }
        }

        private void TryCollectDamage(DamageResult result, AttackCalcInfo calc)
        {
            try
            {
                var collector = _eventCollector;
                if (collector == null || result == null ||
                    !collector.IsEnabled(BattleDiagnosticEventChannel.DamageAndHeal)) return;

                var draft = calc?.Attack != null
                    ? CreateCalculationDraft(calc.Attack, calc, BattleDiagnosticDamageStage.Completed, result.Value, result.TargetHp)
                    : CreateDiagnosticDraft(result);
                collector.TryCollect(in draft);
            }
            catch
            {
            }
        }

        private void RecordCombatActivity(DamageResult result)
        {
            if (result == null || result.Value <= 0f) return;
            var combatActivity = _combatActivity;
            if (combatActivity == null) return;

            combatActivity.RecordCombat(result.AttackerActorId);
            combatActivity.RecordCombat(result.TargetActorId);
        }

        private MobaExecutionContextNode BeginDamageExecution(
            MobaExecutionKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            in MobaCombatExecutionContext parentContext,
            in MobaCombatExecutionFacts combatFacts,
            out MobaCombatExecutionContext executionContext)
        {
            executionContext = default;
            var contexts = _executionContexts;
            if (contexts == null)
            {
                throw new InvalidOperationException(
                    "Damage execution requires MobaExecutionContextRegistry.");
            }

            var frame = _frameTime != null ? _frameTime.Frame.Value : parentContext.Frame;
            var effectiveConfigId = configId != 0 ? configId : parentContext.ConfigId;
            var node = contexts.Create(new MobaExecutionContextCreateRequest(
                kind,
                effectiveConfigId,
                sourceActorId,
                targetActorId,
                parentContextId: parentContext.ParentContextId,
                rootContextId: parentContext.RootContextId,
                ownerContextId: parentContext.OwnerContextId,
                frame: frame,
                triggerId: parentContext.TriggerId,
                originKind: ToExecutionKind(parentContext.OriginKind),
                originConfigId: parentContext.ConfigId,
                combatFlags: combatFacts.Flags));

            var executionKind = NormalizeDamageExecutionKind(kind);
            var handle = parentContext.SkillRuntimeHandle;
            var origin = new MobaGameplayOrigin(
                sourceActorId,
                targetActorId,
                executionKind,
                effectiveConfigId,
                node.ContextId,
                node.ContextId,
                node.RootContextId,
                node.OwnerContextId,
                handle);
            var lineage = new MobaEffectLineageInput(
                EffectContextKind.Trigger,
                executionKind,
                sourceActorId,
                targetActorId,
                node.ContextId,
                node.RootContextId,
                node.OwnerContextId,
                effectiveConfigId);
            var snapshot = new MobaTriggerExecutionSnapshot(
                EffectContextKind.Trigger,
                sourceActorId,
                targetActorId,
                node.ContextId,
                node.RootContextId,
                node.OwnerContextId,
                parentContext.TriggerId,
                effectiveConfigId,
                frame,
                handle);
            executionContext = new MobaCombatExecutionContext(
                null,
                lineage,
                origin,
                snapshot,
                handle,
                frame,
                combatFacts);
            return node;
        }

        private void EndDamageExecution(in MobaExecutionContextNode node, bool succeeded)
        {
            if (node.ContextId == 0L) return;
            _executionContexts?.End(
                node.ContextId,
                (int)(succeeded ? MobaExecutionEndReason.Completed : MobaExecutionEndReason.Failed),
                _frameTime != null ? _frameTime.Frame.Value : node.CreatedFrame);
        }

        private static MobaExecutionKind ToExecutionKind(MobaExecutionKind kind)
        {
            switch (kind)
            {
                case MobaExecutionKind.SkillCast: return MobaExecutionKind.SkillCast;
                case MobaExecutionKind.SkillEffect: return MobaExecutionKind.SkillEffect;
                case MobaExecutionKind.SkillPhase: return MobaExecutionKind.SkillPhase;
                case MobaExecutionKind.EffectExecution: return MobaExecutionKind.EffectExecution;
                case MobaExecutionKind.EffectAction: return MobaExecutionKind.EffectAction;
                case MobaExecutionKind.BuffApply: return MobaExecutionKind.BuffApply;
                case MobaExecutionKind.BuffTick: return MobaExecutionKind.BuffTick;
                case MobaExecutionKind.BuffRemove: return MobaExecutionKind.BuffRemove;
                case MobaExecutionKind.ProjectileLaunch: return MobaExecutionKind.ProjectileLaunch;
                case MobaExecutionKind.ProjectileHit: return MobaExecutionKind.ProjectileHit;
                case MobaExecutionKind.AreaSpawn: return MobaExecutionKind.AreaSpawn;
                case MobaExecutionKind.AreaEnter: return MobaExecutionKind.AreaEnter;
                case MobaExecutionKind.AreaExit: return MobaExecutionKind.AreaExit;
                case MobaExecutionKind.AreaExpire: return MobaExecutionKind.AreaExpire;
                case MobaExecutionKind.AreaStay: return MobaExecutionKind.AreaStay;
                case MobaExecutionKind.SummonSpawn: return MobaExecutionKind.SummonSpawn;
                case MobaExecutionKind.SummonDeath: return MobaExecutionKind.SummonDeath;
                case MobaExecutionKind.UnitSpawn: return MobaExecutionKind.UnitSpawn;
                case MobaExecutionKind.UnitDespawn: return MobaExecutionKind.UnitDespawn;
                case MobaExecutionKind.UnitDeath: return MobaExecutionKind.UnitDeath;
                case MobaExecutionKind.UnitRespawn: return MobaExecutionKind.UnitRespawn;
                case MobaExecutionKind.DamageAttack: return MobaExecutionKind.DamageAttack;
                case MobaExecutionKind.DamageCalc: return MobaExecutionKind.DamageCalc;
                case MobaExecutionKind.DamageApply: return MobaExecutionKind.DamageApply;
                case MobaExecutionKind.PresentationPlay: return MobaExecutionKind.PresentationPlay;
                case MobaExecutionKind.PresentationStop: return MobaExecutionKind.PresentationStop;
                default: return MobaExecutionKind.None;
            }
        }

        private static MobaExecutionKind NormalizeDamageExecutionKind(MobaExecutionKind kind)
        {
            switch (kind)
            {
                case MobaExecutionKind.DamageAttack: return MobaExecutionKind.DamageAttack;
                case MobaExecutionKind.DamageCalc: return MobaExecutionKind.DamageCalc;
                case MobaExecutionKind.DamageApply: return MobaExecutionKind.DamageApply;
                default: return MobaExecutionKind.None;
            }
        }

        public void Dispose()
        {
        }
    }
}
