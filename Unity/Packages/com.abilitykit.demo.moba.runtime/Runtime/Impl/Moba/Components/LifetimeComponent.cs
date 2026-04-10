using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Share.Impl.Moba.Components
{
    [Actor]
    public sealed class LifetimeComponent : IComponent
    {
        public long EndTimeMs;
    }
}
