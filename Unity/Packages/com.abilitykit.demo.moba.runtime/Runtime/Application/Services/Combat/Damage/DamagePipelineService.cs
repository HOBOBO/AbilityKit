using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(DamagePipelineService))]
    public sealed class DamagePipelineService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly MobaDamageService _damage;
        private readonly MobaShieldService _shields;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly List<IMobaDamagePipelineStage> _standardStages;
        [WorldInject(required: false)] private MobaTraceRegistry _trace = null;
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
            IMobaBattleDiagnosticEventSink eventCollector = null)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _shields = shields;
            _eventBus = eventBus;
            _diagnostics = diagnostics;
            _eventCollector = eventCollector;
            _standardStages = new List<IMobaDamagePipelineStage>(4)
            {
                new MobaBaseDamagePipelineStage(),
                new MobaDamageMitigationPipelineStage(mitigation),
                new MobaShieldAbsorbPipelineStage(shields),
                new MobaFinalDamagePipelineStage(),
            };
        }

        public DamageResult Execute(AttackInfo attack)
        {
            if (attack == null) return null;
            if (attack.TargetActorId <= 0) return null;

            var diagnostics = _diagnostics;
            var start = diagnostics != null ? diagnostics.GetTimestamp() : 0L;

            try
            {
                if (!_actors.TryGetActorEntity(attack.TargetActorId, out var target) || target == null)
                {
                    diagnostics?.Counter("moba.damage.targetMissing");
                    return null;
                }

                Publish(DamagePipelineEvents.AttackCreated, attack);
                Publish(DamagePipelineEvents.BeforeCalc, attack);

                var calc = new AttackCalcInfo(attack);

                Publish(DamagePipelineEvents.CalcBegin, calc);

                ApplyFormula(calc);

                Publish(DamagePipelineEvents.BeforeApply, calc);

                var shieldCommitted = calc.ShieldPlan == null || _shields == null || _shields.CommitAbsorb(calc.ShieldPlan);
                if (!shieldCommitted)
                {
                    diagnostics?.Counter("moba.damage.shieldCommitConflict");
                    return null;
                }

                attack.TryGetOrigin(out var attackOrigin);
                var hpDamage = calc.HpDamage.Value;
                var committed = hpDamage > 0f
                    ? _damage.CommitDamage(
                        attackerActorId: attack.AttackerActorId,
                        targetActorId: attack.TargetActorId,
                        damageType: (int)attack.DamageType,
                        value: hpDamage,
                        reasonKind: (int)attack.ReasonKind,
                        reasonParam: attack.ReasonParam,
                        origin: attackOrigin)
                    : default;
                if (hpDamage > 0f && !committed.Succeeded)
                {
                    _shields?.RollbackAbsorb(calc.ShieldPlan);
                    diagnostics?.Counter("moba.damage.healthCommitRejected");
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
                    Value = committed.AppliedValue,
                    TargetHp = committed.Succeeded ? committed.TargetHp : targetAttributes.Hp,
                    TargetMaxHp = committed.Succeeded ? committed.TargetMaxHp : targetAttributes.MaxHp,
                };

                _shields?.FinalizeAbsorb(calc.ShieldPlan);

                if (attack.TryGetOrigin(out var origin))
                {
                    result.SetOrigin(in origin);
                    TryTraceDamageApply(in origin, result);
                }

                RecordCombatActivity(result);
                Publish(DamagePipelineEvents.AfterApply, result);
                TryCollectDamage(result);
                diagnostics?.Counter("moba.damage.applied");
                diagnostics?.Sample("moba.damage.value", result.Value);
                return result;
            }
            finally
            {
                diagnostics?.RecordDuration(
                    MobaBattleDiagnosticMetric.DamagePipeline,
                    start,
                    MobaBattleDiagnosticsDefaults.DamagePipelineWarnMs);
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
                    RunStages(calc, _standardStages);
                    break;
            }
        }

        private void RunStages(AttackCalcInfo calc, List<IMobaDamagePipelineStage> stages)
        {
            if (calc == null || stages == null) return;

            var diagnostics = _diagnostics;
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
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

        private void TryCollectDamage(DamageResult result)
        {
            try
            {
                var collector = _eventCollector;
                if (collector == null || result == null) return;

                var draft = CreateDiagnosticDraft(result);
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

        private void TryTraceDamageApply(in MobaGameplayOrigin origin, DamageResult result)
        {
            if (result == null) return;
            var trace = _trace;
            if (trace == null) return;

            var parentContextId = origin.EffectiveParentContextId;
            if (parentContextId == 0L) return;

            var configId = result.ReasonParam != 0 ? result.ReasonParam : origin.ImmediateConfigId;
            if (configId == 0) return;

            var contextId = trace.CreateChildContext(
                parentContextId,
                MobaTraceKind.DamageApply,
                configId,
                result.AttackerActorId,
                result.TargetActorId);

            if (contextId != 0L)
            {
                trace.EndContext(contextId, TraceLifecycleReason.Completed);
            }
        }

        public void Dispose()
        {
        }
    }
}
