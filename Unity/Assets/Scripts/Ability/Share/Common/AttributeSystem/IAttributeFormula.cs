namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public interface IAttributeFormula
    {
        float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, in AttributeModifierSet modifiers);
    }
}
