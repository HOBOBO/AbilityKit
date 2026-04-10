using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Share.Impl.Moba.Components
{
    [Actor]
    public sealed class ModelIdComponent : IComponent
    {
        public int Value;
    }
}
