using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class ModelIdComponent : IComponent
    {
        public int Value;
    }
}
