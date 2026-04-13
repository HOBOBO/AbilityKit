using AbilityKit.Core.Math;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    public enum SkillCastStage
    {
        PreCast = 1,
        Cast = 2,
        Channeling = 3,
        Completed = 4,
        Cancelled = 5,
        Failed = 6,
    }

    public enum SkillCancelReason
    {
        Unknown = 0,
        PlayerCancel = 1,
        Interrupted = 2,
        ReplacedByNewCast = 3,
        ValidationFailed = 4,
    }

    public enum SkillDestroyReason
    {
        Unknown = 0,
        CompletedRetainExpired = 1,
        CancelledRetainExpired = 2,
        FailedRetainExpired = 3,
        RollbackCleanup = 4,
    }

    [Actor]
    [PrimaryEntityIndex]
    public sealed class SkillCastInstanceIdComponent : IComponent
    {
        public long Value;
    }

    [Actor]
    public sealed class SkillCastOwnerActorIdComponent : IComponent
    {
        public int Value;
    }

    [Actor]
    public sealed class SkillCastSkillIdComponent : IComponent
    {
        public int Value;
    }

    [Actor]
    public sealed class SkillCastSlotComponent : IComponent
    {
        public int Value;
    }

    [Actor]
    public sealed class SkillCastSkillLevelComponent : IComponent
    {
        public int Value;
    }

    [Actor]
    public sealed class SkillCastSequenceComponent : IComponent
    {
        public int Value;
    }

    [Actor]
    public sealed class SkillCastStartFrameComponent : IComponent
    {
        public int Value;
    }

    [Actor]
    public sealed class SkillCastStageComponent : IComponent
    {
        public SkillCastStage Value;
    }

    [Actor]
    public sealed class SkillCastTargetActorIdComponent : IComponent
    {
        public int Value;
    }

    [Actor]
    public sealed class SkillCastAimComponent : IComponent
    {
        public Vec3 Pos;
        public Vec3 Dir;
    }

    [Actor]
    public sealed class SkillCastTimelineRuntimeComponent : IComponent
    {
        public int ElapsedMs;
        public int NextEventIndex;
    }

    [Actor]
    public sealed class SkillCastRunningTagComponent : IComponent
    {
    }

    [Actor]
    public sealed class SkillCastCancelRequestComponent : IComponent
    {
        public int Frame;
        public SkillCancelReason Reason;
    }

    [Actor]
    public sealed class SkillCastDestroyRequestComponent : IComponent
    {
        public int RequestFrame;
        public int MinConfirmedFrame;
        public SkillDestroyReason Reason;
    }
}
