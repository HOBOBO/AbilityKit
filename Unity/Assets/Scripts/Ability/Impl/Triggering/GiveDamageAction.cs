using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Share.Common.Pool;

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
        private readonly int _queryTemplateId;
        private readonly string _aimPosKey;
        private readonly bool _log;

        private static readonly ObjectPool<List<int>> _intListPool = Pools.GetPool(
            createFunc: () => new List<int>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        public GiveDamageAction(float value, DamageType damageType, CritType critType, DamageReasonKind reasonKind, int reasonParam, string targetKey, string attackerKey, int queryTemplateId, string aimPosKey, bool log)
        {
            _value = value;
            _damageType = damageType;
            _critType = critType;
            _reasonKind = reasonKind;
            _reasonParam = reasonParam;
            _targetKey = targetKey;
            _attackerKey = attackerKey;
            _queryTemplateId = queryTemplateId;
            _aimPosKey = aimPosKey;
            _log = log;
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
                damageType = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(dtObj, DamageType.Physical);
            }

            var critType = CritType.None;
            if (args.TryGetValue("crit", out var cObj) && cObj != null)
            {
                critType = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(cObj, CritType.None);
            }
            else if (args.TryGetValue("isCritical", out var isCritObj) && isCritObj is bool b && b)
            {
                critType = CritType.Critical;
            }

            var reasonKind = DamageReasonKind.Skill;
            if (args.TryGetValue("reasonKind", out var rkObj) && rkObj != null)
            {
                reasonKind = global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.ParseEnum(rkObj, DamageReasonKind.Skill);
            }

            var reasonParam = 0;
            if (args.TryGetValue("reasonParam", out var rpObj) && rpObj != null)
            {
                reasonParam = rpObj is int rpi ? rpi : rpObj is long rpl ? (int)rpl : Convert.ToInt32(rpObj);
            }

            var targetKey = args.TryGetValue("targetKey", out var tkObj) && tkObj is string tks && !string.IsNullOrEmpty(tks) ? tks : null;
            var attackerKey = args.TryGetValue("attackerKey", out var akObj) && akObj is string aks && !string.IsNullOrEmpty(aks) ? aks : null;

            var queryTemplateId = 0;
            if (args.TryGetValue("queryTemplateId", out var qObj) && qObj != null)
            {
                if (qObj is int qi) queryTemplateId = qi;
                else if (qObj is long ql) queryTemplateId = (int)ql;
                else if (qObj is string qs && int.TryParse(qs, out var parsed)) queryTemplateId = parsed;
            }

            var aimPosKey = args.TryGetValue("aimPosKey", out var apObj) && apObj is string aps && !string.IsNullOrEmpty(aps) ? aps : null;

            var log = args.TryGetValue("log", out var logObj) && logObj is bool lb && lb;

            return new GiveDamageAction(value, damageType, critType, reasonKind, reasonParam, targetKey, attackerKey, queryTemplateId, aimPosKey, log);
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

            // Resolve attacker (DOT: attacker=施加者)
            object attackerObj = context.Source;
            if (!string.IsNullOrEmpty(_attackerKey) && context.Event.Args != null && context.Event.Args.TryGetValue(_attackerKey, out var aObj) && aObj != null)
            {
                attackerObj = aObj;
            }
            TriggerActionArgUtil.TryResolveActorId(attackerObj, out var attackerActorId);

            // Resolve target
            object targetObj = context.Target;
            if (!string.IsNullOrEmpty(_targetKey) && context.Event.Args != null && context.Event.Args.TryGetValue(_targetKey, out var tObj) && tObj != null)
            {
                targetObj = tObj;
            }

            // Optional: query template drives target list selection via SearchTargetService.
            if (_queryTemplateId > 0)
            {
                var search = context.Services?.GetService(typeof(SearchTargetService)) as SearchTargetService;
                if (search == null)
                {
                    Log.Warning("[Trigger] give_damage queryTemplateId provided but cannot resolve SearchTargetService from DI");
                    return;
                }

                TriggerActionArgUtil.TryResolveActorId(targetObj, out var explicitTargetActorId);

                var casterActorId = attackerActorId;
                var aimPos = default(AbilityKit.Ability.Share.Math.Vec3);
                if (!string.IsNullOrEmpty(_aimPosKey) && context.Event.Args != null && context.Event.Args.TryGetValue(_aimPosKey, out var ap) && ap is AbilityKit.Ability.Share.Math.Vec3 v3)
                {
                    aimPos = v3;
                }

                var list = _intListPool.Get();
                try
                {
                    if (!search.TrySearchActorIds(_queryTemplateId, casterActorId, in aimPos, explicitTargetActorId, list))
                    {
                        return;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        var targetActorId2 = list[i];
                        if (targetActorId2 <= 0) continue;

                        var attack2 = new AttackInfo
                        {
                            AttackerActorId = attackerActorId,
                            TargetActorId = targetActorId2,
                            DamageType = _damageType,
                            CritType = _critType,
                            ReasonKind = _reasonKind,
                            ReasonParam = _reasonParam,
                        };
                        attack2.BaseDamage.BaseValue = _value;
                        var result2 = pipeline.Execute(attack2);
                        if (_log)
                        {
                            var applied2 = result2 != null ? result2.Value : 0f;
                            var hp2 = result2 != null ? result2.TargetHp : 0f;
                            var maxHp2 = result2 != null ? result2.TargetMaxHp : 0f;
                            Log.Info($"[give_damage] attacker={attackerActorId} target={targetActorId2} base={_value:0.###} applied={applied2:0.###} hp={hp2:0.###}/{maxHp2:0.###} reason=({_reasonKind},{_reasonParam})");
                        }
                    }
                }
                finally
                {
                    _intListPool.Release(list);
                }

                return;
            }

            if (!TriggerActionArgUtil.TryResolveActorId(targetObj, out var targetActorId) || targetActorId <= 0)
            {
                Log.Warning("[Trigger] give_damage requires a valid target actorId");
                return;
            }

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

            var result = pipeline.Execute(attack);
            if (_log)
            {
                var applied = result != null ? result.Value : 0f;
                var hp = result != null ? result.TargetHp : 0f;
                var maxHp = result != null ? result.TargetMaxHp : 0f;
                Log.Info($"[give_damage] attacker={attackerActorId} target={targetActorId} base={_value:0.###} applied={applied:0.###} hp={hp:0.###}/{maxHp:0.###} reason=({_reasonKind},{_reasonParam})");
            }
        }
    }
}
