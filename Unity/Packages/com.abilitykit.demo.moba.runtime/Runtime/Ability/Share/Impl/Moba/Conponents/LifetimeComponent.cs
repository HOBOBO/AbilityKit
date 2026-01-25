using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class LifetimeComponent : IComponent
    {
        public long EndTimeMs;
    }
}
