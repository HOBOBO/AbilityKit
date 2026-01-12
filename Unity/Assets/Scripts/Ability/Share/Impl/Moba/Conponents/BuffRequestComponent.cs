using Entitas;
using Entitas.CodeGeneration.Attributes;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class ApplyBuffRequestComponent : IComponent
    {
        public int BuffId;
        public int SourceId;
        public int DurationOverrideMs;
    }

    [Actor]
    public sealed class RemoveBuffRequestComponent : IComponent
    {
        public int BuffId;
        public int SourceId;
        public EffectSourceEndReason Reason;
    }
}
