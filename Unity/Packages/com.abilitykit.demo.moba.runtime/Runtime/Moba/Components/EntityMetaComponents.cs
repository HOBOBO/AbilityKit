using AbilityKit.Demo.Moba;
using AbilityKit.Ability.Host;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class TeamComponent : IComponent
    {
        public Team Value;
    }

    [Actor]
    public sealed class EntityMainTypeComponent : IComponent
    {
        public EntityMainType Value;
    }

    [Actor]
    public sealed class UnitSubTypeComponent : IComponent
    {
        public UnitSubType Value;
    }

    [Actor]
    public sealed class OwnerPlayerIdComponent : IComponent
    {
        public PlayerId Value;
    }
}
