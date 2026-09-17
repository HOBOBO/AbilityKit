using System.Collections.Generic;

namespace AbilityKit.Diagnostics.Analysis
{
    public static class AnalysisBattleDiagnosticSchema
    {
        public const string Version = "abilitykit-battle-diagnostics.v1";
    }

    public sealed class AnalysisBattleDiagnosticSection
    {
        public string SchemaVersion { get; set; } = AnalysisBattleDiagnosticSchema.Version;
        public long CapturedAtTimestamp { get; set; }
        public AnalysisBattleDiagnosticSession Session { get; set; } = new AnalysisBattleDiagnosticSession();
        public AnalysisBattleDiagnosticEventTrack Events { get; set; } = new AnalysisBattleDiagnosticEventTrack();
        public AnalysisBattleDiagnosticStateTrack State { get; set; } = new AnalysisBattleDiagnosticStateTrack();
        public AnalysisBattleDiagnosticTraceTrack Trace { get; set; } = new AnalysisBattleDiagnosticTraceTrack();
        public AnalysisBattleDiagnosticAttributeTrack Attributes { get; set; } = new AnalysisBattleDiagnosticAttributeTrack();
        public AnalysisBattleDiagnosticBuffTrack Buffs { get; set; } = new AnalysisBattleDiagnosticBuffTrack();
        public AnalysisBattleDiagnosticTagTrack Tags { get; set; } = new AnalysisBattleDiagnosticTagTrack();
        public AnalysisBattleDiagnosticEffectTrack Effects { get; set; } = new AnalysisBattleDiagnosticEffectTrack();
        public AnalysisBattleDiagnosticObjectTrack Objects { get; set; } = new AnalysisBattleDiagnosticObjectTrack();
        public AnalysisBattleDiagnosticDefinitionTrack Definitions { get; set; } = new AnalysisBattleDiagnosticDefinitionTrack();
        public AnalysisBattleDiagnosticMetricTrack FrameMetrics { get; set; } = new AnalysisBattleDiagnosticMetricTrack();
        public AnalysisBattleDiagnosticMetricProfile FrameMetricProfile { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticMetricProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string GameMode { get; set; } = string.Empty;
        public string NetworkMode { get; set; } = string.Empty;
        public string DeviceTier { get; set; } = string.Empty;
        public List<AnalysisBattleDiagnosticMetricThreshold> Thresholds { get; set; } =
            new List<AnalysisBattleDiagnosticMetricThreshold>();
    }

