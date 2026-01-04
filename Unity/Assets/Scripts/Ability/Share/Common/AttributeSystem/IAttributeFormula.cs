namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public interface IAttributeFormula
    {
        float Evaluate(float baseValue, AttributeModifierSet modifiers);
    }
}
