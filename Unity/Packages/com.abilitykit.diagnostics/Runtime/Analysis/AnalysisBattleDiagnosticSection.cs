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
        public int ConfigId { get; set; }
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
        public long RootContextId { get; set; }
        public long ContextId { get; set; }
        public long ParentContextId { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; } = -1;
        public int State { get; set; }
        public long ActorId { get; set; }
        public int ConfigId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string EndReason { get; set; } = string.Empty;
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
}
