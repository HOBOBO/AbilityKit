using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class TakeDamageAction : ITriggerAction
    {
        private readonly float _value;
        private readonly float _rate;
        private readonly DamageType _damageType;
        private readonly CritType _critType;
        private readonly DamageReasonKind _reasonKind;
        private readonly int _reasonParam;

        // Optional overrides
        private readonly string _attackerKey;
        private readonly string _targetKey;

        public TakeDamageAction(float value, float rate, DamageType damageType, CritType critType, DamageReasonKind reasonKind, int reasonParam, string attackerKey, string targetKey)
        {
            _value = value;
            _rate = rate;
            _damageType = damageType;
            _critType = critType;
            _reasonKind = reasonKind;
            _reasonParam = reasonParam;
            _attackerKey = attackerKey;
            _targetKey = targetKey;
        }

        public static TakeDamageAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("take_damage requires args");

            var value = 0f;
            if (args.TryGetValue("value", out var vObj) && vObj != null)
            {
                value = vObj is float f ? f : vObj is int i ? i : Convert.ToSingle(vObj);
            }

            var rate = 1f;
            if (args.TryGetValue("rate", out var rObj) && rObj != null)
            {
                rate = rObj is float rf ? rf : rObj is int ri ? ri : Convert.ToSingle(rObj);
            }

            var damageType = DamageType.Physical;
            if (args.TryGetValue("damageType", out var dtObj) && dtObj != null)
            {
                damageType = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(dtObj, DamageType.Physical);
            }

            var critType = CritType.None;
            if (args.TryGetValue("crit", out var cObj) && cObj != null)
            {
                critType = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(cObj, CritType.None);
            }

            var reasonKind = DamageReasonKind.Buff;
            if (args.TryGetValue("reasonKind", out var rkObj) && rkObj != null)
            {
                reasonKind = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(rkObj, DamageReasonKind.Buff);
            }

            var reasonParam = 0;
            if (args.TryGetValue("reasonParam", out var rpObj) && rpObj != null)
            {
                reasonParam = rpObj is int rpi ? rpi : rpObj is long rpl ? (int)rpl : Convert.ToInt32(rpObj);
            }

            var attackerKey = args.TryGetValue("attackerKey", out var akObj) && akObj is string aks && !string.IsNullOrEmpty(aks) ? aks : null;
            var targetKey = args.TryGetValue("targetKey", out var tkObj) && tkObj is string tks && !string.IsNullOrEmpty(tks) ? tks : null;

            return new TakeDamageAction(value, rate, damageType, critType, reasonKind, reasonParam, attackerKey, targetKey);
        }

        public void Execute(TriggerContext context)
        {
            if (context == null) return;

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

            // allow overrides via args
            if (!string.IsNullOrEmpty(_attackerKey) && context.Event.Args != null && context.Event.Args.TryGetValue(_attackerKey, out var aObj) && aObj != null)
            {
                attackerObj = aObj;
            }

            if (!string.IsNullOrEmpty(_targetKey) && context.Event.Args != null && context.Event.Args.TryGetValue(_targetKey, out var tObj) && tObj != null)
            {
                targetObj = tObj;
            }

            // if not overridden, try infer target from payload
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

            if (!global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryResolveActorId(attackerObj, out var attackerActorId) || attackerActorId <= 0)
            {
                Log.Warning("[Trigger] take_damage requires a valid attacker actorId (default=context.Target)");
                return;
            }

            if (!global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryResolveActorId(targetObj, out var targetActorId) || targetActorId <= 0)
            {
                Log.Warning("[Trigger] take_damage cannot resolve target actorId (default=payload.AttackerActorId)");
                return;
            }

            // determine base value
            var baseValue = _value;
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

            baseValue *= _rate;

            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = _damageType,
                CritType = _critType,
                ReasonKind = _reasonKind,
                ReasonParam = _reasonParam,
            };
            attack.BaseDamage.BaseValue = baseValue;

            pipeline.Execute(attack);
        }

    }
}
