using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public sealed class BattleDiagnosticOfflineSession :
        IBattleDiagnosticReadOnlySession,
        IBattleDiagnosticRuntimeObjectCatalogSession,
        IBattleDiagnosticMetricSession,
        IDisposable
    {
        private readonly BattleDiagnosticSessionSnapshot _snapshot;
        private readonly HashSet<long> _stateActorIds;

        public BattleDiagnosticOfflineSession(BattleDiagnosticSessionSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _stateActorIds = new HashSet<long>();
            for (var i = 0; i < snapshot.State.Actors.Count; i++)
                _stateActorIds.Add(snapshot.State.Actors[i].ActorId);

            var source = snapshot.SessionInfo;
            SessionInfo = new BattleDiagnosticSessionInfo(
                source.Scope,
                source.DisplayName,
                source.BuildId,
                source.SchemaVersion,
                source.MonotonicTimestampFrequency,
                source.Capabilities,
                BattleDiagnosticConnectionState.Disconnected,
                BattleDiagnosticCaptureState.Frozen);
        }

        public BattleDiagnosticSessionInfo SessionInfo { get; }
        public long EventStoreRevision => _snapshot.Events.Revision;
        public long StateStoreRevision => _snapshot.State.Revision;
        public long TraceStoreRevision => _snapshot.Trace.Revision;
        public long ActorAttributeStoreRevision => _snapshot.Attributes.Revision;
        public long ActorBuffStoreRevision => _snapshot.Buffs.Revision;
        public long ActorTagStoreRevision => _snapshot.Tags.Revision;
        public long ActorEffectStoreRevision => _snapshot.Effects.Revision;
        public long RuntimeObjectStoreRevision => _snapshot.Objects.Revision;
        public long MetricStoreRevision => _snapshot.FrameMetrics.Revision;
        public long StoreRevision => EventStoreRevision;

        public BattleDiagnosticQueryResult<BattleDiagnosticMetricSample> QueryMetrics(
            BattleDiagnosticMetricQuery query)
        {
            if (!SessionInfo.Supports(BattleDiagnosticCapabilities.FrameMetrics))
                return Unsupported<BattleDiagnosticMetricSample>(
                    query.RequestId,
                    MetricStoreRevision,
                    "frame metric history");
            if (query.Page.StoreRevision > 0L && query.Page.StoreRevision != MetricStoreRevision)
                return Unavailable<BattleDiagnosticMetricSample>(
                    query.RequestId,
                    query.Page.StoreRevision,
                    BattleDiagnosticDataAvailability.Evicted,
                    "The requested metric store revision is not present in this offline artifact.");

            var result = new List<BattleDiagnosticMetricSample>(
                Math.Min(query.Page.Limit, _snapshot.FrameMetrics.Samples.Count));
            var skipped = 0;
            var hasMore = false;
            for (var i = 0; i < _snapshot.FrameMetrics.Samples.Count; i++)
            {
                var item = _snapshot.FrameMetrics.Samples[i];
                if (!query.Matches(in item)) continue;
                if (skipped++ < query.Page.Offset) continue;
                if (result.Count == query.Page.Limit)
                {
                    hasMore = true;
                    break;
                }
                result.Add(item);
            }
            return BattleDiagnosticQueryResult<BattleDiagnosticMetricSample>.FromItems(
                query.RequestId,
                MetricStoreRevision,
                result,
                hasMore);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary> QueryWorld(long requestId, int frame)
        {
            ValidateRequest(requestId);
            if (!BattleDiagnosticFrames.IsValid(frame))
                return Unavailable<BattleDiagnosticWorldSummary>(requestId, StateStoreRevision, BattleDiagnosticDataAvailability.NotProduced, "Invalid frame.");
            if (!_snapshot.State.World.HasValue)
                return Unavailable<BattleDiagnosticWorldSummary>(requestId, StateStoreRevision, BattleDiagnosticDataAvailability.NotProduced, "No world state was captured.");
            if (frame != 0 && frame != _snapshot.State.Frame)
                return Unavailable<BattleDiagnosticWorldSummary>(requestId, StateStoreRevision, BattleDiagnosticDataAvailability.NotCaptured, LatestMessage(frame, _snapshot.State.Frame, "state"));
            return Ready(requestId, StateStoreRevision, new[] { _snapshot.State.World.Value });
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> QueryActors(long requestId, int frame)
        {
            ValidateRequest(requestId);
            if (!TryValidateFrame(requestId, frame, _snapshot.State.Frame, StateStoreRevision, "state", out BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> unavailable))
                return unavailable;
            return Ready(requestId, StateStoreRevision, _snapshot.State.Actors);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticEvent> QueryEvents(BattleDiagnosticEventQuery query)
        {
            var requestedRevision = query.Page.StoreRevision;
            if (requestedRevision > 0 && requestedRevision != EventStoreRevision)
                return Unavailable<BattleDiagnosticEvent>(query.RequestId, requestedRevision, BattleDiagnosticDataAvailability.Evicted, "The requested store revision is not present in this offline artifact.");

            var matches = new List<BattleDiagnosticEvent>(Math.Min(query.Page.Limit, _snapshot.Events.Events.Count));
            var skipped = 0;
            var hasMore = false;
            for (var i = 0; i < _snapshot.Events.Events.Count; i++)
            {
                var item = _snapshot.Events.Events[i];
                if (!Matches(item, query.Filter)) continue;
                if (skipped++ < query.Page.Offset) continue;
                if (matches.Count == query.Page.Limit)
                {
                    hasMore = true;
                    break;
                }
                matches.Add(item);
            }
            return BattleDiagnosticQueryResult<BattleDiagnosticEvent>.FromItems(query.RequestId, EventStoreRevision, matches, hasMore);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObject(
            long requestId,
            in BattleDiagnosticRuntimeObjectReference reference,
            int frame)
        {
            ValidateRequest(requestId);
            if (!reference.HasRuntimeId) throw new ArgumentException(
                "A runtime object reference with an ID is required.",
                nameof(reference));
            if (!SessionInfo.Supports(BattleDiagnosticCapabilities.RuntimeObjects))
                return Unavailable<BattleDiagnosticRuntimeObject>(
                    requestId,
                    RuntimeObjectStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This artifact does not provide a runtime object catalog.");
            if (_snapshot.Objects.TryResolve(in reference, frame, out var runtimeObject))
                return Ready(requestId, RuntimeObjectStoreRevision, new[] { runtimeObject });

            return Unavailable<BattleDiagnosticRuntimeObject>(
                requestId,
                RuntimeObjectStoreRevision,
                _snapshot.Objects.Truncated
                    ? BattleDiagnosticDataAvailability.Truncated
                    : BattleDiagnosticDataAvailability.NotCaptured,
                _snapshot.Objects.Truncated
                    ? "The runtime object may have been evicted from the bounded catalog."
                    : "The runtime object was not captured.");
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObjects(
            BattleDiagnosticRuntimeObjectQuery query)
        {
            if (!SessionInfo.Supports(BattleDiagnosticCapabilities.RuntimeObjects))
                return Unavailable<BattleDiagnosticRuntimeObject>(
                    query.RequestId,
                    RuntimeObjectStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This artifact does not provide a runtime object catalog.");
            if (query.Page.StoreRevision > 0L &&
                query.Page.StoreRevision != RuntimeObjectStoreRevision)
                return Unavailable<BattleDiagnosticRuntimeObject>(
                    query.RequestId,
                    query.Page.StoreRevision,
                    BattleDiagnosticDataAvailability.Evicted,
                    "The requested runtime object catalog revision is not present in this artifact.");

            var results = new List<BattleDiagnosticRuntimeObject>(query.Page.Limit);
            var skipped = 0;
            var hasMore = false;
            for (var i = 0; i < _snapshot.Objects.Items.Count; i++)
            {
                var item = _snapshot.Objects.Items[i];
                if (!query.Filter.Matches(in item)) continue;
                if (skipped++ < query.Page.Offset) continue;
                if (results.Count == query.Page.Limit)
                {
                    hasMore = true;
                    break;
                }
                results.Add(item);
            }

            return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.FromItems(
                query.RequestId,
                RuntimeObjectStoreRevision,
                results,
                hasMore);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObjectCatalogSummary>
            QueryRuntimeObjectSummary(long requestId)
        {
            ValidateRequest(requestId);
            if (!SessionInfo.Supports(BattleDiagnosticCapabilities.RuntimeObjects))
                return Unavailable<BattleDiagnosticRuntimeObjectCatalogSummary>(
                    requestId,
                    RuntimeObjectStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This artifact does not provide a runtime object catalog.");
            return Ready(
                requestId,
                RuntimeObjectStoreRevision,
                new[] { _snapshot.Objects.Summary });
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary> QueryTrace(long requestId, long rootContextId)
        {
            ValidateRequest(requestId);
            if (rootContextId == 0) throw new ArgumentOutOfRangeException(nameof(rootContextId));
            if (!SessionInfo.Supports(BattleDiagnosticCapabilities.Trace))
                return Unavailable<BattleDiagnosticTraceNodeSummary>(requestId, TraceStoreRevision, BattleDiagnosticDataAvailability.Unsupported, "This artifact does not provide trace graph queries.");

            var result = new List<BattleDiagnosticTraceNodeSummary>();
            for (var i = 0; i < _snapshot.Trace.Nodes.Count; i++)
                if (_snapshot.Trace.Nodes[i].RootContextId == rootContextId) result.Add(_snapshot.Trace.Nodes[i]);

            if (result.Count == 0)
                return Ready(requestId, TraceStoreRevision, result);
            if (_snapshot.Trace.Truncated || !_snapshot.Trace.IsStable)
            {
                var availability = BattleDiagnosticDataAvailability.Truncated;
                var message = !_snapshot.Trace.IsStable
                    ? "Trace capture changed while the artifact snapshot was created."
                    : "Trace capture was truncated during export.";
                return new BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>(
                    BattleDiagnosticQueryStatus.Partial(requestId, TraceStoreRevision, result.Count, availability, message),
                    result);
            }
            return Ready(requestId, TraceStoreRevision, result);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(long requestId, int frame, long actorId)
        {
            ValidateActorRequest(requestId, actorId);
            if (!SessionInfo.Supports(BattleDiagnosticCapabilities.ActorAttributes)) return Unsupported<BattleDiagnosticActorAttribute>(requestId, ActorAttributeStoreRevision, "actor attribute");
            if (!TryValidateFrame(requestId, frame, _snapshot.Attributes.Frame, ActorAttributeStoreRevision, "attribute", out BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> unavailable)) return unavailable;
            if (!ContainsActor(actorId, _snapshot.Attributes.Frame, _snapshot.Attributes.Attributes)) return MissingActor<BattleDiagnosticActorAttribute>(requestId, ActorAttributeStoreRevision, actorId, _snapshot.Attributes.Frame, "attribute");
            return FilterByActor(requestId, ActorAttributeStoreRevision, actorId, _snapshot.Attributes.Attributes, item => item.ActorId);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(long requestId, int frame, long actorId)
        {
            ValidateActorRequest(requestId, actorId);
            if (!SessionInfo.Supports(BattleDiagnosticCapabilities.ActorAttributes)) return Unsupported<BattleDiagnosticActorAttributeModifier>(requestId, ActorAttributeStoreRevision, "actor attribute modifier");
            if (!TryValidateFrame(requestId, frame, _snapshot.Attributes.Frame, ActorAttributeStoreRevision, "attribute", out BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> unavailable)) return unavailable;
            if (!ContainsActor(actorId, _snapshot.Attributes.Frame, _snapshot.Attributes.Attributes)) return MissingActor<BattleDiagnosticActorAttributeModifier>(requestId, ActorAttributeStoreRevision, actorId, _snapshot.Attributes.Frame, "attribute");
            return FilterByActor(requestId, ActorAttributeStoreRevision, actorId, _snapshot.Attributes.Modifiers, item => item.ActorId);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorBuff> QueryActorBuffs(long requestId, int frame, long actorId)
        {
            ValidateActorRequest(requestId, actorId);
            return QueryLatest(requestId, frame, actorId, BattleDiagnosticCapabilities.ActorBuffs, ActorBuffStoreRevision, "buff", _snapshot.Buffs, item => item.ActorId);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorTag> QueryActorTags(long requestId, int frame, long actorId)
        {
            ValidateActorRequest(requestId, actorId);
            return QueryLatest(requestId, frame, actorId, BattleDiagnosticCapabilities.ActorTags, ActorTagStoreRevision, "tag", _snapshot.Tags, item => item.ActorId);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorEffect> QueryActorEffects(long requestId, int frame, long actorId)
        {
            ValidateActorRequest(requestId, actorId);
            return QueryLatest(requestId, frame, actorId, BattleDiagnosticCapabilities.ActorEffects, ActorEffectStoreRevision, "effect", _snapshot.Effects, item => item.ActorId);
        }

        public void Dispose() { }

        private BattleDiagnosticQueryResult<T> QueryLatest<T>(long requestId, int frame, long actorId, BattleDiagnosticCapabilities capability, long revision, string label, BattleDiagnosticLatestTrackSnapshot<T> track, Func<T, long> actorIdSelector)
        {
            if (!SessionInfo.Supports(capability)) return Unsupported<T>(requestId, revision, "actor " + label);
            if (!TryValidateFrame(requestId, frame, track.Frame, revision, label, out BattleDiagnosticQueryResult<T> unavailable)) return unavailable;
            if (!ContainsActor(actorId, track.Frame, track.Items, actorIdSelector)) return MissingActor<T>(requestId, revision, actorId, track.Frame, label);
            return FilterByActor(requestId, revision, actorId, track.Items, actorIdSelector);
        }

        private bool ContainsActor<T>(long actorId, int frame, IReadOnlyList<T> items, Func<T, long> actorIdSelector)
        {
            if (frame == _snapshot.State.Frame && _stateActorIds.Contains(actorId)) return true;
            for (var i = 0; i < items.Count; i++) if (actorIdSelector(items[i]) == actorId) return true;
            return false;
        }

        private bool ContainsActor(long actorId, int frame, IReadOnlyList<BattleDiagnosticActorAttribute> items)
        {
            return ContainsActor(actorId, frame, items, item => item.ActorId);
        }

        private static BattleDiagnosticQueryResult<T> FilterByActor<T>(long requestId, long revision, long actorId, IReadOnlyList<T> items, Func<T, long> actorIdSelector)
        {
            var result = new List<T>();
            for (var i = 0; i < items.Count; i++) if (actorIdSelector(items[i]) == actorId) result.Add(items[i]);
            return Ready(requestId, revision, result);
        }

        private static bool TryValidateFrame<T>(long requestId, int frame, int snapshotFrame, long revision, string label, out BattleDiagnosticQueryResult<T> unavailable)
        {
            if (!BattleDiagnosticFrames.IsValid(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
            if (snapshotFrame == BattleDiagnosticFrames.Invalid)
            {
                unavailable = Unavailable<T>(requestId, revision, BattleDiagnosticDataAvailability.NotProduced, "No " + label + " snapshot was captured.");
                return false;
            }
            if (frame != 0 && frame != snapshotFrame)
            {
                unavailable = Unavailable<T>(requestId, revision, BattleDiagnosticDataAvailability.NotCaptured, LatestMessage(frame, snapshotFrame, label));
                return false;
            }
            unavailable = default;
            return true;
        }

        private static bool Matches(BattleDiagnosticEvent item, BattleDiagnosticFilter filter)
        {
            if (!filter.Frames.Contains(item.Frame) || (filter.Channels & item.Channel) == 0) return false;
            if (filter.ConfigId != 0 && filter.ConfigId != item.ConfigId) return false;
            if (filter.RootContextId != 0 && filter.RootContextId != item.RootContextId) return false;
            if (filter.ContextId != 0 && filter.ContextId != item.ContextId) return false;
            if (filter.SkillRuntimeId != 0 && filter.SkillRuntimeId != item.SkillRuntime.RuntimeId) return false;
            if (filter.AttackId != 0 && filter.AttackId != item.AttackId) return false;
            if (filter.FailuresOnly && !item.IsFailure || filter.UnfinishedOnly && !item.IsUnfinished) return false;
            if (!MatchesTriggerAnalysisFilter(in item, filter)) return false;
            if (filter.HasTextSearch && !MatchesSearchText(in item, filter.SearchText)) return false;
            if (!filter.HasActorFilter) return true;
            if (filter.ActorRelation == BattleDiagnosticActorRelation.Source) return item.SourceActorId == filter.ActorId;
            if (filter.ActorRelation == BattleDiagnosticActorRelation.Target) return item.TargetActorId == filter.ActorId;
            return item.SourceActorId == filter.ActorId || item.TargetActorId == filter.ActorId;
        }

        private static bool MatchesTriggerAnalysisFilter(
            in BattleDiagnosticEvent item,
            BattleDiagnosticFilter filter)
        {
            if (!filter.HasTriggerAnalysisFilter) return true;
            if (!item.Payload.TryGetTriggerAnalysis(out var trigger)) return false;
            if (filter.TriggerStage != BattleDiagnosticTriggerAnalysisStage.Unknown &&
                trigger.Stage != filter.TriggerStage)
            {
                return false;
            }

            if (filter.TriggerResult != BattleDiagnosticTriggerAnalysisResult.Unknown &&
                trigger.Result != filter.TriggerResult)
            {
                return false;
            }

            if (filter.TriggerContextKind != 0 && trigger.ContextKind != filter.TriggerContextKind) return false;
            if (filter.TriggerOriginKind != 0 && trigger.OriginKind != filter.TriggerOriginKind) return false;
            return true;
        }

        private static bool MatchesSearchText(in BattleDiagnosticEvent item, string searchText)
        {
            if (item.Summary.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (item.Kind.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (item.Channel.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (item.Outcome.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (item.Payload.TryGetTriggerAnalysis(out var trigger) &&
                MatchesTriggerSearch(in trigger, searchText))
            {
                return true;
            }

            return MatchesNumber(item.Sequence, searchText) ||
                   MatchesNumber(item.Frame, searchText) ||
                   MatchesNumber(item.SourceActorId, searchText) ||
                   MatchesNumber(item.TargetActorId, searchText) ||
                   MatchesNumber(item.ConfigId, searchText) ||
                   MatchesNumber(item.RootContextId, searchText) ||
                   MatchesNumber(item.ContextId, searchText) ||
                   MatchesNumber(item.SkillRuntime.RuntimeId, searchText) ||
                   MatchesNumber(item.AttackId, searchText);
        }

        private static bool MatchesTriggerSearch(
            in BattleDiagnosticTriggerAnalysisPayload trigger,
            string searchText)
        {
            return trigger.Stage.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trigger.Result.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trigger.FailureKey.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trigger.Reason.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   MatchesNumber(trigger.TriggerId, searchText) ||
                   MatchesNumber(trigger.ContextKind, searchText) ||
                   MatchesNumber(trigger.OriginKind, searchText) ||
                   MatchesNumber(trigger.DetailCode, searchText) ||
                   MatchesNumber(trigger.CurrentDepth, searchText) ||
                   MatchesNumber(trigger.CurrentFrameCount, searchText) ||
                   MatchesNumber(trigger.CurrentRootCount, searchText) ||
                   MatchesNumber(trigger.CurrentSameTriggerCount, searchText);
        }

        private static bool MatchesNumber(long value, string searchText)
        {
            return value != 0 &&
                   value.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ValidateRequest(long requestId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
        }

        private static void ValidateActorRequest(long requestId, long actorId)
        {
            ValidateRequest(requestId);
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));
        }

        private static string LatestMessage(int requested, int latest, string label) => $"Requested frame {requested} is unavailable; latest-only {label} snapshot is frame {latest}.";
        private static BattleDiagnosticQueryResult<T> Ready<T>(long requestId, long revision, IReadOnlyList<T> items) => BattleDiagnosticQueryResult<T>.FromItems(requestId, revision, new List<T>(items), false);
        private static BattleDiagnosticQueryResult<T> Unavailable<T>(long requestId, long revision, BattleDiagnosticDataAvailability availability, string message) => BattleDiagnosticQueryResult<T>.Unavailable(requestId, revision, availability, message);
        private static BattleDiagnosticQueryResult<T> Unsupported<T>(long requestId, long revision, string label) => Unavailable<T>(requestId, revision, BattleDiagnosticDataAvailability.Unsupported, "This artifact does not provide " + label + " queries.");
        private static BattleDiagnosticQueryResult<T> MissingActor<T>(long requestId, long revision, long actorId, int frame, string label) => Unavailable<T>(requestId, revision, BattleDiagnosticDataAvailability.NotCaptured, $"Actor {actorId} is not present in {label} snapshot frame {frame}.");
    }
}
