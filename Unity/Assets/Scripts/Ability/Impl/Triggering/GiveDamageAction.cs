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
    public sealed class GiveDamageAction : ITriggerAction
    {
        private readonly float _value;
        private readonly DamageType _damageType;
        private readonly CritType _critType;
        private readonly DamageReasonKind _reasonKind;
        private readonly int _reasonParam;

        private readonly string _targetKey;
        private readonly string _attackerKey;

        public GiveDamageAction(float value, DamageType damageType, CritType critType, DamageReasonKind reasonKind, int reasonParam, string targetKey, string attackerKey)
        {
            _value = value;
            _damageType = damageType;
            _critType = critType;
            _reasonKind = reasonKind;
            _reasonParam = reasonParam;
            _targetKey = targetKey;
            _attackerKey = attackerKey;
        }

        public static GiveDamageAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("give_damage requires args");

            var value = 0f;
            if (args.TryGetValue("value", out var vObj) && vObj != null)
            {
                value = vObj is float f ? f : vObj is int i ? i : Convert.ToSingle(vObj);
            }

            var damageType = DamageType.Physical;
            if (args.TryGetValue("damageType", out var dtObj) && dtObj != null)
            {
                damageType = ParseEnum(dtObj, DamageType.Physical);
            }

            var critType = CritType.None;
            if (args.TryGetValue("crit", out var cObj) && cObj != null)
            {
                critType = ParseEnum(cObj, CritType.None);
            }
            else if (args.TryGetValue("isCritical", out var isCritObj) && isCritObj is bool b && b)
            {
                critType = CritType.Critical;
            }

            var reasonKind = DamageReasonKind.Skill;
            if (args.TryGetValue("reasonKind", out var rkObj) && rkObj != null)
            {
                reasonKind = ParseEnum(rkObj, DamageReasonKind.Skill);
            }

            var reasonParam = 0;
            if (args.TryGetValue("reasonParam", out var rpObj) && rpObj != null)
            {
                reasonParam = rpObj is int rpi ? rpi : rpObj is long rpl ? (int)rpl : Convert.ToInt32(rpObj);
            }

            var targetKey = args.TryGetValue("targetKey", out var tkObj) && tkObj is string tks && !string.IsNullOrEmpty(tks) ? tks : null;
            var attackerKey = args.TryGetValue("attackerKey", out var akObj) && akObj is string aks && !string.IsNullOrEmpty(aks) ? aks : null;

            return new GiveDamageAction(value, damageType, critType, reasonKind, reasonParam, targetKey, attackerKey);
        }

        public void Execute(TriggerContext context)
        {
            if (context == null) return;

            var pipeline = context.Services?.GetService(typeof(DamagePipelineService)) as DamagePipelineService;
            if (pipeline == null)
            {
                Log.Warning("[Trigger] give_damage cannot resolve DamagePipelineService from DI");
                return;
            }

            // Resolve target
            object targetObj = context.Target;
            if (!string.IsNullOrEmpty(_targetKey) && context.Event.Args != null && context.Event.Args.TryGetValue(_targetKey, out var tObj) && tObj != null)
            {
                targetObj = tObj;
            }

            if (!TryResolveActorId(targetObj, out var targetActorId) || targetActorId <= 0)
            {
                Log.Warning("[Trigger] give_damage requires a valid target actorId");
                return;
            }

            // Resolve attacker (DOT: attacker=施加者)
            object attackerObj = context.Source;
            if (!string.IsNullOrEmpty(_attackerKey) && context.Event.Args != null && context.Event.Args.TryGetValue(_attackerKey, out var aObj) && aObj != null)
            {
                attackerObj = aObj;
            }

            TryResolveActorId(attackerObj, out var attackerActorId);

            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = _damageType,
                CritType = _critType,
                ReasonKind = _reasonKind,
                ReasonParam = _reasonParam,
            };
            attack.BaseDamage.BaseValue = _value;

            pipeline.Execute(attack);
        }

        private static TEnum ParseEnum<TEnum>(object obj, TEnum fallback) where TEnum : struct
        {
            try
            {
                if (obj is int i) return (TEnum)Enum.ToObject(typeof(TEnum), i);
                if (obj is long l) return (TEnum)Enum.ToObject(typeof(TEnum), (int)l);
                if (obj is string s && !string.IsNullOrEmpty(s))
                {
                    if (Enum.TryParse<TEnum>(s, ignoreCase: true, out var parsed)) return parsed;
                    if (int.TryParse(s, out var i2)) return (TEnum)Enum.ToObject(typeof(TEnum), i2);
                }
            }
            catch
            {
            }
            return fallback;
        }

        private static bool TryResolveActorId(object obj, out int actorId)
        {
            actorId = 0;
            if (obj == null) return false;

            if (obj is int i)
            {
                actorId = i;
                return actorId > 0;
            }

            if (obj is long l)
            {
                actorId = (int)l;
                return actorId > 0;
            }

            if (obj is EcsEntityId id)
            {
                actorId = id.ActorId;
                return actorId > 0;
            }

            if (obj is IUnitFacade unit)
            {
                actorId = unit.Id.ActorId;
                return actorId > 0;
            }

            if (obj is global::ActorEntity e && e.hasActorId)
            {
                actorId = e.actorId.Value;
                return actorId > 0;
            }

            return false;
        }
    }
}
