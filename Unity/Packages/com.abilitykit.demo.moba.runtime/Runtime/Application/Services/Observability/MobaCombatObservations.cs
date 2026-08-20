using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services.Observability
{
    public enum MobaTriggerAnalysisStage
    {
        Unknown = 0,
        Budget = 1,
        Conditions = 2,
        Plan = 3,
        Execution = 4,
    }

    public enum MobaTriggerAnalysisResult
    {
        Unknown = 0,
        Passed = 1,
        Failed = 2,
        Blocked = 3,
        Skipped = 4,
    }

    public enum MobaEffectLifecycleStage
    {
        Started = 1,
        Ended = 2,
    }

    public enum MobaBuffLifecycleStage
    {
        Applied = 1,
        Refreshed = 2,
        StackChanged = 3,
        Interval = 4,
        Removed = 5,
    }

    public enum MobaRuntimeObjectKind
    {
        Unknown = 0,
        Actor = 1,
        Projectile = 2,
        Area = 3,
        Summon = 4,
    }

    public enum MobaRuntimeObjectLifecycleStage
    {
        Created = 1,
        Destroyed = 2,
    }

    public enum MobaRuntimeObjectDefinitionKind
    {
        Unknown = 0,
        Skill = 1,
        Trigger = 2,
        Effect = 3,
        Buff = 4,
        Projectile = 5,
        Area = 6,
        Summon = 7,
        Actor = 8,
    }

    public readonly struct MobaTriggerAnalysisObservation
    {
        public MobaTriggerAnalysisObservation(
            int triggerId,
            int contextKind,
            int originKind,
            MobaTriggerAnalysisStage stage,
            MobaTriggerAnalysisResult result,
            int sourceActorId,
            int targetActorId,
            long contextId,
            long rootContextId,
            int detailCode = 0,
            int currentDepth = 0,
            int currentFrameCount = 0,
            int currentRootCount = 0,
            int currentSameTriggerCount = 0,
            string failureKey = "",
            string reason = "")
        {
            TriggerId = triggerId;
            ContextKind = contextKind;
            OriginKind = originKind;
            Stage = stage;
            Result = result;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ContextId = contextId;
            RootContextId = rootContextId;
            DetailCode = detailCode;
            CurrentDepth = currentDepth;
            CurrentFrameCount = currentFrameCount;
            CurrentRootCount = currentRootCount;
            CurrentSameTriggerCount = currentSameTriggerCount;
            FailureKey = failureKey ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public int TriggerId { get; }
        public int ContextKind { get; }
        public int OriginKind { get; }
        public MobaTriggerAnalysisStage Stage { get; }
        public MobaTriggerAnalysisResult Result { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long ContextId { get; }
        public long RootContextId { get; }
        public int DetailCode { get; }
        public int CurrentDepth { get; }
        public int CurrentFrameCount { get; }
        public int CurrentRootCount { get; }
        public int CurrentSameTriggerCount { get; }
        public string FailureKey { get; }
        public string Reason { get; }
    }

    public readonly struct MobaEffectLifecycleObservation
    {
        public MobaEffectLifecycleObservation(
            MobaEffectLifecycleStage stage,
            int effectConfigId,
            int triggerId,
            int sourceActorId,
            int targetActorId,
            long effectContextId,
            long rootContextId,
            bool succeeded = false)
        {
            Stage = stage;
            EffectConfigId = effectConfigId;
            TriggerId = triggerId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            EffectContextId = effectContextId;
            RootContextId = rootContextId;
            Succeeded = succeeded;
        }

        public MobaEffectLifecycleStage Stage { get; }
        public int EffectConfigId { get; }
        public int TriggerId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long EffectContextId { get; }
        public long RootContextId { get; }
        public bool Succeeded { get; }
    }

    public readonly struct MobaBuffLifecycleObservation
    {
        public MobaBuffLifecycleObservation(
            MobaBuffLifecycleStage stage,
            int buffId,
            int sourceActorId,
            int targetActorId,
            long rootContextId,
            long contextId,
            in MobaSkillCastRuntimeHandle skillRuntime,
            int stackCount,
            int previousStackCount,
            float durationSeconds,
            float remainingSeconds,
            float intervalRemainingSeconds,
            int maxStacks,
            int modifierBindingCount,
            int modifierSourceId,
            TraceLifecycleReason removeReason = TraceLifecycleReason.None)
        {
            Stage = stage;
            BuffId = buffId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            RootContextId = rootContextId;
            ContextId = contextId;
            SkillRuntime = skillRuntime;
            StackCount = stackCount;
            PreviousStackCount = previousStackCount;
            DurationSeconds = durationSeconds;
            RemainingSeconds = remainingSeconds;
            IntervalRemainingSeconds = intervalRemainingSeconds;
            MaxStacks = maxStacks;
            ModifierBindingCount = modifierBindingCount;
            ModifierSourceId = modifierSourceId;
            RemoveReason = removeReason;
        }

        public MobaBuffLifecycleStage Stage { get; }
        public int BuffId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long RootContextId { get; }
        public long ContextId { get; }
        public MobaSkillCastRuntimeHandle SkillRuntime { get; }
        public int StackCount { get; }
        public int PreviousStackCount { get; }
        public float DurationSeconds { get; }
        public float RemainingSeconds { get; }
        public float IntervalRemainingSeconds { get; }
        public int MaxStacks { get; }
        public int ModifierBindingCount { get; }
        public int ModifierSourceId { get; }
        public TraceLifecycleReason RemoveReason { get; }
    }

    public readonly struct MobaRuntimeObjectLifecycleObservation
    {
        public MobaRuntimeObjectLifecycleObservation(
            MobaRuntimeObjectLifecycleStage stage,
            MobaRuntimeObjectKind kind,
            long runtimeId,
            int frame,
            MobaRuntimeObjectDefinitionKind definitionKind = MobaRuntimeObjectDefinitionKind.Unknown,
            int definitionId = 0,
            long relatedActorId = 0L,
            long ownerActorId = 0L,
            long sourceActorId = 0L,
            long targetActorId = 0L,
            long rootContextId = 0L,
            long contextId = 0L,
            int endReason = 0,
            string displayName = "")
        {
            Stage = stage;
            Kind = kind;
            RuntimeId = runtimeId;
            Frame = frame;
            DefinitionKind = definitionKind;
            DefinitionId = definitionId;
            RelatedActorId = relatedActorId;
            OwnerActorId = ownerActorId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            RootContextId = rootContextId;
            ContextId = contextId;
            EndReason = endReason;
            DisplayName = displayName ?? string.Empty;
        }

        public MobaRuntimeObjectLifecycleStage Stage { get; }
        public MobaRuntimeObjectKind Kind { get; }
        public long RuntimeId { get; }
        public int Frame { get; }
        public MobaRuntimeObjectDefinitionKind DefinitionKind { get; }
        public int DefinitionId { get; }
        public long RelatedActorId { get; }
        public long OwnerActorId { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public long RootContextId { get; }
        public long ContextId { get; }
        public int EndReason { get; }
        public string DisplayName { get; }
    }

    public interface IMobaTriggerAnalysisHook
    {
        bool IsEnabled { get; }
        void OnObserved(in MobaTriggerAnalysisObservation observation);
    }

    public interface IMobaEffectLifecycleHook
    {
        bool IsEnabled { get; }
        void OnObserved(in MobaEffectLifecycleObservation observation);
    }

    public interface IMobaBuffLifecycleHook
    {
        bool IsEnabled { get; }
        void OnObserved(in MobaBuffLifecycleObservation observation);
    }

    public interface IMobaRuntimeObjectLifecycleHook
    {
        bool IsEnabled { get; }
        void OnObserved(in MobaRuntimeObjectLifecycleObservation observation);
    }

    public interface IMobaRuntimeObjectBootstrapContributor
    {
        void CaptureActiveRuntimeObjects(
            IMobaRuntimeObjectLifecycleHook hook,
            int frame);
    }

    public interface IMobaRuntimeObjectBootstrapRegistry
    {
        bool Register(IMobaRuntimeObjectBootstrapContributor contributor);
        void Unregister(IMobaRuntimeObjectBootstrapContributor contributor);
    }

    public static class MobaRuntimeObjectLifecycleHookExtensions
    {
        public static void TryObserve(
            this IMobaRuntimeObjectLifecycleHook hook,
            in MobaRuntimeObjectLifecycleObservation observation)
        {
            if (hook == null || !hook.IsEnabled) return;
            try
            {
                hook.OnObserved(in observation);
            }
            catch
            {
                // Observation callbacks must not affect gameplay lifecycle commits.
            }
        }
    }
}
