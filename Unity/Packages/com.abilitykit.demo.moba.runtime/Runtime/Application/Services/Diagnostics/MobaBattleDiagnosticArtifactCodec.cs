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
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static AbilityKitAnalysisArtifact Attach(
            AbilityKitAnalysisArtifact artifact,
            BattleDiagnosticSessionSnapshot snapshot)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            var profile = BattleDiagnosticMetricProfileRegistry.Resolve();
            artifact.BattleDiagnostics = ToSection(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)),
                profile);
            MobaAnalysisMetricCatalog.AppendFrameMetricsTo(artifact.Dictionaries?.MetricCatalog, profile);
            MobaAnalysisMetricCatalog.AppendFrameThresholdsTo(artifact.ThresholdProfile, profile);
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

        public static AnalysisBattleDiagnosticSection ToSection(
            BattleDiagnosticSessionSnapshot snapshot,
            BattleDiagnosticResolvedMetricProfile profile = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            profile = profile ?? BattleDiagnosticMetricProfileRegistry.Resolve();
            var info = snapshot.SessionInfo;
            var metrics = snapshot.Events.Metrics;
            var frameMetricStoreMetrics = snapshot.FrameMetrics.Metrics;
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
                Effects = new AnalysisBattleDiagnosticEffectTrack { Revision = snapshot.Effects.Revision, Frame = snapshot.Effects.Frame },
                Objects = new AnalysisBattleDiagnosticObjectTrack
                {
                    Revision = snapshot.Objects.Revision,
                    Truncated = snapshot.Objects.Truncated,
                    Completeness = (int)snapshot.Objects.Completeness,
                    BackfillAttemptCount = snapshot.Objects.BackfillAttemptCount,
                    BackfillFailureCount = snapshot.Objects.BackfillFailureCount,
                    LastBackfillFrame = snapshot.Objects.LastBackfillFrame,
                    Summary = ToDto(snapshot.Objects.Summary),
                    EventCoverage = ToDto(snapshot.RuntimeObjectEventCoverage)
                },
                Definitions = new AnalysisBattleDiagnosticDefinitionTrack
                {
                    Revision = snapshot.Definitions.Revision,
                    UnresolvedCount = snapshot.Definitions.UnresolvedCount
                },
                FrameMetrics = new AnalysisBattleDiagnosticMetricTrack
                {
                    Revision = snapshot.FrameMetrics.Revision,
                    Metrics = new AnalysisBattleDiagnosticStoreMetrics
                    {
                        Capacity = frameMetricStoreMetrics.Capacity,
                        Count = frameMetricStoreMetrics.Count,
                        Revision = frameMetricStoreMetrics.Revision,
                        AcceptedCount = frameMetricStoreMetrics.AcceptedCount,
                        EvictedCount = frameMetricStoreMetrics.EvictedCount,
                        RejectedCount = frameMetricStoreMetrics.RejectedCount,
                        IsFrozen = frameMetricStoreMetrics.IsFrozen
                    }
                },
                FrameMetricProfile = ToDto(profile)
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
            Copy(snapshot.Objects.Items, section.Objects.Items, ToDto);
            Copy(snapshot.Definitions.Items, section.Definitions.Items, ToDto);
            Copy(snapshot.FrameMetrics.Samples, section.FrameMetrics.Items, ToDto);
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
                var objects = Convert(section.Objects.Items, FromDto);
                var definitions = Convert(section.Definitions.Items, FromDto);
                var frameMetricSamples = Convert(section.FrameMetrics.Items, item => FromDto(item, scope));
                var frameMetricDto = section.FrameMetrics.Metrics;
                var frameMetricMetrics = new BattleDiagnosticStoreMetrics(
                    frameMetricDto.Capacity,
                    frameMetricDto.Count,
                    frameMetricDto.Revision,
                    frameMetricDto.AcceptedCount,
                    frameMetricDto.EvictedCount,
                    frameMetricDto.RejectedCount,
                    frameMetricDto.IsFrozen);
                BattleDiagnosticWorldSummary? world = section.State.World == null ? (BattleDiagnosticWorldSummary?)null : FromDto(section.State.World, scope);
                ValidateConsistency(section, scope, events, actors, definitions);
                return new BattleDiagnosticSessionSnapshot(
                    in info,
                    section.CapturedAtTimestamp,
                    new BattleDiagnosticEventTrackSnapshot(section.Events.Revision, in storeMetrics, events),
                    new BattleDiagnosticStateTrackSnapshot(section.State.Revision, section.State.Frame, world, actors),
                    new BattleDiagnosticTraceTrackSnapshot(section.Trace.Revision, traces, section.Trace.Truncated, section.Trace.IsStable),
                    new BattleDiagnosticAttributeTrackSnapshot(section.Attributes.Revision, section.Attributes.Frame, attributes, modifiers),
                    new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff>(section.Buffs.Revision, section.Buffs.Frame, buffs),
                    new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag>(section.Tags.Revision, section.Tags.Frame, tags),
                    new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect>(section.Effects.Revision, section.Effects.Frame, effects),
                    new BattleDiagnosticObjectCatalogSnapshot(
                        scope,
                        section.Objects.Revision,
                        section.Objects.Truncated,
                        objects,
                        section.Objects.BackfillAttemptCount,
                        section.Objects.BackfillFailureCount,
                        section.Objects.LastBackfillFrame),
                    new BattleDiagnosticMetricTrackSnapshot(
                        section.FrameMetrics.Revision,
                        in frameMetricMetrics,
                        frameMetricSamples),
                    new BattleDiagnosticDefinitionCatalogSnapshot(
                        scope,
                        section.Definitions.Revision,
                        definitions));
            }
            catch (MobaBattleDiagnosticArtifactException) { throw; }
            catch (Exception ex)
            {
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Invalid", "Battle diagnostics section failed domain validation.", ex);
            }
        }

        public static BattleDiagnosticResolvedMetricProfile FromMetricProfile(
            AnalysisBattleDiagnosticMetricProfile profile)
        {
            if (profile == null) return null;
            var context = new BattleDiagnosticMetricProfileContext(
                profile.Project,
                profile.GameMode,
                profile.NetworkMode,
                profile.DeviceTier);
            var overrides = new List<BattleDiagnosticMetricThresholdOverride>();
            var thresholds = profile.Thresholds ?? new List<AnalysisBattleDiagnosticMetricThreshold>();
            for (var i = 0; i < thresholds.Count; i++)
            {
                var item = thresholds[i];
                if (item == null || !item.WarningThreshold.HasValue || !item.CriticalThreshold.HasValue)
                    continue;
                overrides.Add(new BattleDiagnosticMetricThresholdOverride(
                    item.Metric,
                    item.WarningThreshold.Value,
                    item.CriticalThreshold.Value,
                    item.SuggestedMinimum ?? double.NaN,
                    item.SuggestedMaximum ?? double.NaN));
            }
            return BattleDiagnosticMetricProfileResolver.Restore(
                in context,
                profile.Name,
                overrides);
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
            section.Objects = section.Objects ?? new AnalysisBattleDiagnosticObjectTrack();
            section.Objects.Summary = section.Objects.Summary ??
                                      new AnalysisBattleDiagnosticObjectSummary();
            section.Objects.EventCoverage = section.Objects.EventCoverage ??
                                            new AnalysisBattleDiagnosticObjectEventCoverage();
            section.Objects.Items = section.Objects.Items ?? new List<AnalysisBattleDiagnosticRuntimeObject>();
            section.Definitions = section.Definitions ?? new AnalysisBattleDiagnosticDefinitionTrack();
            section.Definitions.Items = section.Definitions.Items ??
                                        new List<AnalysisBattleDiagnosticDefinition>();
            for (var i = 0; i < section.Definitions.Items.Count; i++)
            {
                var definition = section.Definitions.Items[i];
                if (definition != null)
                    definition.Metadata = definition.Metadata ??
                                          new List<AnalysisBattleDiagnosticDefinitionMetadataEntry>();
            }
            section.FrameMetrics = section.FrameMetrics ?? new AnalysisBattleDiagnosticMetricTrack();
            section.FrameMetrics.Metrics = section.FrameMetrics.Metrics ??
                                           new AnalysisBattleDiagnosticStoreMetrics();
            section.FrameMetrics.Items = section.FrameMetrics.Items ??
                                         new List<AnalysisBattleDiagnosticMetricSample>();
            if (section.FrameMetricProfile != null)
                section.FrameMetricProfile.Thresholds = section.FrameMetricProfile.Thresholds ??
                                                        new List<AnalysisBattleDiagnosticMetricThreshold>();
        }

        private static void ValidateConsistency(
            AnalysisBattleDiagnosticSection section,
            BattleDiagnosticSessionScope scope,
            List<BattleDiagnosticEvent> events,
            List<BattleDiagnosticActorSummary> actors,
            List<BattleDiagnosticDefinition> definitions)
        {
            if (!scope.IsValid) throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Scope", "Battle diagnostics session scope is invalid.");
            if (section.CapturedAtTimestamp < 0) throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Timestamp", "Captured timestamp cannot be negative.");
            if (section.Events.Revision != section.Events.Metrics.Revision || section.Events.Metrics.Count != events.Count)
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.EventMetrics", "Event track revision/count does not match its metrics.");
            for (var i = 1; i < events.Count; i++)
                if (events[i].Sequence <= events[i - 1].Sequence) throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.EventSequence", "Event sequences must be strictly increasing.");
            if (section.State.World != null && section.State.World.ActorCount != actors.Count)
                throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.ActorCount", "World actor count does not match the actor list.");
            if (section.FrameMetrics.Revision != section.FrameMetrics.Metrics.Revision ||
                section.FrameMetrics.Metrics.Count != section.FrameMetrics.Items.Count)
                throw new MobaBattleDiagnosticArtifactException(
                    "BattleDiagnostics.FrameMetricMetrics",
                    "Frame metric track revision/count does not match its metrics.");
            var unresolvedCount = 0;
            for (var i = 0; i < definitions.Count; i++)
                if (!definitions[i].IsResolved) unresolvedCount++;
            if (section.Definitions.UnresolvedCount != unresolvedCount)
                throw new MobaBattleDiagnosticArtifactException(
                    "BattleDiagnostics.DefinitionCount",
                    "Definition track unresolved count does not match its items.");
        }

        private static AnalysisBattleDiagnosticEvent ToDto(BattleDiagnosticEvent item)
        {
            var result = new AnalysisBattleDiagnosticEvent { Frame = item.Frame, Sequence = item.Sequence, MonotonicTimestamp = item.MonotonicTimestamp, Kind = (int)item.Kind, Channel = (int)item.Channel, Outcome = (int)item.Outcome, SourceActorId = item.SourceActorId, TargetActorId = item.TargetActorId, SourceActorGeneration = item.SourceActor.Generation, TargetActorGeneration = item.TargetActor.Generation, SubjectObjectKind = (int)item.SubjectObject.Kind, SubjectRuntimeId = item.SubjectObject.RuntimeId, SubjectGeneration = item.SubjectObject.Generation, ConfigId = item.ConfigId, DefinitionKind = (int)item.DefinitionKind, RootContextId = item.RootContextId, ContextId = item.ContextId, SkillRuntimeId = item.SkillRuntime.RuntimeId, SkillRuntimeGeneration = item.SkillRuntime.Generation, AttackId = item.AttackId, PayloadVersion = item.PayloadVersion, Summary = item.Summary };
            if (item.Payload.TryGetSyncSnapshotReceived(out var payload)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, AuthoritativeFrame = payload.AuthoritativeFrame, StateHash = payload.StateHash };
            else if (item.Payload.TryGetTriggerAnalysis(out var trigger)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, TriggerId = trigger.TriggerId, TriggerContextKind = trigger.ContextKind, TriggerOriginKind = trigger.OriginKind, TriggerStage = (int)trigger.Stage, TriggerResult = (int)trigger.Result, TriggerDetailCode = trigger.DetailCode, TriggerCurrentDepth = trigger.CurrentDepth, TriggerCurrentFrameCount = trigger.CurrentFrameCount, TriggerCurrentRootCount = trigger.CurrentRootCount, TriggerCurrentSameTriggerCount = trigger.CurrentSameTriggerCount, TriggerFailureKey = trigger.FailureKey, TriggerReason = trigger.Reason };
            else if (item.Payload.TryGetTriggerAnalysisAggregate(out var aggregate)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, TriggerId = aggregate.TriggerId, TriggerContextKind = aggregate.ContextKind, TriggerOriginKind = aggregate.OriginKind, TriggerStage = (int)aggregate.Stage, TriggerResult = (int)aggregate.Result, TriggerDetailCode = aggregate.DetailCode, TriggerAggregateOccurrenceCount = aggregate.OccurrenceCount, TriggerAggregateFirstFrame = aggregate.FirstFrame, TriggerAggregateLastFrame = aggregate.LastFrame, TriggerAggregateFirstContextId = aggregate.FirstContextId, TriggerAggregateLastContextId = aggregate.LastContextId, TriggerAggregateFirstRootContextId = aggregate.FirstRootContextId, TriggerAggregateLastRootContextId = aggregate.LastRootContextId, TriggerFailureKey = aggregate.FailureKey, TriggerReason = aggregate.SampleReason };
            else if (item.Payload.TryGetSkillFailure(out var failure)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, SkillFailureSlot = failure.Slot, SkillFailureSource = failure.Source, SkillFailureStage = failure.Stage, SkillFailureCode = failure.Code, SkillFailureMessage = failure.Message, SkillFailureCommandId = failure.CommandId };
            else if (item.Payload.TryGetBuffLifecycle(out var buff)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, BuffLifecycleStage = (int)buff.Stage, BuffLifecycleStackCount = buff.StackCount, BuffLifecyclePreviousStackCount = buff.PreviousStackCount, BuffLifecycleDurationMilliseconds = buff.DurationMilliseconds, BuffLifecycleRemainingMilliseconds = buff.RemainingMilliseconds, BuffLifecycleIntervalRemainingMilliseconds = buff.IntervalRemainingMilliseconds, BuffLifecycleMaxStacks = buff.MaxStacks, BuffLifecycleModifierBindingCount = buff.ModifierBindingCount, BuffLifecycleModifierSourceId = buff.ModifierSourceId, BuffLifecycleRemoveReason = buff.RemoveReason };
            else if (item.Payload.TryGetDamageCalculation(out var damage)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, DamageStage = (int)damage.Stage, DamageBaseRaw = damage.BaseDamageRaw, DamageRawRaw = damage.RawDamageRaw, DamageMitigatedRaw = damage.MitigatedDamageRaw, DamageShieldRaw = damage.ShieldAbsorbRaw, DamagePlannedHpRaw = damage.PlannedHpDamageRaw, DamageAppliedHpRaw = damage.AppliedHpDamageRaw };
            else if (item.Payload.TryGetInputCommand(out var input)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, InputCommandId = input.CommandId, InputFrame = input.InputFrame, InputPlayerId = input.PlayerId, InputOpCode = input.OpCode, InputSucceeded = input.Succeeded, InputFailureCode = input.FailureCode, InputMessage = input.Message, InputSkillSlot = input.SkillSlot, InputSkillPhase = input.SkillPhase, InputTargetActorId = input.TargetActorId };
            else if (item.Payload.TryGetTargetSearch(out var search)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, TargetSearchCommandId = search.CommandId, TargetSearchExplicitTargetActorId = search.ExplicitTargetActorId, TargetSearchCandidateCount = search.CandidateCount, TargetSearchEligibleCount = search.EligibleCount, TargetSearchSelectedCount = search.SelectedCount, TargetSearchSelectedActorIds = search.SelectedActorIds, TargetSearchDecisionDetails = search.DecisionDetails };
            else if (item.Payload.TryGetSkillExecution(out var execution)) result.Payload = new AnalysisBattleDiagnosticEventPayload { Kind = (int)item.Payload.Kind, SchemaVersion = item.Payload.SchemaVersion, SkillExecutionCommandId = execution.CommandId, SkillExecutionStage = (int)execution.Stage, SkillExecutionSlot = execution.SkillSlot, SkillExecutionLevel = execution.SkillLevel, SkillExecutionSequence = execution.CastSequence, SkillExecutionEndReason = execution.EndReason, SkillExecutionResourceType = execution.ResourceType, SkillExecutionResourceAmountRaw = execution.ResourceAmountRaw, SkillExecutionResourceBeforeRaw = execution.ResourceBeforeRaw, SkillExecutionResourceAfterRaw = execution.ResourceAfterRaw, SkillExecutionChargeCost = execution.ChargeCost, SkillExecutionCooldownMs = execution.CooldownMs, SkillExecutionSharedCooldownMs = execution.SharedCooldownMs, SkillExecutionGlobalCooldownMs = execution.GlobalCooldownMs, SkillExecutionPendingChildren = execution.PendingChildren, SkillExecutionForced = execution.Forced, SkillExecutionDetail = execution.Detail };
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
                else if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.TriggerAnalysisAggregate && item.Payload.SchemaVersion == BattleDiagnosticTriggerAnalysisAggregatePayload.CurrentSchemaVersion)
                {
                    var aggregate = new BattleDiagnosticTriggerAnalysisAggregatePayload(
                        item.Payload.TriggerId,
                        item.Payload.TriggerContextKind,
                        item.Payload.TriggerOriginKind,
                        (BattleDiagnosticTriggerAnalysisStage)item.Payload.TriggerStage,
                        (BattleDiagnosticTriggerAnalysisResult)item.Payload.TriggerResult,
                        item.Payload.TriggerDetailCode,
                        item.Payload.TriggerAggregateOccurrenceCount,
                        item.Payload.TriggerAggregateFirstFrame,
                        item.Payload.TriggerAggregateLastFrame,
                        item.Payload.TriggerAggregateFirstContextId,
                        item.Payload.TriggerAggregateLastContextId,
                        item.Payload.TriggerAggregateFirstRootContextId,
                        item.Payload.TriggerAggregateLastRootContextId,
                        item.Payload.TriggerFailureKey,
                        item.Payload.TriggerReason);
                    payload = BattleDiagnosticEventPayload.FromTriggerAnalysisAggregate(in aggregate);
                }
                else if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.BuffLifecycle && item.Payload.SchemaVersion == BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion)
                {
                    var buff = new BattleDiagnosticBuffLifecyclePayload(
                        (BattleDiagnosticBuffLifecycleStage)item.Payload.BuffLifecycleStage,
                        item.Payload.BuffLifecycleStackCount,
                        item.Payload.BuffLifecyclePreviousStackCount,
                        item.Payload.BuffLifecycleDurationMilliseconds,
                        item.Payload.BuffLifecycleRemainingMilliseconds,
                        item.Payload.BuffLifecycleIntervalRemainingMilliseconds,
                        item.Payload.BuffLifecycleMaxStacks,
                        item.Payload.BuffLifecycleModifierBindingCount,
                        item.Payload.BuffLifecycleModifierSourceId,
                        item.Payload.BuffLifecycleRemoveReason);
                    payload = BattleDiagnosticEventPayload.FromBuffLifecycle(in buff);
                }
                else if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.SkillFailure && item.Payload.SchemaVersion == BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion)
                {
                    var failure = new BattleDiagnosticSkillFailurePayload(
                        item.Payload.SkillFailureSlot,
                        item.Payload.SkillFailureSource,
                        item.Payload.SkillFailureStage,
                        item.Payload.SkillFailureCode,
                        item.Payload.SkillFailureMessage,
                        item.Payload.SkillFailureCommandId);
                    payload = BattleDiagnosticEventPayload.FromSkillFailure(in failure);
                }
                else if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.DamageCalculation && item.Payload.SchemaVersion == BattleDiagnosticDamageCalculationPayload.CurrentSchemaVersion)
                {
                    var damage = new BattleDiagnosticDamageCalculationPayload((BattleDiagnosticDamageStage)item.Payload.DamageStage,
                        item.Payload.DamageBaseRaw, item.Payload.DamageRawRaw, item.Payload.DamageMitigatedRaw,
                        item.Payload.DamageShieldRaw, item.Payload.DamagePlannedHpRaw, item.Payload.DamageAppliedHpRaw);
                    payload = BattleDiagnosticEventPayload.FromDamageCalculation(in damage);
                }
                else if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.InputCommand && item.Payload.SchemaVersion == BattleDiagnosticInputCommandPayload.CurrentSchemaVersion)
                {
                    var input = new BattleDiagnosticInputCommandPayload(item.Payload.InputCommandId,
                        item.Payload.InputFrame, item.Payload.InputPlayerId, item.Payload.InputOpCode,
                        item.Payload.InputSucceeded, item.Payload.InputFailureCode, item.Payload.InputMessage,
                        item.Payload.InputSkillSlot, item.Payload.InputSkillPhase, item.Payload.InputTargetActorId);
                    payload = BattleDiagnosticEventPayload.FromInputCommand(in input);
                }
                else if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.TargetSearch && item.Payload.SchemaVersion == BattleDiagnosticTargetSearchPayload.CurrentSchemaVersion)
                {
                    var search = new BattleDiagnosticTargetSearchPayload(item.Payload.TargetSearchCommandId,
                        item.Payload.TargetSearchExplicitTargetActorId, item.Payload.TargetSearchCandidateCount,
                        item.Payload.TargetSearchEligibleCount, item.Payload.TargetSearchSelectedCount,
                        item.Payload.TargetSearchSelectedActorIds, item.Payload.TargetSearchDecisionDetails);
                    payload = BattleDiagnosticEventPayload.FromTargetSearch(in search);
                }
                else if (item.Payload.Kind == (int)BattleDiagnosticPayloadKind.SkillExecution && item.Payload.SchemaVersion == BattleDiagnosticSkillExecutionPayload.CurrentSchemaVersion)
                {
                    var execution = new BattleDiagnosticSkillExecutionPayload(
                        item.Payload.SkillExecutionCommandId,
                        (BattleDiagnosticSkillExecutionStage)item.Payload.SkillExecutionStage,
                        item.Payload.SkillExecutionSlot,
                        item.Payload.SkillExecutionLevel,
                        item.Payload.SkillExecutionSequence,
                        item.Payload.SkillExecutionEndReason,
                        item.Payload.SkillExecutionResourceType,
                        item.Payload.SkillExecutionResourceAmountRaw,
                        item.Payload.SkillExecutionResourceBeforeRaw,
                        item.Payload.SkillExecutionResourceAfterRaw,
                        item.Payload.SkillExecutionChargeCost,
                        item.Payload.SkillExecutionCooldownMs,
                        item.Payload.SkillExecutionSharedCooldownMs,
                        item.Payload.SkillExecutionGlobalCooldownMs,
                        item.Payload.SkillExecutionPendingChildren,
                        item.Payload.SkillExecutionForced,
                        item.Payload.SkillExecutionDetail);
                    payload = BattleDiagnosticEventPayload.FromSkillExecution(in execution);
                }
                else
                {
                    throw new MobaBattleDiagnosticArtifactException("BattleDiagnostics.Payload", "Unsupported battle diagnostic event payload.");
                }
            }
            return new BattleDiagnosticEvent(scope, item.Frame, item.Sequence, item.MonotonicTimestamp, (BattleDiagnosticEventKind)item.Kind, (BattleDiagnosticEventChannel)item.Channel, (BattleDiagnosticEventOutcome)item.Outcome, item.SourceActorId, item.TargetActorId, item.ConfigId, item.RootContextId, item.ContextId, new BattleDiagnosticRuntimeHandle(item.SkillRuntimeId, item.SkillRuntimeGeneration), item.AttackId, item.PayloadVersion, item.Summary, payload, (BattleDiagnosticDefinitionKind)item.DefinitionKind, item.SourceActorGeneration, item.TargetActorGeneration, (BattleDiagnosticRuntimeObjectKind)item.SubjectObjectKind, item.SubjectRuntimeId, item.SubjectGeneration);
        }

        private static AnalysisBattleDiagnosticWorld ToDto(BattleDiagnosticWorldSummary x) => new AnalysisBattleDiagnosticWorld { Frame = x.Frame, MonotonicTimestamp = x.MonotonicTimestamp, ActorCount = x.ActorCount, ActiveSkillRuntimeCount = x.ActiveSkillRuntimeCount, ActiveTraceRootCount = x.ActiveTraceRootCount, StateHash = x.StateHash };
        private static BattleDiagnosticWorldSummary FromDto(AnalysisBattleDiagnosticWorld x, BattleDiagnosticSessionScope s) => new BattleDiagnosticWorldSummary(s, x.Frame, x.MonotonicTimestamp, x.ActorCount, x.ActiveSkillRuntimeCount, x.ActiveTraceRootCount, x.StateHash);
        private static AnalysisBattleDiagnosticActor ToDto(BattleDiagnosticActorSummary x) => new AnalysisBattleDiagnosticActor { Frame = x.Frame, ActorId = x.ActorId, Kind = (int)x.Kind, ConfigId = x.ConfigId, TeamId = x.TeamId, PositionX = x.PositionX, PositionY = x.PositionY, PositionZ = x.PositionZ, Health = x.Health, MaximumHealth = x.MaximumHealth, IsAlive = x.IsAlive, DisplayName = x.DisplayName };
        private static BattleDiagnosticActorSummary FromDto(AnalysisBattleDiagnosticActor x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorSummary(s, x.Frame, x.ActorId, (BattleDiagnosticActorKind)x.Kind, x.ConfigId, x.TeamId, x.PositionX, x.PositionY, x.PositionZ, x.Health, x.MaximumHealth, x.IsAlive, x.DisplayName);
        private static AnalysisBattleDiagnosticTraceNode ToDto(BattleDiagnosticTraceNodeSummary x)
        {
            return new AnalysisBattleDiagnosticTraceNode
            {
                RootContext = ToDto(x.RootContext),
                Context = ToDto(x.Context),
                ParentContext = ToDto(x.ParentContext),
                SourceObject = ToDto(x.SourceObject),
                TargetObject = ToDto(x.TargetObject),
                Definition = ToDto(x.Definition),
                TriggerDefinition = ToDto(x.TriggerDefinition),
                SkillDefinition = ToDto(x.SkillDefinition),
                OriginKind = x.OriginKind,
                OriginDefinition = ToDto(x.OriginDefinition),
                RootContextId = x.RootContextId,
                ContextId = x.ContextId,
                ParentContextId = x.ParentContextId,
                StartFrame = x.StartFrame,
                EndFrame = x.EndFrame,
                State = (int)x.State,
                ActorId = x.ActorId,
                SourceActorGeneration = x.SourceActorGeneration,
                TargetActorId = x.TargetActorId,
                TargetActorGeneration = x.TargetActorGeneration,
                TriggerId = x.TriggerId,
                ConfigId = x.ConfigId,
                DefinitionKind = (int)x.DefinitionKind,
                Kind = x.Kind,
                EndReason = x.EndReason,
                SkillId = x.SkillId,
                CastFlowId = x.CastFlowId,
                PhaseId = x.PhaseId,
                ExecutionFacts = ToDto(x.ExecutionFacts),
                ActionFacts = ToDto(x.ActionFacts)
            };
        }

        private static BattleDiagnosticTraceNodeSummary FromDto(
            AnalysisBattleDiagnosticTraceNode x,
            BattleDiagnosticSessionScope s)
        {
            var rootContext = FromDto(x.RootContext);
            var context = FromDto(x.Context);
            var parentContext = FromDto(x.ParentContext);
            var sourceObject = FromDto(x.SourceObject);
            var targetObject = FromDto(x.TargetObject);
            var definition = FromDto(x.Definition);
            var triggerDefinition = FromDto(x.TriggerDefinition);
            var skillDefinition = FromDto(x.SkillDefinition);
            var originDefinition = FromDto(x.OriginDefinition);
            var rootContextId = rootContext.IsValid ? rootContext.ContextId : x.RootContextId;
            var contextId = context.IsValid ? context.ContextId : x.ContextId;
            var parentContextId = parentContext.IsValid ? parentContext.ContextId : x.ParentContextId;
            if (!sourceObject.HasRuntimeId)
            {
                sourceObject = BattleDiagnosticRuntimeObjectReference.Create(
                    BattleDiagnosticRuntimeObjectKind.Actor,
                    x.ActorId,
                    x.SourceActorGeneration);
            }
            if (!targetObject.HasRuntimeId)
            {
                targetObject = BattleDiagnosticRuntimeObjectReference.Create(
                    BattleDiagnosticRuntimeObjectKind.Actor,
                    x.TargetActorId,
                    x.TargetActorGeneration);
            }
            if (!definition.HasDefinitionId)
            {
                definition = BattleDiagnosticDefinitionReference.Create(
                    (BattleDiagnosticDefinitionKind)x.DefinitionKind,
                    x.ConfigId);
            }
            // Older artifacts classified action registration IDs as effect IDs.
            if (string.Equals(x.Kind, "EffectAction", StringComparison.Ordinal) &&
                definition.Kind == BattleDiagnosticDefinitionKind.Effect)
            {
                definition = BattleDiagnosticDefinitionReference.Create(
                    BattleDiagnosticDefinitionKind.Action,
                    definition.DefinitionId);
            }
            if (!triggerDefinition.HasDefinitionId)
            {
                triggerDefinition = BattleDiagnosticDefinitionReference.Create(
                    BattleDiagnosticDefinitionKind.Trigger,
                    x.TriggerId);
            }
            if (!skillDefinition.HasDefinitionId)
            {
                skillDefinition = BattleDiagnosticDefinitionReference.Create(
                    BattleDiagnosticDefinitionKind.Skill,
                    x.SkillId);
            }

            return new BattleDiagnosticTraceNodeSummary(
                s,
                rootContextId,
                contextId,
                parentContextId,
                x.StartFrame,
                x.EndFrame,
                (BattleDiagnosticTraceNodeState)x.State,
                sourceObject.RuntimeId,
                definition.DefinitionId,
                x.Kind,
                x.EndReason,
                skillDefinition.DefinitionId,
                x.CastFlowId,
                x.PhaseId,
                targetObject.RuntimeId,
                triggerDefinition.DefinitionId,
                sourceObject.Generation,
                targetObject.Generation,
                definition.Kind,
                x.OriginKind,
                originDefinition.DefinitionId,
                originDefinition.Kind,
                FromDto(x.ExecutionFacts),
                FromDto(x.ActionFacts));
        }

        private static AnalysisBattleDiagnosticActionActorValues ToDto(BattleDiagnosticActionActorValues x) =>
            new AnalysisBattleDiagnosticActionActorValues
            { ActorId = x.ActorId, BindingId = x.BindingId, HasActor = x.HasActor, HasHp = x.HasHp, Hp = x.Hp, HasMana = x.HasMana, Mana = x.Mana };

        private static BattleDiagnosticActionActorValues FromDto(AnalysisBattleDiagnosticActionActorValues x) =>
            x == null ? default : new BattleDiagnosticActionActorValues(x.ActorId, x.BindingId, x.HasActor, x.HasHp, x.Hp, x.HasMana, x.Mana);

        private static AnalysisBattleDiagnosticActionExecutionFacts ToDto(BattleDiagnosticActionExecutionFacts x)
        {
            if (x.Availability == BattleDiagnosticDataAvailability.NotCaptured) return null;
            var commits = new List<AnalysisBattleDiagnosticActionHealthCommit>();
            foreach (var c in x.Commits)
                commits.Add(new AnalysisBattleDiagnosticActionHealthCommit
                {
                    Kind = c.Kind, SourceActorId = c.SourceActorId, TargetActorId = c.TargetActorId,
                    ValueType = c.ValueType, ReasonKind = c.ReasonKind, ReasonParam = c.ReasonParam,
                    RequestedValue = c.RequestedValue, AppliedValue = c.AppliedValue, OldHp = c.OldHp,
                    TargetHp = c.TargetHp, TargetMaxHp = c.TargetMaxHp, OriginContextId = c.OriginContextId
                });
            var damageResults = new List<AnalysisBattleDiagnosticActionDamageResult>();
            foreach (var d in x.DamageResults)
            {
                var c = d.Calculation;
                damageResults.Add(new AnalysisBattleDiagnosticActionDamageResult
                {
                    Sequence = d.Sequence, Frame = d.Frame, SourceActorId = d.SourceActorId, TargetActorId = d.TargetActorId,
                    OriginContextId = d.OriginContextId, Stage = (int)c.Stage, BaseDamageRaw = c.BaseDamageRaw,
                    RawDamageRaw = c.RawDamageRaw, MitigatedDamageRaw = c.MitigatedDamageRaw, ShieldAbsorbRaw = c.ShieldAbsorbRaw,
                    PlannedHpDamageRaw = c.PlannedHpDamageRaw, AppliedHpDamageRaw = c.AppliedHpDamageRaw, Detail = d.Detail
                });
            }
            return new AnalysisBattleDiagnosticActionExecutionFacts
            {
                Availability = (int)x.Availability, SnapshotId = x.SnapshotId, Generation = x.Generation,
                Frame = x.Frame, TypeId = x.TypeId, SchemaVersion = x.SchemaVersion, ActionIndex = x.ActionIndex,
                ActionId = x.ActionId, Outcome = (int)x.Outcome, HasAfter = x.HasAfter, EndFrame = x.EndFrame,
                CommitsComplete = x.CommitsComplete, CommitsTruncated = x.CommitsTruncated,
                SourceBefore = ToDto(x.SourceBefore), TargetBefore = ToDto(x.TargetBefore),
                SourceAfter = ToDto(x.SourceAfter), TargetAfter = ToDto(x.TargetAfter), Commits = commits,
                DamageAvailability = (int)x.DamageAvailability, DamageCoverageContinuous = x.DamageCoverageContinuous,
                DamageResultsTruncated = x.DamageResultsTruncated, DamageResults = damageResults
            };
        }

        private static BattleDiagnosticActionExecutionFacts FromDto(AnalysisBattleDiagnosticActionExecutionFacts x)
        {
            if (x == null) return default;
            var commits = new List<BattleDiagnosticActionHealthCommit>();
            if (x.Commits != null)
                foreach (var c in x.Commits)
                    if (c != null) commits.Add(new BattleDiagnosticActionHealthCommit(c.Kind, c.SourceActorId, c.TargetActorId,
                        c.ValueType, c.ReasonKind, c.ReasonParam, c.RequestedValue, c.AppliedValue, c.OldHp, c.TargetHp,
                        c.TargetMaxHp, c.OriginContextId));
            var damageResults = new List<BattleDiagnosticActionDamageResult>();
            if (x.DamageResults != null)
                foreach (var d in x.DamageResults)
                {
                    if (d == null) continue;
                    var c = new BattleDiagnosticDamageCalculationPayload((BattleDiagnosticDamageStage)d.Stage,
                        d.BaseDamageRaw, d.RawDamageRaw, d.MitigatedDamageRaw, d.ShieldAbsorbRaw, d.PlannedHpDamageRaw, d.AppliedHpDamageRaw);
                    damageResults.Add(new BattleDiagnosticActionDamageResult(d.Sequence, d.Frame, d.SourceActorId, d.TargetActorId,
                        d.OriginContextId, c, d.Detail));
                }
            return new BattleDiagnosticActionExecutionFacts((BattleDiagnosticDataAvailability)x.Availability,
                x.SnapshotId, x.Generation, x.Frame, x.TypeId, x.SchemaVersion, x.ActionIndex, x.ActionId,
                (BattleDiagnosticActionOutcome)x.Outcome, x.HasAfter, x.EndFrame, x.CommitsComplete, x.CommitsTruncated,
                FromDto(x.SourceBefore), FromDto(x.TargetBefore), FromDto(x.SourceAfter), FromDto(x.TargetAfter), commits,
                (BattleDiagnosticDataAvailability)x.DamageAvailability, x.DamageCoverageContinuous, x.DamageResultsTruncated, damageResults);
        }

        private static AnalysisBattleDiagnosticEffectExecutionFacts ToDto(BattleDiagnosticEffectExecutionFacts x) =>
            new AnalysisBattleDiagnosticEffectExecutionFacts
            {
                Availability = (int)x.Availability, SnapshotId = x.SnapshotId, Generation = x.Generation,
                Frame = x.Frame, TypeId = x.TypeId, SchemaVersion = x.SchemaVersion,
                EffectConfigId = x.EffectConfigId, TriggerId = x.TriggerId, PayloadTypeName = x.PayloadTypeName,
                HasRuntimeContext = x.HasRuntimeContext, RuntimeContextId = x.RuntimeContextId,
                RuntimeContextVersion = x.RuntimeContextVersion, HasStageSnapshot = x.HasStageSnapshot,
                StackCount = x.StackCount, ElapsedSeconds = x.ElapsedSeconds,
                RemainingSeconds = x.RemainingSeconds, DurationSeconds = x.DurationSeconds
            };

        private static BattleDiagnosticEffectExecutionFacts FromDto(AnalysisBattleDiagnosticEffectExecutionFacts x) =>
            x == null ? default : new BattleDiagnosticEffectExecutionFacts((BattleDiagnosticDataAvailability)x.Availability,
                x.SnapshotId, x.Generation, x.Frame, x.TypeId, x.SchemaVersion, x.EffectConfigId, x.TriggerId,
                x.PayloadTypeName, x.HasRuntimeContext, x.RuntimeContextId, x.RuntimeContextVersion,
                x.HasStageSnapshot, x.StackCount, x.ElapsedSeconds, x.RemainingSeconds, x.DurationSeconds);

        private static AnalysisBattleDiagnosticTraceContextReference ToDto(
            BattleDiagnosticTraceContextReference x)
        {
            return x.IsValid
                ? new AnalysisBattleDiagnosticTraceContextReference { ContextId = x.ContextId }
                : null;
        }

        private static BattleDiagnosticTraceContextReference FromDto(
            AnalysisBattleDiagnosticTraceContextReference x)
        {
            return x == null
                ? default
                : BattleDiagnosticTraceContextReference.Create(x.ContextId);
        }

        private static AnalysisBattleDiagnosticRuntimeObjectReference ToDto(
            BattleDiagnosticRuntimeObjectReference x)
        {
            return x.HasRuntimeId
                ? new AnalysisBattleDiagnosticRuntimeObjectReference
                {
                    Kind = (int)x.Kind,
                    RuntimeId = x.RuntimeId,
                    Generation = x.Generation
                }
                : null;
        }

        private static BattleDiagnosticRuntimeObjectReference FromDto(
            AnalysisBattleDiagnosticRuntimeObjectReference x)
        {
            return x == null
                ? default
                : BattleDiagnosticRuntimeObjectReference.Create(
                    (BattleDiagnosticRuntimeObjectKind)x.Kind,
                    x.RuntimeId,
                    x.Generation);
        }

        private static AnalysisBattleDiagnosticDefinitionReference ToDto(
            BattleDiagnosticDefinitionReference x)
        {
            return x.HasDefinitionId
                ? new AnalysisBattleDiagnosticDefinitionReference
                {
                    Kind = (int)x.Kind,
                    DefinitionId = x.DefinitionId
                }
                : null;
        }

        private static BattleDiagnosticDefinitionReference FromDto(
            AnalysisBattleDiagnosticDefinitionReference x)
        {
            return x == null
                ? default
                : BattleDiagnosticDefinitionReference.Create(
                    (BattleDiagnosticDefinitionKind)x.Kind,
                    x.DefinitionId);
        }
        private static AnalysisBattleDiagnosticAttribute ToDto(BattleDiagnosticActorAttribute x) => new AnalysisBattleDiagnosticAttribute { Frame = x.Frame, ActorId = x.ActorId, AttributeId = x.AttributeId, BaseValue = x.BaseValue, FinalValue = x.FinalValue, ModifierCount = x.ModifierCount, Name = x.Name };
        private static BattleDiagnosticActorAttribute FromDto(AnalysisBattleDiagnosticAttribute x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorAttribute(s, x.Frame, x.ActorId, x.AttributeId, x.BaseValue, x.FinalValue, x.ModifierCount, x.Name);
        private static AnalysisBattleDiagnosticAttributeModifier ToDto(BattleDiagnosticActorAttributeModifier x) => new AnalysisBattleDiagnosticAttributeModifier { Frame = x.Frame, ActorId = x.ActorId, AttributeId = x.AttributeId, Operation = x.Operation, Magnitude = x.Magnitude, Priority = x.Priority, SourceId = x.SourceId, MagnitudeType = x.MagnitudeType, DeclaredValue = x.DeclaredValue, StackedValue = x.StackedValue, ProjectedValue = x.ProjectedValue, CurrentValue = x.CurrentValue, HasCurrentValue = x.HasCurrentValue, CapturedValue = x.CapturedValue, HasCapturedValue = x.HasCapturedValue, EvaluationPolicy = x.EvaluationPolicy, StackCount = x.StackCount, CaptureMode = x.CaptureMode, Explanation = x.Explanation };
        private static BattleDiagnosticActorAttributeModifier FromDto(AnalysisBattleDiagnosticAttributeModifier x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorAttributeModifier(s, x.Frame, x.ActorId, x.AttributeId, x.Operation, x.Magnitude, x.Priority, x.SourceId, x.MagnitudeType, x.DeclaredValue, x.StackedValue, x.ProjectedValue, x.CurrentValue, x.HasCurrentValue, x.CapturedValue, x.HasCapturedValue, x.EvaluationPolicy, x.StackCount, x.CaptureMode, x.Explanation);
        private static AnalysisBattleDiagnosticBuff ToDto(BattleDiagnosticActorBuff x) => new AnalysisBattleDiagnosticBuff { Frame = x.Frame, ActorId = x.ActorId, BuffId = x.BuffId, SourceActorId = x.SourceActorId, StackCount = x.StackCount, RemainingSeconds = x.RemainingSeconds, IntervalRemainingSeconds = x.IntervalRemainingSeconds, SourceContextId = x.SourceContextId, RuntimeContextId = x.RuntimeContextId, RuntimeContextVersion = x.RuntimeContextVersion, SkillRuntimeId = x.SkillRuntime.RuntimeId, SkillRuntimeGeneration = x.SkillRuntime.Generation, RootContextId = x.RootContextId, ModifierBindingCount = x.ModifierBindingCount, MaxStacks = x.MaxStacks, Name = x.Name, ModifierSourceId = x.ModifierSourceId };
        private static BattleDiagnosticActorBuff FromDto(AnalysisBattleDiagnosticBuff x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorBuff(s, x.Frame, x.ActorId, x.BuffId, x.SourceActorId, x.StackCount, x.RemainingSeconds, x.IntervalRemainingSeconds, x.SourceContextId, x.RuntimeContextId, x.RuntimeContextVersion, new BattleDiagnosticRuntimeHandle(x.SkillRuntimeId, x.SkillRuntimeGeneration), x.RootContextId, x.ModifierBindingCount, x.MaxStacks, x.Name, x.ModifierSourceId);
        private static AnalysisBattleDiagnosticTag ToDto(BattleDiagnosticActorTag x) => new AnalysisBattleDiagnosticTag { Frame = x.Frame, ActorId = x.ActorId, TagId = x.TagId, Name = x.Name };
        private static BattleDiagnosticActorTag FromDto(AnalysisBattleDiagnosticTag x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorTag(s, x.Frame, x.ActorId, x.TagId, x.Name);
        private static AnalysisBattleDiagnosticEffect ToDto(BattleDiagnosticActorEffect x) => new AnalysisBattleDiagnosticEffect { Frame = x.Frame, ActorId = x.ActorId, InstanceId = x.InstanceId, DurationPolicy = (int)x.DurationPolicy, StackCount = x.StackCount, ElapsedSeconds = x.ElapsedSeconds, RemainingSeconds = x.RemainingSeconds, HasRemainingTime = x.HasRemainingTime, NextTickInSeconds = x.NextTickInSeconds, HasPeriodicTick = x.HasPeriodicTick, DurationSeconds = x.DurationSeconds, PeriodSeconds = x.PeriodSeconds, ComponentCount = x.ComponentCount, ExecutePeriodicOnApply = x.ExecutePeriodicOnApply };
        private static BattleDiagnosticActorEffect FromDto(AnalysisBattleDiagnosticEffect x, BattleDiagnosticSessionScope s) => new BattleDiagnosticActorEffect(s, x.Frame, x.ActorId, x.InstanceId, (BattleDiagnosticEffectDurationPolicy)x.DurationPolicy, x.StackCount, x.ElapsedSeconds, x.RemainingSeconds, x.HasRemainingTime, x.NextTickInSeconds, x.HasPeriodicTick, x.DurationSeconds, x.PeriodSeconds, x.ComponentCount, x.ExecutePeriodicOnApply);
        private static AnalysisBattleDiagnosticMetricProfile ToDto(
            BattleDiagnosticResolvedMetricProfile profile)
        {
            if (profile == null) return null;
            var result = new AnalysisBattleDiagnosticMetricProfile
            {
                Name = profile.Name,
                Project = profile.Context.Project,
                GameMode = profile.Context.GameMode,
                NetworkMode = profile.Context.NetworkMode,
                DeviceTier = profile.Context.DeviceTier
            };
            for (var i = 0; i < profile.Descriptors.Count; i++)
            {
                var descriptor = profile.Descriptors[i];
                if (!descriptor.HasAssessment) continue;
                result.Thresholds.Add(new AnalysisBattleDiagnosticMetricThreshold
                {
                    Metric = descriptor.Metric,
                    WarningThreshold = descriptor.WarningThreshold,
                    CriticalThreshold = descriptor.CriticalThreshold,
                    SuggestedMinimum = descriptor.HasSuggestedRange
                        ? descriptor.SuggestedMinimum
                        : (double?)null,
                    SuggestedMaximum = descriptor.HasSuggestedRange
                        ? descriptor.SuggestedMaximum
                        : (double?)null
                });
            }
            return result;
        }
        private static AnalysisBattleDiagnosticMetricSample ToDto(BattleDiagnosticMetricSample x) => new AnalysisBattleDiagnosticMetricSample { Sequence = x.Sequence, Frame = x.Frame, MonotonicTimestamp = x.MonotonicTimestamp, Category = (int)x.Category, ValueKind = (int)x.ValueKind, Metric = x.Metric, Value = x.Value, Dimension = x.Dimension };
        private static BattleDiagnosticMetricSample FromDto(AnalysisBattleDiagnosticMetricSample x, BattleDiagnosticSessionScope s) => new BattleDiagnosticMetricSample(s, x.Sequence, x.Frame, x.MonotonicTimestamp, (BattleDiagnosticMetricCategory)x.Category, (BattleDiagnosticMetricValueKind)x.ValueKind, x.Metric, x.Value, x.Dimension);
        private static AnalysisBattleDiagnosticRuntimeObject ToDto(BattleDiagnosticRuntimeObject x) => new AnalysisBattleDiagnosticRuntimeObject { Kind = (int)x.Kind, RuntimeId = x.RuntimeId, Generation = x.Generation, DefinitionKind = (int)x.DefinitionKind, DefinitionId = x.DefinitionId, RelatedActorId = x.RelatedActorId, OwnerActorId = x.OwnerActorId, SourceActorId = x.SourceActorId, TargetActorId = x.TargetActorId, CreatedFrame = x.CreatedFrame, DestroyedFrame = x.DestroyedFrame, RootContextId = x.RootContextId, ContextId = x.ContextId, State = (int)x.State, EndReason = x.EndReason, DisplayName = x.DisplayName, DiscoveryKind = (int)x.DiscoveryKind, BackfilledFrame = x.BackfilledFrame, Completeness = (int)x.Completeness };
        private static AnalysisBattleDiagnosticObjectSummary ToDto(BattleDiagnosticRuntimeObjectCatalogSummary x) => new AnalysisBattleDiagnosticObjectSummary { TotalCount = x.TotalCount, CompleteCount = x.CompleteCount, PartialCount = x.PartialCount, UnreliableCount = x.UnreliableCount, ActiveCount = x.ActiveCount, EndedCount = x.EndedCount, Completeness = (int)x.Completeness, Truncated = x.Truncated, BackfillAttemptCount = x.BackfillAttemptCount, BackfillFailureCount = x.BackfillFailureCount, LastBackfillFrame = x.LastBackfillFrame };
        private static AnalysisBattleDiagnosticObjectEventCoverage ToDto(BattleDiagnosticRuntimeObjectEventCoverageSummary x) => new AnalysisBattleDiagnosticObjectEventCoverage { EventCount = x.EventCount, ReferencedEventCount = x.ReferencedEventCount, CompleteEventCount = x.CompleteEventCount, PartialEventCount = x.PartialEventCount, UnreliableEventCount = x.UnreliableEventCount, TotalReferenceCount = x.TotalReferenceCount, ResolvedReferenceCount = x.ResolvedReferenceCount, UnresolvedReferenceCount = x.UnresolvedReferenceCount, ResolvedReferenceRatio = x.ResolvedReferenceRatio };
        private static BattleDiagnosticRuntimeObject FromDto(AnalysisBattleDiagnosticRuntimeObject x) => new BattleDiagnosticRuntimeObject((BattleDiagnosticRuntimeObjectKind)x.Kind, x.RuntimeId, x.Generation, (BattleDiagnosticDefinitionKind)x.DefinitionKind, x.DefinitionId, x.RelatedActorId, x.OwnerActorId, x.SourceActorId, x.TargetActorId, x.CreatedFrame, x.DestroyedFrame, x.RootContextId, x.ContextId, (BattleDiagnosticRuntimeObjectState)x.State, x.EndReason, x.DisplayName, (BattleDiagnosticRuntimeObjectDiscoveryKind)x.DiscoveryKind, x.BackfilledFrame);
        private static AnalysisBattleDiagnosticDefinition ToDto(BattleDiagnosticDefinition x)
        {
            var result = new AnalysisBattleDiagnosticDefinition
            {
                Kind = (int)x.Kind,
                DefinitionId = x.DefinitionId,
                DisplayName = x.DisplayName,
                Revision = x.Revision,
                ContentHash = x.ContentHash,
                SourcePath = x.SourcePath,
                Resolution = (int)x.Resolution
            };
            Copy(x.Metadata, result.Metadata, ToDto);
            return result;
        }
        private static AnalysisBattleDiagnosticDefinitionMetadataEntry ToDto(BattleDiagnosticDefinitionMetadataEntry x) => new AnalysisBattleDiagnosticDefinitionMetadataEntry { Key = x.Key, ValueKind = (int)x.ValueKind, StringValue = x.StringValue, IntegerValue = x.IntegerValue, NumberValue = x.NumberValue, BooleanValue = x.BooleanValue };
        private static BattleDiagnosticDefinition FromDto(AnalysisBattleDiagnosticDefinition x)
        {
            if (x == null) throw new MobaBattleDiagnosticArtifactException(
                "BattleDiagnostics.Definition",
                "Definition catalog items cannot be null.");
            var reference = BattleDiagnosticDefinitionReference.Create(
                (BattleDiagnosticDefinitionKind)x.Kind,
                x.DefinitionId);
            var metadata = Convert(x.Metadata, FromDto);
            return new BattleDiagnosticDefinition(
                in reference,
                x.DisplayName,
                x.Revision,
                x.ContentHash,
                x.SourcePath,
                (BattleDiagnosticDefinitionResolution)x.Resolution,
                metadata);
        }
        private static BattleDiagnosticDefinitionMetadataEntry FromDto(AnalysisBattleDiagnosticDefinitionMetadataEntry x)
        {
            if (x == null) throw new MobaBattleDiagnosticArtifactException(
                "BattleDiagnostics.DefinitionMetadata",
                "Definition metadata items cannot be null.");
            switch ((BattleDiagnosticDefinitionMetadataValueKind)x.ValueKind)
            {
                case BattleDiagnosticDefinitionMetadataValueKind.String:
                    return BattleDiagnosticDefinitionMetadataEntry.String(x.Key, x.StringValue);
                case BattleDiagnosticDefinitionMetadataValueKind.Integer:
                    return BattleDiagnosticDefinitionMetadataEntry.Integer(x.Key, x.IntegerValue);
                case BattleDiagnosticDefinitionMetadataValueKind.Number:
                    return BattleDiagnosticDefinitionMetadataEntry.Number(x.Key, x.NumberValue);
                case BattleDiagnosticDefinitionMetadataValueKind.Boolean:
                    return BattleDiagnosticDefinitionMetadataEntry.Boolean(x.Key, x.BooleanValue);
                default:
                    throw new MobaBattleDiagnosticArtifactException(
                        "BattleDiagnostics.DefinitionMetadataKind",
                        "Definition metadata value kind is invalid: " + x.ValueKind);
            }
        }

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
