namespace AbilityKit.Ability.Share.Effect
{
    public interface IEffectComponent
    {
        void OnApply(in EffectExecutionContext context, EffectInstance instance);
        void OnTick(in EffectExecutionContext context, EffectInstance instance);
        void OnRemove(in EffectExecutionContext context, EffectInstance instance);
    }
}
