using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.Ability.Share.Common.TagSystem;
using AbilityKit.Ability.Share.Effect;

namespace AbilityKit.Ability.Share.ECS
{
    public interface IUnitFacade
    {
        EcsEntityId Id { get; }

        GameplayTagContainer Tags { get; }
        AttributeContext Attributes { get; }
        EffectContainer Effects { get; }
    }
}
