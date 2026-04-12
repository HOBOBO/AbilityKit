using AbilityKit.Attributes.Core;

namespace AbilityKit.Attributes.Constraint
{
    public interface IAttributeConstraint
    {
        float Apply(AttributeId id, float value);
    }
}
