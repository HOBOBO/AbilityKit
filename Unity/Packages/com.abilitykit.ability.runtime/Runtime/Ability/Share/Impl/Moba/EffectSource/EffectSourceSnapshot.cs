using System;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Impl.Moba.EffectSource
{
    [Serializable]
    public readonly struct EffectSourceSnapshot
    {
        public readonly long ContextId;
        public readonly long RootId;
        public readonly long ParentId;

        public readonly EffectSourceKind Kind;
        public readonly int ConfigId;

        public readonly int SourceActorId;
        public readonly int TargetActorId;

        public readonly int CreatedFrame;
        public readonly int EndedFrame;

        public readonly EffectSourceEndReason EndReason;

        public EffectSourceSnapshot(
            long contextId,
            long rootId,
            long parentId,
            EffectSourceKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int createdFrame,
            int endedFrame,
            EffectSourceEndReason endReason)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
            ConfigId = configId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
        }

        public bool IsEnded => EndedFrame > 0;
    }
}
