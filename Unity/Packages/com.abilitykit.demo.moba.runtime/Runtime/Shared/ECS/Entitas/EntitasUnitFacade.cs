using System;
using AbilityKit.Attributes.Core;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;
using EffectContainer = AbilityKit.Ability.Share.Effect.EffectContainer;

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
