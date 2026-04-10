namespace AbilityKit.Ability.Share.Effect
{
    using AbilityKit.Effect;
    
    public interface IGameplayEffectCue
    {
        void OnActive(in EffectExecutionContext context, EffectInstance instance);
        void WhileActive(in EffectExecutionContext context, EffectInstance instance);
        void OnRemove(in EffectExecutionContext context, EffectInstance instance);
    }
}
