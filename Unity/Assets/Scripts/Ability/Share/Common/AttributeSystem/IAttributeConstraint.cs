namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public interface IAttributeConstraint
    {
        float Apply(AttributeId id, float value);
    }
}
