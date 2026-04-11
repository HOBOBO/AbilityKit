using AbilityKit.Core.Common.AttributeSystem;
using AbilityKit.Ability.Share.ECS;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;

namespace AbilityKit.ECS
{
    // 使用完全限定名避免循环依赖
    using EffectContainer = AbilityKit.Ability.Share.Effect.EffectContainer;

    public interface IUnitFacade
    {
        EcsEntityId Id { get; }

        GameplayTagContainer Tags { get; }
        AttributeContext Attributes { get; }
        EffectContainer Effects { get; }
    }
}
