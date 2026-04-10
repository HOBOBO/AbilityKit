using AbilityKit.Core.Common.AttributeSystem;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.ECS;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;

namespace AbilityKit.ECS
{
    public interface IUnitFacade
    {
        EcsEntityId Id { get; }

        GameplayTagContainer Tags { get; }
        AttributeContext Attributes { get; }
        EffectContainer Effects { get; }
    }
}
