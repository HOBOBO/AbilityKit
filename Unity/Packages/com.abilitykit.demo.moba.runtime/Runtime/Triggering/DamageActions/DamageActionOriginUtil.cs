using AbilityKit.Demo.Moba;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Demo.Moba.Triggering.DamageActions
{
    using AbilityKit.Demo.Moba;
    public static class DamageActionOriginUtil
    {
        public static void FillOrigin(TriggerContext context, AttackInfo attack, object fallbackOriginSource, object fallbackOriginTarget)
        {
            if (attack == null) return;

            if (context?.Event.Args != null)
            {
                var args = context.Event.Args;
                if (args.TryGetValue(EffectTriggering.Args.OriginSource, out var os) && os != null) attack.OriginSource = os;
                if (args.TryGetValue(EffectTriggering.Args.OriginTarget, out var ot) && ot != null) attack.OriginTarget = ot;
                if (args.TryGetValue(EffectTriggering.Args.OriginKind, out var ok) && ok is EffectSourceKind kind) attack.OriginKind = kind;
                if (args.TryGetValue(EffectTriggering.Args.OriginConfigId, out var oc) && oc is int cid) attack.OriginConfigId = cid;
                if (args.TryGetValue(EffectTriggering.Args.OriginContextId, out var octx) && octx is long rid) attack.OriginContextId = rid;
            }

            if (attack.OriginSource == null) attack.OriginSource = fallbackOriginSource;
            if (attack.OriginTarget == null) attack.OriginTarget = fallbackOriginTarget;
        }
    }
}