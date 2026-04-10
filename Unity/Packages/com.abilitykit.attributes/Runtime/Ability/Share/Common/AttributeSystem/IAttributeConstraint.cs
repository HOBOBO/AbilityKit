namespace AbilityKit.Core.Common.AttributeSystem
{
    public interface IAttributeConstraint
    {
        float Apply(AttributeId id, float value);
    }
}
