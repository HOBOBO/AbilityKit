using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    [Serializable]
    public sealed class SkillDTO
    {
        public int Id;
        public string Name;
        public int CooldownMs;
        public int Range;
        public int IconId;
        public int Category;
        public int SkillType;
        public int[] Tags;

        public int SkillButtonTemplateId;
        public int RequiredTargetQueryId;

        public int LevelTableId;
        public int PreCastFlowId;
        public int CastFlowId;
    }

    [Serializable]
    public sealed class SkillButtonTemplateDTO
    {
        public int Id;
        public string Name;

        public float LongPressSeconds;
        public float DragThreshold;
        public bool EnableAim;

        public int AimMode;
        public float AimMaxRadius;
        public int IndicatorShape;
        public float IndicatorWorldWidth;

        public int UsePointMode;
        public float SelectRange;
        public bool FaceToAim;

        // ---- Extended geometry fields (Sector / Dash / LockProjectile / Fan / SelfCircle) ----
        // All default to 0; consumers fall back to safe defaults when fields are unset.

        /// <summary>扇形指示器的中心角度（度）。</summary>
        public float SectorAngleDegrees;

        /// <summary>冲刺指示器的最大位移距离。</summary>
        public float DashDistance;

        /// <summary>锁定型投射指示器允许的瞄准时长（毫秒）。</summary>
        public int LockOnDurationMs;

        /// <summary>扇形技能指示器的半径。</summary>
        public float FanRadius;

        /// <summary>扇形技能指示器的中心角度（度）。</summary>
        public float FanAngleDegrees;

        /// <summary>以自身为中心技能指示器的半径。</summary>
        public float SelfRadius;

        /// <summary>锁定投射指示器的目标吸附半径。</summary>
        public float LockProjectileRadius;
    }

    [Serializable]
    public sealed class PassiveSkillDTO
    {
        public int Id;
        public string Name;
        public int CooldownMs;
        public int[] TriggerIds;
        public int[] ContinuousProcessIds;
    }

    [Serializable]
    public sealed class SkillFlowDTO
    {
        public int Id;
        public string Name;
        public int PipelineContinuousTagTemplateId;
        public SkillPhaseDTO[] Phases;
    }

    public enum SkillPhaseType
    {
        Checks = 1,
        Timeline = 2,
        Handlers = 3,
        RulePlan = 4,
        Sequence = 10,
        Parallel = 11,
        Repeat = 12,
        Delay = 13,
        WaitUntil = 14,
        Race = 15,
        AwaitEvent = 16,
        Window = 17,
        CommitPoint = 18,
        Economy = 19,
    }

    [Serializable]
    public sealed class SkillPhaseDTO
    {
        public int Type;
        public string PhaseId;
        public SkillChecksPhaseDTO Checks;
        public SkillTimelinePhaseDTO Timeline;
        public SkillFlowHandlerConfigDTO Handlers;
        public SkillRulePlanPhaseDTO RulePlan;
        public SkillPhaseDTO[] Children;
        public SkillRepeatPhaseDTO Repeat;
        public SkillDelayPhaseDTO Delay;
        public SkillWaitUntilPhaseDTO WaitUntil;
        public SkillAwaitEventPhaseDTO AwaitEvent;
        public SkillWindowPhaseDTO Window;
        public SkillCommitPointPhaseDTO CommitPoint;
        public SkillEconomyPhaseDTO Economy;
    }

    [Serializable]
    public sealed class SkillRulePlanPhaseDTO
    {
        public int[] TriggerIds;
        public bool AbortOnFailure = true;
        public string FailReason;
    }

    [Serializable]
    public sealed class SkillRepeatPhaseDTO
    {
        public int RepeatCount;
        public int IntervalMs;
        public SkillPhaseDTO Phase;
    }

    [Serializable]
    public sealed class SkillDelayPhaseDTO
    {
        public int DelayMs;
    }

    [Serializable]
    public sealed class SkillWaitUntilPhaseDTO
    {
        public string Condition;
        public int TimeoutMs;
        public bool CompleteOnTimeout = true;
        public int[] ObservedSlots;
        public SkillWaitConditionArgumentDTO[] Arguments;
    }

    [Serializable]
    public sealed class SkillWaitConditionArgumentDTO
    {
        public string Name;
        public string Value;
    }

    public enum SkillWindowKind
    {
        Timed = 0,
        Recast = 1,
        Charge = 2,
        Channel = 3,
    }

    [Serializable]
    public sealed class SkillAwaitEventPhaseDTO
    {
        public string EventId;
        public int TimeoutMs;
        public bool CompleteOnTimeout = true;
        public SkillEventIntFilterDTO[] Filters;
    }

    [Serializable]
    public sealed class SkillEventIntFilterDTO
    {
        public int FieldId;
        public int ExpectedValue;
        public bool UseCasterActorId;
        public bool UseTargetActorId;
        public bool UseSkillId;
    }

    [Serializable]
    public sealed class SkillWindowPhaseDTO
    {
        public string WindowId;
        public int Kind;
        public int DurationMs;
        public bool CompleteOnTimeout = true;
        public int ChannelIntervalMs;
        public int[] ChargeTierThresholdMs;
        public int[] OpenTriggerIds;
        public int[] TickTriggerIds;
        public int[] CloseTriggerIds;
        public bool AbortOnTriggerFailure = true;
        public string FailReason;
    }

    [Serializable]
    public sealed class SkillCommitPointPhaseDTO
    {
        public string CommitId;
    }

    public enum SkillEconomyOperation
    {
        ReserveCast = 0,
        ConsumeResource = 1,
    }

    [Serializable]
    public sealed class SkillEconomyPhaseDTO
    {
        public int Operation;
        public int ResourceType;
        public float ResourceAmount;
        public bool UseResolvedResourceCost = true;
        public int ChargeCost = 1;
        public int MaxCharges = 1;
        public int ChargeRecoveryMs;
        public bool StartSkillCooldown = true;
        public int SkillCooldownMs;
        public bool UseResolvedSkillCooldown = true;
        public string CooldownGroup;
        public int SharedCooldownMs;
        public int GlobalCooldownMs;
        public bool IgnoreGlobalCooldown;
        public bool RefundBeforeCommit = true;
        public string FailReason;
    }

    [Serializable]
    public sealed class SkillChecksPhaseDTO
    {
        public bool CheckCooldown;
        public bool CheckCastingState;
        public int[] RequiredTags;
        public int[] BlockedTags;
    }

    [Serializable]
    public sealed class SkillTimelinePhaseDTO
    {
        public int DurationMs;
        public SkillTimelineEventDTO[] Events;
    }

    [Serializable]
    public sealed class SkillTimelineEventDTO
    {
        public int AtMs;
        public int EffectId;
        public int ExecuteMode;
        public string EventTag;
    }
}
