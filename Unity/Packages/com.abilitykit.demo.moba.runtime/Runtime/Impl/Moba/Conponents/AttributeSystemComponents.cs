using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class AttributeGroupComponent : IComponent
    {
        public AttributeGroup Group;
        public AttributeContext Ctx;
    }

    public enum ResourceType
    {
        None = 0,
        Hp,
        Mana,
        Rage,
        Energy,
        Ammo,
        ComboPoint,
    }

    public sealed class ResourceState
    {
        public float Current;
        public float LastMax;
        public AttributeId MaxAttribute;
    }

    public sealed class ResourceContainer
    {
        public Dictionary<ResourceType, ResourceState> Map;
    }

    [Actor]
    public sealed class ResourceContainerComponent : IComponent
    {
        public ResourceContainer Value;
        public bool Initialized;
    }
}
