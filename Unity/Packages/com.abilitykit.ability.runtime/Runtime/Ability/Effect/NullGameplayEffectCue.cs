namespace AbilityKit.Ability.Share.Effect
{
    using AbilityKit.Effect;
    
    public sealed class NullGameplayEffectCue : IGameplayEffectCue
    {
        public static readonly NullGameplayEffectCue Instance = new NullGameplayEffectCue();

        private NullGameplayEffectCue() { }

        public void OnActive(in EffectExecutionContext context, EffectInstance instance) { }
        public void WhileActive(in EffectExecutionContext context, EffectInstance instance) { }
        public void OnRemove(in EffectExecutionContext context, EffectInstance instance) { }
    }
}
