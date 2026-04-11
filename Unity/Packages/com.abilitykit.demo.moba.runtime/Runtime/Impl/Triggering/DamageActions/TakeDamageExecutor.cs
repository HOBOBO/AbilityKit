using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Share.Impl.Moba;

namespace AbilityKit.Ability.Impl.Triggering.DamageActions
{
    using AbilityKit.Ability.Impl.Moba;
    using AbilityKit.Ability.Share.Effect;
    public static class TakeDamageExecutor
    {
        public static void Execute(TriggerContext context, DamageActionSpec spec)
        {
            if (context == null) return;
            if (spec == null) return;

            var pipeline = context.Services?.GetService(typeof(DamagePipelineService)) as DamagePipelineService;
            if (pipeline == null)
            {
                Log.Warning("[Trigger] take_damage cannot resolve DamagePipelineService from DI");
                return;
            }

            // Default mapping (生成型):
            // attacker = 受击者 (context.Target)
            // target   = 原攻击者 (from payload)

            object attackerObj = context.Target;
            object targetObj = null;

            if (!string.IsNullOrEmpty(spec.AttackerKey) && context.Event.Args != null && context.Event.Args.TryGetValue(spec.AttackerKey, out var aObj) && aObj != null)
            {
                attackerObj = aObj;
            }

            if (!string.IsNullOrEmpty(spec.TargetKey) && context.Event.Args != null && context.Event.Args.TryGetValue(spec.TargetKey, out var tObj) && tObj != null)
            {
                targetObj = tObj;
            }

            if (targetObj == null)
            {
                var payload = context.Event.Payload;
                if (payload is DamageResult dr)
                {
                    targetObj = dr.AttackerActorId;
                }
                else if (payload is AttackCalcInfo calc && calc.Attack != null)
                {
                    targetObj = calc.Attack.AttackerActorId;
                }
                else if (payload is AttackInfo ai)
                {
                    targetObj = ai.AttackerActorId;
                }
            }

            if (!TriggerActionArgUtil.TryResolveActorId(attackerObj, out var attackerActorId) || attackerActorId <= 0)
            {
                Log.Warning("[Trigger] take_damage requires a valid attacker actorId (default=context.Target)");
                return;
            }

            if (!TriggerActionArgUtil.TryResolveActorId(targetObj, out var targetActorId) || targetActorId <= 0)
            {
                Log.Warning("[Trigger] take_damage cannot resolve target actorId (default=payload.AttackerActorId)");
                return;
            }

            var baseValue = spec.Value;
            if (baseValue <= 0f)
            {
                var payload = context.Event.Payload;
                if (payload is DamageResult dr2)
                {
                    baseValue = dr2.Value;
                }
                else if (payload is AttackCalcInfo calc2)
                {
                    baseValue = calc2.HpDamage.Value;
                }
                else if (payload is AttackInfo ai2)
                {
                    baseValue = ai2.BaseDamage.Value;
                }
            }

            baseValue *= spec.Rate;

            if (spec.UseProjectileHitDecay && context.Event.Args != null && context.Event.Args.TryGetValue(ProjectileTriggering.Args.HitDecayRate, out var decayObj) && decayObj != null)
            {
                try
                {
                    var decay = decayObj is float df ? df : decayObj is double dd ? (float)dd : Convert.ToSingle(decayObj);
                    if (decay > 0f) baseValue *= decay;
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[TakeDamageExecutor] parse projectile hit decay failed");
                }
            }

            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = spec.DamageType,
                CritType = spec.CritType,
                ReasonKind = spec.ReasonKind,
                ReasonParam = spec.ReasonParam,
                FormulaKind = spec.FormulaKind,
            };

            DamageActionOriginUtil.FillOrigin(context, attack, attackerObj ?? attackerActorId, targetObj ?? targetActorId);
            attack.BaseDamage.BaseValue = baseValue;

            pipeline.Execute(attack);
        }
    }
}
