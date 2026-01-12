using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class ApplyBuffRequestComponent : IComponent
    {
        public int BuffId;
        public int SourceId;
        public int DurationOverrideMs;
    }
}
