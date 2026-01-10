using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Server;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
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
