using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Impl.Triggering.DamageActions
{
    public static class DamageContextResolver
    {
        public static object ResolveAttackerObj(TriggerContext context, DamageActionSpec spec)
        {
            object attackerObj = context?.Source;
            if (!string.IsNullOrEmpty(spec?.AttackerKey) && context?.Event.Args != null && context.Event.Args.TryGetValue(spec.AttackerKey, out var aObj) && aObj != null)
            {
                attackerObj = aObj;
            }
            return attackerObj;
        }

        public static object ResolveTargetObj(TriggerContext context, DamageActionSpec spec)
        {
            object targetObj = context?.Target;
            if (!string.IsNullOrEmpty(spec?.TargetKey) && context?.Event.Args != null && context.Event.Args.TryGetValue(spec.TargetKey, out var tObj) && tObj != null)
            {
                targetObj = tObj;
            }
            return targetObj;
        }
    }
}
