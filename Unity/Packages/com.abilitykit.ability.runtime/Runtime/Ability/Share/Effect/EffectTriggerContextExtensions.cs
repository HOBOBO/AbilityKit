using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Share.Effect
{
    public static class EffectTriggerContextExtensions
    {
        public static bool TryGetEffectInstance(this TriggerContext context, out EffectInstance instance)
        {
            instance = null;
            if (context == null) return false;
            return context.TryGetArg(EffectTriggering.Args.Instance, out instance) && instance != null;
        }

        public static bool TryGetEffectSpec(this TriggerContext context, out GameplayEffectSpec spec)
        {
            spec = null;
            if (context == null) return false;
            return context.TryGetArg(EffectTriggering.Args.Spec, out spec) && spec != null;
        }

        public static int GetEffectStackCountOrDefault(this TriggerContext context, int defaultValue = 0)
        {
            if (context != null && context.TryGetArg<int>(EffectTriggering.Args.StackCount, out var v)) return v;
            return defaultValue;
        }

        public static float GetEffectRemainingSecondsOrDefault(this TriggerContext context, float defaultValue = 0f)
        {
            if (context != null && context.TryGetArg<float>(EffectTriggering.Args.RemainingSeconds, out var v)) return v;
            return defaultValue;
        }
    }
}
