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
        public int CastConflictGroup;
        public int InterruptPriority;
        public bool InterruptRunning;
        public bool Uninterruptible;
        public int TargetLostPolicy;

        public int SkillButtonTemplateId;
        public int RequiredTargetQueryId;
        public bool AllowCastWithoutTarget;

        public int LevelTableId;
        public int PreCastFlowId;
        public int CastFlowId;
        public int PreviewVfxId;
        public ulong ExecutionBlockedResultMask;
    }

    public enum PointRangePolicy { Legacy = 0, ClampToResolvedRange = 1 }

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
        public PointRangePolicy PointRangePolicy;
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
        public int ChildChannelId;
        public int RecastChannelId;
        public bool QueueRecastInFlight;
        public bool ContinueThroughControl;
        public bool PreserveCharacterCastContext;
        public bool UseCommitCooldownFactor;
        public BasicAttackFlowDTO BasicAttack;

        public int RequiredBuffQualificationId;
        public SkillPhaseDTO[] Phases;
    }

    /// <summary>
    /// 单发普攻的槽位共享间隔及可选跨槽等级射程。
    /// </summary>
    [Serializable]
    public sealed class BasicAttackFlowDTO
    {
        public int RangeLevelSlot;
        public float[] RangeBonusByLevel;

        /// <summary>
        /// 解析作者配置的射程。未学习时只使用基础射程。
        /// </summary>
        public float ResolveRange(float baseRange, int level) =>
            RangeLevelSlot > 0 && level > 0 && RangeBonusByLevel?.Length > 0
                ? baseRange + RangeBonusByLevel[Math.Min(level, RangeBonusByLevel.Length) - 1]
                : baseRange;

        /// <summary>
        /// 校验单发及后摇交接结构，返回起手到发射的作者时间。
        /// </summary>
        public static int Validate(SkillFlowDTO flow)
        {
            var rules = flow.BasicAttack;
            if (rules == null) return 0;
            var curve = rules.RangeBonusByLevel;
            if (rules.RangeLevelSlot < 0 || (rules.RangeLevelSlot > 0) != (curve?.Length > 0))
                throw new ArgumentException("Basic attack range requires both a level slot and a nonempty curve.");
            if (curve != null)
                foreach (var value in curve)
                    if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
                        throw new ArgumentException("Basic attack range bonuses must be finite and nonnegative.");

            var phase = flow.Phases?.Length == 1 ? flow.Phases[0] : null;
            var timeline = phase?.Timeline;
            var launch = timeline?.Events?.Length == 1 ? timeline.Events[0] : null;
            if (phase?.Type != (int)SkillPhaseType.Timeline || launch == null || launch.AtMs <= 0 ||
                launch.EventTag != "basic_attack_launch" || timeline.RecoveryStartMs != launch.AtMs ||
                flow.ChildChannelId != 0 || flow.RecastChannelId != 0)
                throw new ArgumentException("Basic attack cadence requires a single launch timeline with handoff at its launch.");
            return launch.AtMs;
        }
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
        DerivedSkill = 20,
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
        public SkillDerivedSkillPhaseDTO DerivedSkill;
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
        public SkillEventCaptureDTO[] Captures;
    }

    public enum SkillEventCaptureScope
    {
        Cast = 0,
        Target = 1,
    }

    public enum SkillEventCaptureValueType
    {
        Integer = 0,
        Number = 1,
    }

    [Serializable]
    public sealed class SkillEventCaptureDTO
    {
        public int FieldId;
        public string Key;
        public int Scope;
        public int ValueType;
        public bool Required = true;
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
        public bool RequireEconomyReservation;
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
        // Separates the release predicate from the economy debit. When enabled,
        // the shared release condition accepts the cast after its configured
        // resource reservation even if the live resource was already debited.
        public bool IgnoreResourceCostCheck;
        // When enabled, the phase validates the cast but does not debit the
        // configured resource. The transaction still carries the resolved cost
        // so release predicates and rollback remain deterministic.
        public bool IgnoreResourceConsumption;
        public bool RefundBeforeCommit = true;
        public bool RequireExplicitCommit;
        public string FailReason;
    }

    [Serializable]
    public sealed class SkillDerivedSkillPhaseDTO
    {
        public int SkillId;
        public bool InheritAim = true;
        public bool InheritTarget = true;
        public bool WaitForCompletion;
        public bool AbortOnFailure = true;
        public int MaxDepth = 4;
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
        // -1 disables handoff. Measured from this terminal timeline's own start.
        public int RecoveryStartMs = -1;
        public int CastGateTagTemplateId;
        public int CastGateEndMs;
#if UNITY_5_3_OR_NEWER
        [UnityEngine.SerializeReference]
#endif
        public SkillTimelineWindupDTO Windup;
        public SkillTimelineEventDTO[] Events;
    }

    /// <summary>
    /// 以有效额外攻速百分值线性压缩指定的 Timeline 起始区间。
    /// </summary>
    [Serializable]
    public sealed class SkillTimelineWindupDTO
    {
        public int AuthoredMs;
        public int BaseMs;
        public int MinimumMs;
        public int AttackSpeedPercentAtMinimum;

        public void Validate(int durationMs)
        {
            if (AuthoredMs <= 0 || AuthoredMs >= durationMs || MinimumMs <= 0 ||
                BaseMs < MinimumMs || BaseMs > int.MaxValue - (durationMs - AuthoredMs) ||
                AttackSpeedPercentAtMinimum <= 0)
                throw new ArgumentException("Invalid Timeline windup range or authored boundary.");
        }
    }

    [Serializable]
    public sealed class SkillTimelineEventDTO
    {
        public int AtMs;
        public int EffectId;
        public int ExecuteMode;
        public bool AbortOnFailure;
        public string EventTag;
    }

    public static class SkillRecoveryContract
    {
        // Initial scope deliberately allows only a terminal top-level Timeline.
        // This proves that a handoff cannot skip later gameplay phases.
        public static void Validate(SkillFlowDTO flow)
        {
            var phases = flow?.Phases;
            if (phases == null) return;
            for (var i = 0; i < phases.Length; i++)
            {
                ValidatePhase(phases[i], i == phases.Length - 1);
                if (phases[i]?.Timeline?.Windup == null) continue;
                if (flow.BasicAttack != null) throw new ArgumentException("Timeline windup cannot replace basic attack timing.");
                for (var prior = 0; prior < i; prior++)
                    if (phases[prior]?.Type != (int)SkillPhaseType.RulePlan)
                        throw new ArgumentException("Timeline windup requires immediate entry after synchronous rules.");
            }
        }

        private static void ValidatePhase(SkillPhaseDTO phase, bool terminal)
        {
            if (phase == null) return;
            var timeline = phase.Timeline;
            if (timeline != null)
            {
                if (timeline.Windup != null)
                {
                    if (!terminal || phase.Type != (int)SkillPhaseType.Timeline)
                        throw new ArgumentException("Windup requires a terminal top-level Timeline.");
                    timeline.Windup.Validate(timeline.DurationMs);
                }
                ValidateTimelineGate(timeline.DurationMs, timeline.RecoveryStartMs,
                    timeline.CastGateTagTemplateId, timeline.CastGateEndMs);
                if (timeline.RecoveryStartMs < -1 || timeline.RecoveryStartMs >= 0 &&
                    (!terminal || phase.Type != (int)SkillPhaseType.Timeline ||
                     timeline.RecoveryStartMs >= timeline.DurationMs))
                    throw new ArgumentException("Recovery requires a terminal top-level Timeline and an entry before its full end.");
                if (timeline.RecoveryStartMs >= 0 && timeline.Events != null)
                    foreach (var effect in timeline.Events)
                        if (effect != null && effect.AtMs > timeline.RecoveryStartMs)
                            throw new ArgumentException("Recovery cannot precede an unsubmitted Timeline effect.");
            }
            if (phase.Children != null)
                foreach (var child in phase.Children) ValidatePhase(child, false);
            if (phase.Repeat?.Phase != null) ValidatePhase(phase.Repeat.Phase, false);
        }

        public static void ValidateTimelineGate(int durationMs, int recoveryStartMs, int templateId, int endMs)
        {
            if (templateId < 0 || (templateId == 0 ? endMs != 0 :
                endMs <= 0 || endMs > durationMs || recoveryStartMs >= 0 && endMs > recoveryStartMs))
                throw new ArgumentException("Cast gate requires a positive template and end within the Timeline, no later than recovery.");
        }
    }
}
