using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class LifetimeComponent : IComponent
    {
        public long EndTimeMs;
    }
}
