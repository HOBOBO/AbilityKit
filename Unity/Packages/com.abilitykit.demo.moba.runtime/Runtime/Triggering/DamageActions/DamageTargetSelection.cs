using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Pool;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Triggering.DamageActions
{
    public static class DamageTargetSelection
    {
        private static readonly ObjectPool<List<int>> _intListPool = Pools.GetPool(
            createFunc: () => new List<int>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        public static List<int> RentTargets(TriggerContext context, DamageActionSpec spec, int casterActorId, object explicitTargetObj)
        {
            var list = _intListPool.Get();

            if (spec == null)
            {
                return list;
            }

            var mode = spec.TargetMode;
            if (mode == DamageActionSpec.DamageTargetMode.Explicit && spec.QueryTemplateId > 0)
            {
                // Backward compatibility: old configs only set queryTemplateId.
                mode = DamageActionSpec.DamageTargetMode.QueryTemplate;
            }

            var tmpActorId = 0;
            object payload = null;
            if (context != null) payload = context.Event.Payload;

            switch (mode)
            {
                case DamageActionSpec.DamageTargetMode.Self:
                    if (casterActorId > 0) list.Add(casterActorId);
                    return list;

                case DamageActionSpec.DamageTargetMode.Source:
                    if (context != null && TriggerActionArgUtil.TryResolveActorId(context.Source, out tmpActorId) && tmpActorId > 0) list.Add(tmpActorId);
                    return list;

                case DamageActionSpec.DamageTargetMode.Target:
                    if (context != null && TriggerActionArgUtil.TryResolveActorId(context.Target, out tmpActorId) && tmpActorId > 0) list.Add(tmpActorId);
                    return list;

                case DamageActionSpec.DamageTargetMode.PayloadAttacker:
                    if (TryResolvePayloadAttackerActorId(payload, out tmpActorId) && tmpActorId > 0) list.Add(tmpActorId);
                    return list;

                case DamageActionSpec.DamageTargetMode.PayloadTarget:
                    if (TryResolvePayloadTargetActorId(payload, out tmpActorId) && tmpActorId > 0) list.Add(tmpActorId);
                    return list;

                case DamageActionSpec.DamageTargetMode.QueryTemplate:
                    break;

                case DamageActionSpec.DamageTargetMode.Explicit:
                    break;

                default:
                    Log.Warning($"[Trigger] give_damage unsupported targetMode={mode}");
                    return list;
            }

            if (mode == DamageActionSpec.DamageTargetMode.QueryTemplate)
            {
                var services = context != null ? context.Services : null;
                var search = services != null ? services.GetService(typeof(SearchTargetService)) as SearchTargetService : null;
                if (search == null)
                {
                    Log.Warning("[Trigger] give_damage queryTemplateId provided but cannot resolve SearchTargetService from DI");
                    return list;
                }

                if (spec.QueryTemplateId <= 0)
                {
                    Log.Warning("[Trigger] give_damage targetMode=QueryTemplate requires queryTemplateId>0");
                    return list;
                }

                TriggerActionArgUtil.TryResolveActorId(explicitTargetObj, out var explicitTargetActorId);

                var aimPos = default(Vec3);
                var args = context != null ? context.Event.Args : null;
                object ap;
                if (!string.IsNullOrEmpty(spec.AimPosKey) && args != null && args.TryGetValue(spec.AimPosKey, out ap) && ap is Vec3)
                {
                    aimPos = (Vec3)ap;
                }

                if (!search.TrySearchActorIds(spec.QueryTemplateId, casterActorId, in aimPos, explicitTargetActorId, list))
                {
                    list.Clear();
                }

                return list;
            }

            if (TriggerActionArgUtil.TryResolveActorId(explicitTargetObj, out var targetActorId) && targetActorId > 0)
            {
                list.Add(targetActorId);
            }

            return list;
        }

        public static void Release(List<int> list)
        {
            if (list == null) return;
            _intListPool.Release(list);
        }

        private static bool TryResolvePayloadAttackerActorId(object payload, out int attackerActorId)
        {
            attackerActorId = 0;
            if (payload is DamageResult dr)
            {
                attackerActorId = dr.AttackerActorId;
                return true;
            }

            if (payload is AttackCalcInfo ac && ac.Attack != null)
            {
                attackerActorId = ac.Attack.AttackerActorId;
                return true;
            }

            if (payload is AttackInfo ai)
            {
                attackerActorId = ai.AttackerActorId;
                return true;
            }

            return false;
        }

        private static bool TryResolvePayloadTargetActorId(object payload, out int targetActorId)
        {
            targetActorId = 0;
            if (payload is DamageResult dr)
            {
                targetActorId = dr.TargetActorId;
                return true;
            }

            if (payload is AttackCalcInfo ac && ac.Attack != null)
            {
                targetActorId = ac.Attack.TargetActorId;
                return true;
            }

            if (payload is AttackInfo ai)
            {
                targetActorId = ai.TargetActorId;
                return true;
            }

            return false;
        }
    }
}
