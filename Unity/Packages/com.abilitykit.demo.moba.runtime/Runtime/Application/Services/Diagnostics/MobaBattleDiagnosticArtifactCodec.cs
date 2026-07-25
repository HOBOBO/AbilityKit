using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Diagnostics.Analysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaBattleDiagnosticArtifactException : Exception
    {
        public MobaBattleDiagnosticArtifactException(string errorCode, string message, Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode ?? string.Empty;
        }

        public string ErrorCode { get; }
    }

    public static class MobaBattleDiagnosticArtifactCodec
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static AbilityKitAnalysisArtifact Attach(
            AbilityKitAnalysisArtifact artifact,
            BattleDiagnosticSessionSnapshot snapshot)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            artifact.BattleDiagnostics = ToSection(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
            return artifact;
        }

        public static string ExportSnapshotToString(BattleDiagnosticSessionSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return ExportToString(Attach(new AbilityKitAnalysisArtifact(), snapshot));
        }

        public static string ExportToString(AbilityKitAnalysisArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (string.IsNullOrEmpty(artifact.SchemaVersion)) artifact.SchemaVersion = AbilityKitAnalysisSchema.Version;
            return JsonConvert.SerializeObject(artifact, Settings);
        }

        public static AbilityKitAnalysisArtifact ImportArtifact(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new MobaBattleDiagnosticArtifactException("Artifact.Empty", "Analysis artifact JSON is empty.");
            try
            {
                var root = JObject.Parse(json);
                var schemaVersion = (string)(root["schemaVersion"] ?? root["SchemaVersion"]);
                if (!string.Equals(schemaVersion, AbilityKitAnalysisSchema.Version, StringComparison.Ordinal))
                    throw new MobaBattleDiagnosticArtifactException("Artifact.SchemaVersion", "Unsupported analysis artifact schema version: " + (schemaVersion ?? "<missing>"));

                var artifact = root.ToObject<AbilityKitAnalysisArtifact>(JsonSerializer.Create(Settings));
                if (artifact == null)
                    throw new MobaBattleDiagnosticArtifactException("Artifact.Invalid", "Analysis artifact could not be deserialized.");
                if (artifact.BattleDiagnostics != null &&
                    !string.Equals(artifact.BattleDiagnostics.SchemaVersion, AnalysisBattleDiagnosticSchema.Version, StringComparison.Ordinal))
                    throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.SchemaVersion", "Unsupported battle diagnostics section schema version: " + (artifact.BattleDiagnostics.SchemaVersion ?? "<missing>"));
                return artifact;
            }
            catch (MobaBattleDiagnosticArtifactException) { throw; }
            catch (JsonException ex)
            {
                throw new MobaBattleDiagnosticArtifactException("Artifact.MalformedJson", "Analysis artifact JSON is malformed.", ex);
            }
        }

        public static BattleDiagnosticSessionSnapshot ImportSnapshot(string json)
        {
            var artifact = ImportArtifact(json);
            if (artifact.BattleDiagnostics == null)
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Missing", "Analysis artifact does not contain a battleDiagnostics section.");
            return FromSection(artifact.BattleDiagnostics);
        }

        public static AnalysisBattleDiagnosticSection ToSection(BattleDiagnosticSessionSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var info = snapshot.SessionInfo;
            var metrics = snapshot.Events.Metrics;
            var section = new AnalysisBattleDiagnosticSection
            {
                CapturedAtTimestamp = snapshot.CapturedAtTimestamp,
                Session = new AnalysisBattleDiagnosticSession
                {
                    SessionId = info.Scope.SessionId,
                    WorldId = info.Scope.WorldId,
                    WorldEpoch = info.Scope.WorldEpoch,
                    DisplayName = info.DisplayName,
                    BuildId = info.BuildId,
                    SchemaVersion = info.SchemaVersion,
                    MonotonicTimestampFrequency = info.MonotonicTimestampFrequency,
                    Capabilities = (long)info.Capabilities,
                    ConnectionState = (int)info.ConnectionState,
                    CaptureState = (int)info.CaptureState
                },
                Events = new AnalysisBattleDiagnosticEventTrack
                {
                    Revision = snapshot.Events.Revision,
                    Metrics = new AnalysisBattleDiagnosticStoreMetrics
                    {
                        Capacity = metrics.Capacity,
                        Count = metrics.Count,
                        Revision = metrics.Revision,
                        AcceptedCount = metrics.AcceptedCount,
                        EvictedCount = metrics.EvictedCount,
                        RejectedCount = metrics.RejectedCount,
                        IsFrozen = metrics.IsFrozen
                    }
                },
                State = new AnalysisBattleDiagnosticStateTrack { Revision = snapshot.State.Revision, Frame = snapshot.State.Frame },
                Trace = new AnalysisBattleDiagnosticTraceTrack { Revision = snapshot.Trace.Revision, Truncated = snapshot.Trace.Truncated, IsStable = snapshot.Trace.IsStable },
                Attributes = new AnalysisBattleDiagnosticAttributeTrack { Revision = snapshot.Attributes.Revision, Frame = snapshot.Attributes.Frame },
                Buffs = new AnalysisBattleDiagnosticBuffTrack { Revision = snapshot.Buffs.Revision, Frame = snapshot.Buffs.Frame },
                Tags = new AnalysisBattleDiagnosticTagTrack { Revision = snapshot.Tags.Revision, Frame = snapshot.Tags.Frame },
                Effects = new AnalysisBattleDiagnosticEffectTrack { Revision = snapshot.Effects.Revision, Frame = snapshot.Effects.Frame }
            };

            if (snapshot.State.World.HasValue) section.State.World = ToDto(snapshot.State.World.Value);
            Copy(snapshot.Events.Events, section.Events.Items, ToDto);
            Copy(snapshot.State.Actors, section.State.Actors, ToDto);
            Copy(snapshot.Trace.Nodes, section.Trace.Nodes, ToDto);
            Copy(snapshot.Attributes.Attributes, section.Attributes.Items, ToDto);
            Copy(snapshot.Attributes.Modifiers, section.Attributes.Modifiers, ToDto);
            Copy(snapshot.Buffs.Items, section.Buffs.Items, ToDto);
            Copy(snapshot.Tags.Items, section.Tags.Items, ToDto);
            Copy(snapshot.Effects.Items, section.Effects.Items, ToDto);
            return section;
        }

        public static BattleDiagnosticSessionSnapshot FromSection(AnalysisBattleDiagnosticSection section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            try
            {
                ValidateSection(section);
                var session = section.Session;
                var scope = new BattleDiagnosticSessionScope(session.SessionId, session.WorldId, session.WorldEpoch);
                var info = new BattleDiagnosticSessionInfo(
                    scope, session.DisplayName, session.BuildId, session.SchemaVersion,
                    session.MonotonicTimestampFrequency, (BattleDiagnosticCapabilities)session.Capabilities,
                    (BattleDiagnosticConnectionState)session.ConnectionState, (BattleDiagnosticCaptureState)session.CaptureState);
                var metrics = section.Events.Metrics;
                var storeMetrics = new BattleDiagnosticStoreMetrics(metrics.Capacity, metrics.Count, metrics.Revision, metrics.AcceptedCount, metrics.EvictedCount, metrics.RejectedCount, metrics.IsFrozen);
                var events = Convert(section.Events.Items, item => FromDto(item, scope));
                var actors = Convert(section.State.Actors, item => FromDto(item, scope));
                var traces = Convert(section.Trace.Nodes, item => FromDto(item, scope));
                var attributes = Convert(section.Attributes.Items, item => FromDto(item, scope));
                var modifiers = Convert(section.Attributes.Modifiers, item => FromDto(item, scope));
                var buffs = Convert(section.Buffs.Items, item => FromDto(item, scope));
                var tags = Convert(section.Tags.Items, item => FromDto(item, scope));
                var effects = Convert(section.Effects.Items, item => FromDto(item, scope));
                BattleDiagnosticWorldSummary? world = section.State.World == null ? (BattleDiagnosticWorldSummary?)null : FromDto(section.State.World, scope);
                ValidateConsistency(section, scope, events, actors);
                return new BattleDiagnosticSessionSnapshot(
                    in info,
                    section.CapturedAtTimestamp,
                    new BattleDiagnosticEventTrackSnapshot(section.Events.Revision, in storeMetrics, events),
                    new BattleDiagnosticStateTrackSnapshot(section.State.Revision, section.State.Frame, world, actors),
                    new BattleDiagnosticTraceTrackSnapshot(section.Trace.Revision, traces, section.Trace.Truncated, section.Trace.IsStable),
                    new BattleDiagnosticAttributeTrackSnapshot(section.Attributes.Revision, section.Attributes.Frame, attributes, modifiers),
                    new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff>(section.Buffs.Revision, section.Buffs.Frame, buffs),
                    new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag>(section.Tags.Revision, section.Tags.Frame, tags),
                    new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect>(section.Effects.Revision, section.Effects.Frame, effects));
            }
            catch (MobaBattleDiagnosticArtifactException) { throw; }
            catch (Exception ex)
            {
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Invalid", "Battle diagnostics section failed domain validation.", ex);
            }
        }

        private static void ValidateSection(AnalysisBattleDiagnosticSection section)
        {
            if (!string.Equals(section.SchemaVersion, AnalysisBattleDiagnosticSchema.Version, StringComparison.Ordinal))
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.SchemaVersion", "Unsupported battle diagnostics section schema version.");
            if (section.Session == null || section.Events == null || section.State == null || section.Trace == null || section.Attributes == null || section.Buffs == null || section.Tags == null || section.Effects == null)
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.RequiredTrack", "Battle diagnostics section is missing a required track.");
            if (section.Events.Metrics == null)
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.EventMetrics", "Battle diagnostics event metrics are missing.");
            EnsureLists(section);
        }

        private static void EnsureLists(AnalysisBattleDiagnosticSection section)
        {
            section.Events.Items = section.Events.Items ?? new List<AnalysisBattleDiagnosticEvent>();
            section.State.Actors = section.State.Actors ?? new List<AnalysisBattleDiagnosticActor>();
            section.Trace.Nodes = section.Trace.Nodes ?? new List<AnalysisBattleDiagnosticTraceNode>();
            section.Attributes.Items = section.Attributes.Items ?? new List<AnalysisBattleDiagnosticAttribute>();
            section.Attributes.Modifiers = section.Attributes.Modifiers ?? new List<AnalysisBattleDiagnosticAttributeModifier>();
            section.Buffs.Items = section.Buffs.Items ?? new List<AnalysisBattleDiagnosticBuff>();
            section.Tags.Items = section.Tags.Items ?? new List<AnalysisBattleDiagnosticTag>();
            section.Effects.Items = section.Effects.Items ?? new List<AnalysisBattleDiagnosticEffect>();
        }

        private static void ValidateConsistency(AnalysisBattleDiagnosticSection section, BattleDiagnosticSessionScope scope, List<BattleDiagnosticEvent> events, List<BattleDiagnosticActorSummary> actors)
        {
            if (!scope.IsValid) throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Scope", "Battle diagnostics session scope is invalid.");
            if (section.CapturedAtTimestamp < 0) throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Timestamp", "Captured timestamp cannot be negative.");
            if (section.Events.Revision != section.Events.Metrics.Revision || section.Events.Metrics.Count != events.Count)
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.EventMetrics", "Event track revision/count does not match its metrics.");
            for (var i = 1; i < events.Count; i++)
                if (events[i].Sequence <= events[i - 1].Sequence) throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.EventSequence", "Event sequences must be strictly increasing.");
            if (section.State.World != null && section.State.World.ActorCount != actors.Count)
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.ActorCount", "World actor count does not match the actor list.");
        }

        private static AnalysisBattleDiagnosticEvent ToDto(BattleDiagnosticEvent item)
        {
            var result = new AnalysisBattleDiagnosticEvent { Frame = item.Frame, Sequence = item.Sequence, MonotonicTimestamp = item.MonotonicTimestamp, Kind = (int)item.Kind, Channel = (int)item.Channel, Outcome = (int)item.Outcome, SourceActorId = item.SourceActorId, TargetActorId = item.TargetActorId, ConfigId = item.ConfigId, RootContextId = item.RootContextId, ContextId = item.ContextId, SkillRuntimeId = item.SkillRuntime.RuntimeId, SkillRuntimeGeneration = item.SkillRuntime.Generation, AttackId = item.AttackId, PayloadVersion = item.PayloadVersion, Summary = item.Summary };
            if (item.Payload.TryGetSyncSnapshotReceived(out var payload)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, AuthoritativeFrame = payload.AuthoritativeFrame, StateHash = payload.StateHash };
            else if (item.Payload.TryGetTriggerAnalysis(out var trigger)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, TriggerId = trigger.TriggerId, TriggerContextKind = trigger.ContextKind, TriggerOriginKind = trigger.OriginKind, TriggerStage = (int)trigger.Stage, TriggerResult = (int)trigger.Result, TriggerDetailCode = trigger.DetailCode, TriggerCurrentDepth = trigger.CurrentDepth, TriggerCurrentFrameCount = trigger.CurrentFrameCount, TriggerCurrentRootCount = trigger.CurrentRootCount, TriggerCurrentSameTriggerCount = trigger.CurrentSameTriggerCount, TriggerFailureKey = trigger.FailureKey, TriggerReason = trigger.Reason };
            return result;
        }

        private static BattleDiagnosticEvent FromDto(AnalysisBattleDiagnosticEvent item, BattleDiagnosticSessionScope scope)
        {
            var payload = BattleDiagnosticEventPayload.None;
            if (item.Payload != null)
            {
                if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.SyncSnapshotReceived && item.Payload.SchemaVersion == BattleDiagnosticSyncSnapshotReceivedPayload.CurrentSchemaVersion)
                {
                    var sync = new BattleDiagnosticSyncSnapshotReceivedPayload(item.Payload.AuthoritativeFrame, item.Payload.StateHash);
                    payload = BattleDiagnosticEventPayload.FromSyncSnapshotReceived(in sync);
                }
                else if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.TriggerAnalysis && item.Payload.SchemaVersion == BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion)
                {
                    var trigger = new BattleDiagnosticTriggerAnalysisPayload(
                        item.Payload.TriggerId,
                        item.Payload.TriggerContextKind,
                        item.Payload.TriggerOriginKind,
                        (BattleDiagnosticTriggerAnalysisStage)item.Payload.TriggerStage,
                        (BattleDiagnosticTriggerAnalysisResult)item.Payload.TriggerResult,
                        item.Payload.TriggerDetailCode,
                        item.Payload.TriggerCurrentDepth,
                        item.Payload.TriggerCurrentFrameCount,
                        item.Payload.TriggerCurrentRootCount,
                        item.Payload.TriggerCurrentSameTriggerCount,
                        item.Payload.TriggerFailureKey,
                        item.Payload.TriggerReason);
                    payload = BattleDiagnosticEventPayload.FromTriggerAnalysis(in trigger);
                }
                else
                {
                    throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Payload", "Unsupported battle diagnostic event payload.");
                }
            }
            return new BattleDiagnosticEvent(scope, item.Frame, item.Sequence, item.MonotonicTimestamp, (BattleDiagnosticEventKind)item.Kind, (BattleDiagnosticEventChannel)item.Channel, (BattleDiagnosticEventOutcome)item.Outcome, item.SourceActorId, item.TargetActorId, item.ConfigId, item.RootContextId, item.ContextId, new BattleDiagnosticRuntimeHandle(item.SkillRuntimeId, item.SkillRuntimeGeneration), item.AttackId, item.PayloadVersion, item.Summary, payload);
        }

        private static AnalysisBattleDiagnosticWorld ToDto(BattleDiagnosticWorldSummary x) => new AnalysisBattleDiagnosticWorld { Frame = x.Frame, MonotonicTimestamp = x.MonotonicTimestamp, ActorCount = x.ActorCount, ActiveSkillRuntimeCount = x.ActiveSkillRuntimeCount, ActiveTraceRootCount = x.ActiveTraceRootCount, StateHash = x.StateHash };
        private static BattleDiagnosticWorldSummary FromDto(AnalysisBattleDiagnosticWorld x, BattleDiagnosticSessionScope s) => new BattleDiagnosticWorldSummary(s, x.Frame, x.MonotonicTimestamp, x.ActorCount, x.ActiveSkillRuntimeCount, x.ActiveTraceRootCount, x.StateHash);
        private static AnalysisBattleDiagnosticActor ToDto(BattleDiagnosticActorSummary x) => new AnalysisBattleDiagnosticActor { Frame = x.Frame, ActorId = x.ActorId, Kind = (int)x.Kind, ConfigId = x.ConfigId, TeamId = x.TeamId, PositionX = x.PositionX, PositionY = x.PositionY, PositionZ = x.PositionZ, Health = x.Health, MaximumHealth = x.MaximumHealth, IsAlive = x.IsAlive, DisplayName = x.DisplayName };
        private static BattleDiagnosticActorSummary FromDto(AnalysisBattleDiagnosticActor x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorSummary(s, x.Frame, x.ActorId, (BattleDiagnosticActorKind)x.Kind, x.ConfigId, x.TeamId, x.PositionX, x.PositionY, x.PositionZ, x.Health, x.MaximumHealth, x.IsAlive, x.DisplayName);
        private static AnalysisBattleDiagnosticTraceNode ToDto(BattleDiagnosticTraceNodeSummary x) => new AnalysisBattleDiagnosticTraceNode { RootContextId = x.RootContextId, ContextId = x.ContextId, ParentContextId = x.ParentContextId, StartFrame = x.StartFrame, EndFrame = x.EndFrame, State = (int)x.State, ActorId = x.ActorId, ConfigId = x.ConfigId, Kind = x.Kind, EndReason = x.EndReason };
        private static BattleDiagnosticTraceNodeSummary FromDto(AnalysisBattleDiagnosticTraceNode x, BattleDiagnosticSessionScope s) => new BattleDiagnosticTraceNodeSummary(s, x.RootContextId, x.ContextId, x.ParentContextId, x.StartFrame, x.EndFrame, (BattleDiagnosticTraceNodeState)x.State, x.ActorId, x.ConfigId, x.Kind, x.EndReason);
        private static AnalysisBattleDiagnosticAttribute ToDto(BattleDiagnosticActorAttribute x) => new AnalysisBattleDiagnosticAttribute { Frame = x.Frame, ActorId = x.ActorId, AttributeId = x.AttributeId, BaseValue = x.BaseValue, FinalValue = x.FinalValue, ModifierCount = x.ModifierCount, Name = x.Name };
        private static BattleDiagnosticActorAttribute FromDto(AnalysisBattleDiagnosticAttribute x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorAttribute(s, x.Frame, x.ActorId, x.AttributeId, x.BaseValue, x.FinalValue, x.ModifierCount, x.Name);
        private static AnalysisBattleDiagnosticAttributeModifier ToDto(BattleDiagnosticActorAttributeModifier x) => new AnalysisBattleDiagnosticAttributeModifier { Frame = x.Frame, ActorId = x.ActorId, AttributeId = x.AttributeId, Operation = x.Operation, Magnitude = x.Magnitude, Priority = x.Priority, SourceId = x.SourceId, MagnitudeType = x.MagnitudeType };
        private static BattleDiagnosticActorAttributeModifier FromDto(AnalysisBattleDiagnosticAttributeModifier x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorAttributeModifier(s, x.Frame, x.ActorId, x.AttributeId, x.Operation, x.Magnitude, x.Priority, x.SourceId, x.MagnitudeType);
        private static AnalysisBattleDiagnosticBuff ToDto(BattleDiagnosticActorBuff x) => new AnalysisBattleDiagnosticBuff { Frame = x.Frame, ActorId = x.ActorId, BuffId = x.BuffId, SourceActorId = x.SourceActorId, StackCount = x.StackCount, RemainingSeconds = x.RemainingSeconds, IntervalRemainingSeconds = x.IntervalRemainingSeconds, SourceContextId = x.SourceContextId, RuntimeContextId = x.RuntimeContextId, RuntimeContextVersion = x.RuntimeContextVersion, SkillRuntimeId = x.SkillRuntime.RuntimeId, SkillRuntimeGeneration = x.SkillRuntime.Generation, RootContextId = x.RootContextId, ModifierBindingCount = x.ModifierBindingCount, MaxStacks = x.MaxStacks, Name = x.Name };
        private static BattleDiagnosticActorBuff FromDto(AnalysisBattleDiagnosticBuff x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorBuff(s, x.Frame, x.ActorId, x.BuffId, x.SourceActorId, x.StackCount, x.RemainingSeconds, x.IntervalRemainingSeconds, x.SourceContextId, x.RuntimeContextId, x.RuntimeContextVersion, new BattleDiagnosticRuntimeHandle(x.SkillRuntimeId, x.SkillRuntimeGeneration), x.RootContextId, x.ModifierBindingCount, x.MaxStacks, x.Name);
        private static AnalysisBattleDiagnosticTag ToDto(BattleDiagnosticActorTag x) => new AnalysisBattleDiagnosticTag { Frame = x.Frame, ActorId = x.ActorId, TagId = x.TagId, Name = x.Name };
        private static BattleDiagnosticActorTag FromDto(AnalysisBattleDiagnosticTag x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorTag(s, x.Frame, x.ActorId, x.TagId, x.Name);
        private static AnalysisBattleDiagnosticEffect ToDto(BattleDiagnosticActorEffect x) => new AnalysisBattleDiagnosticEffect { Frame = x.Frame, ActorId = x.ActorId, InstanceId = x.InstanceId, DurationPolicy = (int)x.DurationPolicy, StackCount = x.StackCount, ElapsedSeconds = x.ElapsedSeconds, RemainingSeconds = x.RemainingSeconds, HasRemainingTime = x.HasRemainingTime, NextTickInSeconds = x.NextTickInSeconds, HasPeriodicTick = x.HasPeriodicTick, DurationSeconds = x.DurationSeconds, PeriodSeconds = x.PeriodSeconds, ComponentCount = x.ComponentCount, ExecutePeriodicOnApply = x.ExecutePeriodicOnApply };
        private static BattleDiagnosticActorEffect FromDto(AnalysisBattleDiagnosticEffect x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorEffect(s, x.Frame, x.ActorId, x.InstanceId, (BattleDiagnosticEffectDurationPolicy)x.DurationPolicy, x.StackCount, x.ElapsedSeconds, x.RemainingSeconds, x.HasRemainingTime, x.NextTickInSeconds, x.HasPeriodicTick, x.DurationSeconds, x.PeriodSeconds, x.ComponentCount, x.ExecutePeriodicOnApply);

        private static void Copy<TSource, TTarget>(IReadOnlyList<TSource> source, List<TTarget> target, Func<TSource, TTarget> convert)
        {
            for (var i = 0; i < source.Count; i++) target.Add(convert(source[i]));
        }

        private static List<TTarget> Convert<TSource, TTarget>(IReadOnlyList<TSource> source, Func<TSource, TTarget> convert)
        {
            var result = new List<TTarget>(source.Count);
            for (var i = 0; i < source.Count; i++) result.Add(convert(source[i]));
            return result;
        }
    }
}
