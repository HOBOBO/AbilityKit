using Entitas;
using Entitas.CodeGeneration.Attributes;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Impl.Moba.Components
{
    [Actor]
    public sealed class ApplyBuffRequestComponent : IComponent
    {
        public int BuffId;
        public int SourceId;
        public int DurationOverrideMs;
        public long ParentContextId;
        public int OriginSourceActorId;
        public int OriginTargetActorId;
    }

    [Actor]
    public sealed class RemoveBuffRequestComponent : IComponent
    {
        public int BuffId;
        public int SourceId;
        public EffectSourceEndReason Reason;
    }
}