    public sealed class AnalysisBattleDiagnosticMetricThreshold
    {
        public string Metric { get; set; } = string.Empty;
        public double? WarningThreshold { get; set; }
        public double? CriticalThreshold { get; set; }
        public double? SuggestedMinimum { get; set; }
        public double? SuggestedMaximum { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string WorldId { get; set; } = string.Empty;
        public long WorldEpoch { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string BuildId { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public long MonotonicTimestampFrequency { get; set; }
        public long Capabilities { get; set; }
        public int ConnectionState { get; set; }
        public int CaptureState { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticEventTrack
    {
        public long Revision { get; set; }
        public AnalysisBattleDiagnosticStoreMetrics Metrics { get; set; } = new AnalysisBattleDiagnosticStoreMetrics();
        public List<AnalysisBattleDiagnosticEvent> Items { get; set; } = new List<AnalysisBattleDiagnosticEvent>();
    }

    public sealed class AnalysisBattleDiagnosticStoreMetrics
    {
        public int Capacity { get; set; }
        public int Count { get; set; }
        public long Revision { get; set; }
        public long AcceptedCount { get; set; }
        public long EvictedCount { get; set; }
        public long RejectedCount { get; set; }
        public bool IsFrozen { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticEvent
    {
        public int Frame { get; set; }
        public long Sequence { get; set; }
        public long MonotonicTimestamp { get; set; }
        public int Kind { get; set; }
        public int Channel { get; set; }
        public int Outcome { get; set; }
        public long SourceActorId { get; set; }
        public long TargetActorId { get; set; }
        public int SourceActorGeneration { get; set; }
        public int TargetActorGeneration { get; set; }
        public int SubjectObjectKind { get; set; }
        public long SubjectRuntimeId { get; set; }
        public int SubjectGeneration { get; set; }
        public int ConfigId { get; set; }
        public int DefinitionKind { get; set; }
        public long RootContextId { get; set; }
        public long ContextId { get; set; }
        public long SkillRuntimeId { get; set; }
        public int SkillRuntimeGeneration { get; set; }
        public long AttackId { get; set; }
        public int PayloadVersion { get; set; }
        public string Summary { get; set; } = string.Empty;
        public AnalysisBattleDiagnosticEventPayload Payload { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticEventPayload
    {
        public int Kind { get; set; }
        public int SchemaVersion { get; set; }
        public int DamageStage { get; set; }
        public long DamageBaseRaw { get; set; }
        public long DamageRawRaw { get; set; }
        public long DamageMitigatedRaw { get; set; }
        public long DamageShieldRaw { get; set; }
        public long DamagePlannedHpRaw { get; set; }
        public long DamageAppliedHpRaw { get; set; }
        public long InputCommandId { get; set; }
        public int InputFrame { get; set; }
        public string InputPlayerId { get; set; } = string.Empty;
        public int InputOpCode { get; set; }
        public bool InputSucceeded { get; set; }
        public int InputFailureCode { get; set; }
        public string InputMessage { get; set; } = string.Empty;
        public int InputSkillSlot { get; set; }
        public int InputSkillPhase { get; set; }
        public int InputTargetActorId { get; set; }
        public long TargetSearchCommandId { get; set; }
        public int TargetSearchExplicitTargetActorId { get; set; }
        public int TargetSearchCandidateCount { get; set; }
        public int TargetSearchEligibleCount { get; set; }
        public int TargetSearchSelectedCount { get; set; }
        public string TargetSearchSelectedActorIds { get; set; } = string.Empty;
        public string TargetSearchDecisionDetails { get; set; } = string.Empty;
        public long SkillExecutionCommandId { get; set; }
        public int SkillExecutionStage { get; set; }
        public int SkillExecutionSlot { get; set; }
        public int SkillExecutionLevel { get; set; }
        public int SkillExecutionSequence { get; set; }
        public int SkillExecutionEndReason { get; set; }
        public int SkillExecutionResourceType { get; set; }
        public long SkillExecutionResourceAmountRaw { get; set; }
        public long SkillExecutionResourceBeforeRaw { get; set; }
        public long SkillExecutionResourceAfterRaw { get; set; }
        public int SkillExecutionChargeCost { get; set; }
        public int SkillExecutionCooldownMs { get; set; }
        public int SkillExecutionSharedCooldownMs { get; set; }
        public int SkillExecutionGlobalCooldownMs { get; set; }
        public int SkillExecutionPendingChildren { get; set; }
        public bool SkillExecutionForced { get; set; }
        public string SkillExecutionDetail { get; set; } = string.Empty;
        public int AuthoritativeFrame { get; set; }
        public uint StateHash { get; set; }
        public int TriggerId { get; set; }
        public int TriggerContextKind { get; set; }
        public int TriggerOriginKind { get; set; }
        public int TriggerStage { get; set; }
        public int TriggerResult { get; set; }
        public int TriggerDetailCode { get; set; }
        public int TriggerCurrentDepth { get; set; }
        public int TriggerCurrentFrameCount { get; set; }
        public int TriggerCurrentRootCount { get; set; }
        public int TriggerCurrentSameTriggerCount { get; set; }
        public string TriggerFailureKey { get; set; } = string.Empty;
        public string TriggerReason { get; set; } = string.Empty;
        public int TriggerAggregateOccurrenceCount { get; set; }
        public int TriggerAggregateFirstFrame { get; set; }
        public int TriggerAggregateLastFrame { get; set; }
        public long TriggerAggregateFirstContextId { get; set; }
        public long TriggerAggregateLastContextId { get; set; }
        public long TriggerAggregateFirstRootContextId { get; set; }
        public long TriggerAggregateLastRootContextId { get; set; }
        public int SkillFailureSlot { get; set; }
        public string SkillFailureSource { get; set; } = string.Empty;
        public int BuffLifecycleStage { get; set; }
        public int BuffLifecycleStackCount { get; set; }
        public int BuffLifecyclePreviousStackCount { get; set; }
        public int BuffLifecycleDurationMilliseconds { get; set; }
        public int BuffLifecycleRemainingMilliseconds { get; set; }
        public int BuffLifecycleIntervalRemainingMilliseconds { get; set; }
        public int BuffLifecycleMaxStacks { get; set; }
        public int BuffLifecycleModifierBindingCount { get; set; }
        public int BuffLifecycleModifierSourceId { get; set; }
        public int BuffLifecycleRemoveReason { get; set; }
        public string SkillFailureStage { get; set; } = string.Empty;
        public string SkillFailureCode { get; set; } = string.Empty;
        public string SkillFailureMessage { get; set; } = string.Empty;
        public long SkillFailureCommandId { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticStateTrack
    {
        public long Revision { get; set; }
        public int Frame { get; set; } = -1;
        public AnalysisBattleDiagnosticWorld World { get; set; }
        public List<AnalysisBattleDiagnosticActor> Actors { get; set; } = new List<AnalysisBattleDiagnosticActor>();
    }

    public sealed class AnalysisBattleDiagnosticWorld
    {
        public int Frame { get; set; }
        public long MonotonicTimestamp { get; set; }
        public int ActorCount { get; set; }
        public int ActiveSkillRuntimeCount { get; set; }
        public int ActiveTraceRootCount { get; set; }
        public string StateHash { get; set; } = string.Empty;
    }

    public sealed class AnalysisBattleDiagnosticActor
    {
        public int Frame { get; set; }
        public long ActorId { get; set; }
        public int Kind { get; set; }
        public int ConfigId { get; set; }
        public int TeamId { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float Health { get; set; }
        public float MaximumHealth { get; set; }
        public bool IsAlive { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class AnalysisBattleDiagnosticTraceTrack
    {
        public long Revision { get; set; }
        public bool Truncated { get; set; }
        public bool IsStable { get; set; }
        public List<AnalysisBattleDiagnosticTraceNode> Nodes { get; set; } = new List<AnalysisBattleDiagnosticTraceNode>();
    }

    public sealed class AnalysisBattleDiagnosticTraceNode
    {
        public AnalysisBattleDiagnosticEffectExecutionFacts ExecutionFacts { get; set; }
        public AnalysisBattleDiagnosticActionExecutionFacts ActionFacts { get; set; }
        public AnalysisBattleDiagnosticTraceContextReference RootContext { get; set; }
        public AnalysisBattleDiagnosticTraceContextReference Context { get; set; }
        public AnalysisBattleDiagnosticTraceContextReference ParentContext { get; set; }
        public AnalysisBattleDiagnosticRuntimeObjectReference SourceObject { get; set; }
        public AnalysisBattleDiagnosticRuntimeObjectReference TargetObject { get; set; }
        public AnalysisBattleDiagnosticDefinitionReference Definition { get; set; }
        public AnalysisBattleDiagnosticDefinitionReference TriggerDefinition { get; set; }
        public AnalysisBattleDiagnosticDefinitionReference SkillDefinition { get; set; }
        public int OriginKind { get; set; }
        public AnalysisBattleDiagnosticDefinitionReference OriginDefinition { get; set; }
        public long RootContextId { get; set; }
        public long ContextId { get; set; }
        public long ParentContextId { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; } = -1;
        public int State { get; set; }
        public long ActorId { get; set; }
        public int SourceActorGeneration { get; set; }
        public long TargetActorId { get; set; }
        public int TargetActorGeneration { get; set; }
        public int TriggerId { get; set; }
        public int ConfigId { get; set; }
        public int DefinitionKind { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string EndReason { get; set; } = string.Empty;
        public int SkillId { get; set; }
        public int CastFlowId { get; set; }
        public string PhaseId { get; set; } = string.Empty;
    }

    public sealed class AnalysisBattleDiagnosticTraceContextReference
    {
        public long ContextId { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticActionActorValues
    {
        public long ActorId { get; set; }
        public long BindingId { get; set; }
        public bool HasActor { get; set; }
        public bool HasHp { get; set; }
        public float Hp { get; set; }
        public bool HasMana { get; set; }
        public float Mana { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticActionHealthCommit
    {
        public int Kind { get; set; }
        public long SourceActorId { get; set; }
        public long TargetActorId { get; set; }
        public int ValueType { get; set; }
        public int ReasonKind { get; set; }
        public int ReasonParam { get; set; }
        public float RequestedValue { get; set; }
        public float AppliedValue { get; set; }
        public float OldHp { get; set; }
        public float TargetHp { get; set; }
        public float TargetMaxHp { get; set; }
        public long OriginContextId { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticActionDamageResult
    {
        public long Sequence { get; set; }
        public int Frame { get; set; }
        public long SourceActorId { get; set; }
        public long TargetActorId { get; set; }
        public long OriginContextId { get; set; }
        public int Stage { get; set; }
        public long BaseDamageRaw { get; set; }
        public long RawDamageRaw { get; set; }
        public long MitigatedDamageRaw { get; set; }
        public long ShieldAbsorbRaw { get; set; }
        public long PlannedHpDamageRaw { get; set; }
        public long AppliedHpDamageRaw { get; set; }
        public string Detail { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticActionExecutionFacts
    {
        public int Availability { get; set; }
        public long SnapshotId { get; set; }
        public long Generation { get; set; }
        public int Frame { get; set; }
        public string TypeId { get; set; }
        public int SchemaVersion { get; set; }
        public int ActionIndex { get; set; }
        public long ActionId { get; set; }
        public int Outcome { get; set; }
        public bool HasAfter { get; set; }
        public int EndFrame { get; set; }
        public bool CommitsComplete { get; set; }
        public bool CommitsTruncated { get; set; }
        public AnalysisBattleDiagnosticActionActorValues SourceBefore { get; set; }
        public AnalysisBattleDiagnosticActionActorValues TargetBefore { get; set; }
        public AnalysisBattleDiagnosticActionActorValues SourceAfter { get; set; }
        public AnalysisBattleDiagnosticActionActorValues TargetAfter { get; set; }
        public List<AnalysisBattleDiagnosticActionHealthCommit> Commits { get; set; }
        public int DamageAvailability { get; set; } = 2;
        public bool DamageCoverageContinuous { get; set; }
        public bool DamageResultsTruncated { get; set; }
        public List<AnalysisBattleDiagnosticActionDamageResult> DamageResults { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticEffectExecutionFacts
    {
        public int Availability { get; set; } = 2;
        public long SnapshotId { get; set; }
        public long Generation { get; set; }
        public int Frame { get; set; }
        public string TypeId { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public int EffectConfigId { get; set; }
        public int TriggerId { get; set; }
        public string PayloadTypeName { get; set; } = string.Empty;
        public bool HasRuntimeContext { get; set; }
        public long RuntimeContextId { get; set; }
        public long RuntimeContextVersion { get; set; }
        public bool HasStageSnapshot { get; set; }
        public int StackCount { get; set; }
        public float ElapsedSeconds { get; set; }
        public float RemainingSeconds { get; set; }
        public float DurationSeconds { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticRuntimeObjectReference
    {
        public int Kind { get; set; }
        public long RuntimeId { get; set; }
        public int Generation { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticDefinitionReference
    {
        public int Kind { get; set; }
        public int DefinitionId { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticAttributeTrack
    {
        public long Revision { get; set; }
        public int Frame { get; set; } = -1;
        public List<AnalysisBattleDiagnosticAttribute> Items { get; set; } = new List<AnalysisBattleDiagnosticAttribute>();
        public List<AnalysisBattleDiagnosticAttributeModifier> Modifiers { get; set; } = new List<AnalysisBattleDiagnosticAttributeModifier>();
    }

    public sealed class AnalysisBattleDiagnosticAttribute
    {
        public int Frame { get; set; }
        public long ActorId { get; set; }
        public int AttributeId { get; set; }
        public float BaseValue { get; set; }
        public float FinalValue { get; set; }
        public int ModifierCount { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class AnalysisBattleDiagnosticAttributeModifier
    {
        public int Frame { get; set; }
        public long ActorId { get; set; }
        public int AttributeId { get; set; }
        public int Operation { get; set; }
        public float Magnitude { get; set; }
        public int Priority { get; set; }
        public int SourceId { get; set; }
        public int MagnitudeType { get; set; }
        public float DeclaredValue { get; set; }
        public float StackedValue { get; set; }
        public float ProjectedValue { get; set; }
        public float CurrentValue { get; set; }
        public bool HasCurrentValue { get; set; }
        public float CapturedValue { get; set; }
        public bool HasCapturedValue { get; set; }
        public int EvaluationPolicy { get; set; }
        public int StackCount { get; set; } = 1;
        public string CaptureMode { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }

    public sealed class AnalysisBattleDiagnosticBuffTrack
    {
        public long Revision { get; set; }
        public int Frame { get; set; } = -1;
        public List<AnalysisBattleDiagnosticBuff> Items { get; set; } = new List<AnalysisBattleDiagnosticBuff>();
    }

    public sealed class AnalysisBattleDiagnosticBuff
    {
        public int Frame { get; set; }
        public long ActorId { get; set; }
        public int BuffId { get; set; }
        public long SourceActorId { get; set; }
        public int StackCount { get; set; }
        public float RemainingSeconds { get; set; }
        public float IntervalRemainingSeconds { get; set; }
        public long SourceContextId { get; set; }
        public long RuntimeContextId { get; set; }
        public long RuntimeContextVersion { get; set; }
        public long SkillRuntimeId { get; set; }
        public int SkillRuntimeGeneration { get; set; }
        public long RootContextId { get; set; }
        public int ModifierBindingCount { get; set; }
        public int MaxStacks { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ModifierSourceId { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticTagTrack
    {
        public long Revision { get; set; }
        public int Frame { get; set; } = -1;
        public List<AnalysisBattleDiagnosticTag> Items { get; set; } = new List<AnalysisBattleDiagnosticTag>();
    }

    public sealed class AnalysisBattleDiagnosticTag
    {
        public int Frame { get; set; }
        public long ActorId { get; set; }
        public int TagId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class AnalysisBattleDiagnosticEffectTrack
    {
        public long Revision { get; set; }
        public int Frame { get; set; } = -1;
        public List<AnalysisBattleDiagnosticEffect> Items { get; set; } = new List<AnalysisBattleDiagnosticEffect>();
    }

    public sealed class AnalysisBattleDiagnosticEffect
    {
        public int Frame { get; set; }
        public long ActorId { get; set; }
        public int InstanceId { get; set; }
        public int DurationPolicy { get; set; }
        public int StackCount { get; set; }
        public float ElapsedSeconds { get; set; }
        public float RemainingSeconds { get; set; }
        public bool HasRemainingTime { get; set; }
        public float NextTickInSeconds { get; set; }
        public bool HasPeriodicTick { get; set; }
        public float DurationSeconds { get; set; }
        public float PeriodSeconds { get; set; }
        public int ComponentCount { get; set; }
        public bool ExecutePeriodicOnApply { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticMetricTrack
    {
        public long Revision { get; set; }
        public AnalysisBattleDiagnosticStoreMetrics Metrics { get; set; } = new AnalysisBattleDiagnosticStoreMetrics();
        public List<AnalysisBattleDiagnosticMetricSample> Items { get; set; } = new List<AnalysisBattleDiagnosticMetricSample>();
    }

    public sealed class AnalysisBattleDiagnosticMetricSample
    {
        public long Sequence { get; set; }
        public int Frame { get; set; }
        public long MonotonicTimestamp { get; set; }
        public int Category { get; set; }
        public int ValueKind { get; set; }
        public string Metric { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Dimension { get; set; } = string.Empty;
    }

    public sealed class AnalysisBattleDiagnosticObjectTrack
    {
        public long Revision { get; set; }
        public bool Truncated { get; set; }
        public int Completeness { get; set; }
        public long BackfillAttemptCount { get; set; }
        public long BackfillFailureCount { get; set; }
        public int LastBackfillFrame { get; set; } = -1;
        public AnalysisBattleDiagnosticObjectSummary Summary { get; set; } =
            new AnalysisBattleDiagnosticObjectSummary();
        public AnalysisBattleDiagnosticObjectEventCoverage EventCoverage { get; set; } =
            new AnalysisBattleDiagnosticObjectEventCoverage();
        public List<AnalysisBattleDiagnosticRuntimeObject> Items { get; set; } = new List<AnalysisBattleDiagnosticRuntimeObject>();
    }

    public sealed class AnalysisBattleDiagnosticDefinitionTrack
    {
        public long Revision { get; set; }
        public int UnresolvedCount { get; set; }
        public List<AnalysisBattleDiagnosticDefinition> Items { get; set; } =
            new List<AnalysisBattleDiagnosticDefinition>();
    }

    public sealed class AnalysisBattleDiagnosticDefinition
    {
        public int Kind { get; set; }
        public int DefinitionId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public int Resolution { get; set; }
        public List<AnalysisBattleDiagnosticDefinitionMetadataEntry> Metadata { get; set; } =
            new List<AnalysisBattleDiagnosticDefinitionMetadataEntry>();
    }

    public sealed class AnalysisBattleDiagnosticDefinitionMetadataEntry
    {
        public string Key { get; set; } = string.Empty;
        public int ValueKind { get; set; }
        public string StringValue { get; set; } = string.Empty;
        public long IntegerValue { get; set; }
        public double NumberValue { get; set; }
        public bool BooleanValue { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticObjectSummary
    {
        public int TotalCount { get; set; }
        public int CompleteCount { get; set; }
        public int PartialCount { get; set; }
        public int UnreliableCount { get; set; }
        public int ActiveCount { get; set; }
        public int EndedCount { get; set; }
        public int Completeness { get; set; }
        public bool Truncated { get; set; }
        public long BackfillAttemptCount { get; set; }
        public long BackfillFailureCount { get; set; }
        public int LastBackfillFrame { get; set; } = -1;
    }

    public sealed class AnalysisBattleDiagnosticObjectEventCoverage
    {
        public int EventCount { get; set; }
        public int ReferencedEventCount { get; set; }
        public int CompleteEventCount { get; set; }
        public int PartialEventCount { get; set; }
        public int UnreliableEventCount { get; set; }
        public int TotalReferenceCount { get; set; }
        public int ResolvedReferenceCount { get; set; }
        public int UnresolvedReferenceCount { get; set; }
        public float ResolvedReferenceRatio { get; set; }
    }

    public sealed class AnalysisBattleDiagnosticRuntimeObject
    {
        public int Kind { get; set; }
        public long RuntimeId { get; set; }
        public int Generation { get; set; }
        public int DefinitionKind { get; set; }
        public int DefinitionId { get; set; }
        public long RelatedActorId { get; set; }
        public long OwnerActorId { get; set; }
        public long SourceActorId { get; set; }
        public long TargetActorId { get; set; }
        public int CreatedFrame { get; set; } = -1;
        public int DestroyedFrame { get; set; } = -1;
        public long RootContextId { get; set; }
        public long ContextId { get; set; }
        public int State { get; set; }
        public int EndReason { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int DiscoveryKind { get; set; }
        public int BackfilledFrame { get; set; } = -1;
        public int Completeness { get; set; }
    }
}
