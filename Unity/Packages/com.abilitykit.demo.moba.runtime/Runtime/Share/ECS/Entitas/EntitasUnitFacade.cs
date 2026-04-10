using System;
using AbilityKit.Core.Common.AttributeSystem;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.ECS; using AbilityKit.ECS; using AbilityKit.Ability.Share.ECS;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;

namespace AbilityKit.Ability.Share.ECS.Entitas
{
    public sealed class EntitasUnitFacade : IUnitFacade
    {
        public EntitasUnitFacade(int actorId)
        {
            Id = new EcsEntityId(actorId);
            Tags = new GameplayTagContainer();
            Attributes = new AttributeContext();
            Effects = new EffectContainer();
        }

        public EcsEntityId Id { get; }

        public GameplayTagContainer Tags { get; }

        public AttributeContext Attributes { get; }

        public EffectContainer Effects { get; }
    }
}
