using System.Collections.Generic;
using AbilityKit.Attributes.Core;
using AbilityKit.Deterministic;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
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

    /// <summary>
    /// 资源当前值以 Q32.32 定点存储（raw long），帧回滚与状态哈希按 raw 值对账。
    /// float 只允许在表现/IO 边界经 <see cref="MobaResourceFixedConvert"/> 换算出现。
    /// </summary>
    public sealed class ResourceState
    {
        public Fixed64 Current;
        public Fixed64 LastMax;
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
