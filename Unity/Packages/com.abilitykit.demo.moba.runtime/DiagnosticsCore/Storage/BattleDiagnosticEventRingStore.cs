using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public readonly struct BattleDiagnosticStoreMetrics : IEquatable<BattleDiagnosticStoreMetrics>
    {
        public BattleDiagnosticStoreMetrics(
            int capacity,
            int count,
            long revision,
            long acceptedCount,
            long evictedCount,
            long rejectedCount,
            bool isFrozen)
        {
            Capacity = capacity;
            Count = count;
            Revision = revision;
            AcceptedCount = acceptedCount;
            EvictedCount = evictedCount;
            RejectedCount = rejectedCount;
            IsFrozen = isFrozen;
        }

        public int Capacity { get; }
        public int Count { get; }
        public long Revision { get; }
        public long AcceptedCount { get; }
        public long EvictedCount { get; }
        public long RejectedCount { get; }
        public bool IsFrozen { get; }

        public bool Equals(BattleDiagnosticStoreMetrics other)
        {
            return Capacity == other.Capacity && Count == other.Count && Revision == other.Revision &&
                   AcceptedCount == other.AcceptedCount && EvictedCount == other.EvictedCount &&
                   RejectedCount == other.RejectedCount && IsFrozen == other.IsFrozen;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticStoreMetrics other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Capacity;
                hashCode = (hashCode * 397) ^ Count;
                hashCode = (hashCode * 397) ^ Revision.GetHashCode();
                hashCode = (hashCode * 397) ^ AcceptedCount.GetHashCode();
                hashCode = (hashCode * 397) ^ EvictedCount.GetHashCode();
                hashCode = (hashCode * 397) ^ RejectedCount.GetHashCode();
                hashCode = (hashCode * 397) ^ IsFrozen.GetHashCode();
                return hashCode;
            }
        }
    }

    public interface IBattleDiagnosticEventReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }
        BattleDiagnosticQueryResult<BattleDiagnosticEvent> Query(BattleDiagnosticEventQuery query);
    }

    public sealed class BattleDiagnosticEventRingStore :
        IBattleDiagnosticEventReadStore,
        IBattleDiagnosticEventSnapshotSource
    {
        public const int DefaultCapacity = 20000;
        public const int DefaultRetainedReadViewCount = 4;

        private readonly BattleDiagnosticEvent[] _buffer;
        private readonly Dictionary<long, BattleDiagnosticEvent[]> _readViews;
        private readonly Queue<long> _readViewOrder;
        private readonly int _retainedReadViewCount;
        private int _head;
        private int _count;
        private long _revision;
        private long _lastSequence;
        private long _acceptedCount;
        private long _evictedCount;
        private long _rejectedCount;
        private bool _isFrozen;

        public BattleDiagnosticEventRingStore(
            BattleDiagnosticSessionScope scope,
            int capacity = DefaultCapacity,
            int retainedReadViewCount = DefaultRetainedReadViewCount)
        {
            if (!scope.IsValid) throw new ArgumentException("A valid session scope is required.", nameof(scope));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (retainedReadViewCount <= 0) throw new ArgumentOutOfRangeException(nameof(retainedReadViewCount));

            Scope = scope;
            _buffer = new BattleDiagnosticEvent[capacity];
            _retainedReadViewCount = retainedReadViewCount;
            _readViews = new Dictionary<long, BattleDiagnosticEvent[]>(retainedReadViewCount);
            _readViewOrder = new Queue<long>(retainedReadViewCount);
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public int Capacity => _buffer.Length;
        public int Count => _count;
        public long Revision => _revision;
        public bool IsFrozen => _isFrozen;

        public BattleDiagnosticStoreMetrics Metrics => new BattleDiagnosticStoreMetrics(
            Capacity,
            _count,
            _revision,
            _acceptedCount,
            _evictedCount,
            _rejectedCount,
            _isFrozen);

        public bool TryAppend(BattleDiagnosticEvent diagnosticEvent)
        {
            if (_isFrozen || diagnosticEvent.Scope != Scope || diagnosticEvent.Sequence <= _lastSequence)
            {
                _rejectedCount++;
                return false;
            }

            if (_count == Capacity)
            {
                _buffer[_head] = diagnosticEvent;
                _head = (_head + 1) % Capacity;
                _evictedCount++;
            }
            else
            {
                var tail = (_head + _count) % Capacity;
                _buffer[tail] = diagnosticEvent;
                _count++;
            }

            _lastSequence = diagnosticEvent.Sequence;
            _acceptedCount++;
            _revision++;
            return true;
        }

        public void SetFrozen(bool frozen)
        {
            _isFrozen = frozen;
        }

        public void Clear()
        {
            if (_count == 0)
            {
                return;
            }

            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
            _revision++;
            ClearReadViews();
        }

        public BattleDiagnosticEventTrackSnapshot CaptureEventSnapshot()
        {
            var events = new BattleDiagnosticEvent[_count];
            for (var index = 0; index < _count; index++)
            {
                events[index] = _buffer[(_head + index) % Capacity];
            }

            var metrics = Metrics;
            return new BattleDiagnosticEventTrackSnapshot(_revision, in metrics, events);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticEvent> Query(BattleDiagnosticEventQuery query)
        {
            var requestedRevision = query.Page.StoreRevision;
            BattleDiagnosticEvent[] readView;

            if (requestedRevision <= 0 || requestedRevision == _revision)
            {
                requestedRevision = _revision;
                readView = GetOrCreateCurrentReadView();
            }
            else if (!_readViews.TryGetValue(requestedRevision, out readView))
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticEvent>.Unavailable(
                    query.RequestId,
                    requestedRevision,
                    BattleDiagnosticDataAvailability.Evicted,
                    "The requested store revision is no longer retained.");
            }

            var matches = new List<BattleDiagnosticEvent>(Math.Min(query.Page.Limit, readView.Length));
            var skipped = 0;
            var hasMore = false;
            var recentFirstFrame = ResolveRecentFirstFrame(readView, query.RecentFrameCount);

            for (var position = 0; position < readView.Length; position++)
            {
                var index = query.NewestFirst
                    ? readView.Length - 1 - position
                    : position;
                var diagnosticEvent = readView[index];
                if (diagnosticEvent.Frame < recentFirstFrame || !Matches(diagnosticEvent, query.Filter))
                {
                    continue;
                }

                if (skipped < query.Page.Offset)
                {
                    skipped++;
                    continue;
                }

                if (matches.Count == query.Page.Limit)
                {
                    hasMore = true;
                    break;
                }

                matches.Add(diagnosticEvent);
            }

            return BattleDiagnosticQueryResult<BattleDiagnosticEvent>.FromItems(
                query.RequestId,
                requestedRevision,
                matches,
                hasMore);
        }

        private BattleDiagnosticEvent[] GetOrCreateCurrentReadView()
        {
            if (_readViews.TryGetValue(_revision, out var existing))
            {
                return existing;
            }

            var readView = new BattleDiagnosticEvent[_count];
            for (var index = 0; index < _count; index++)
            {
                readView[index] = _buffer[(_head + index) % Capacity];
            }

            _readViews.Add(_revision, readView);
            _readViewOrder.Enqueue(_revision);
            while (_readViewOrder.Count > _retainedReadViewCount)
            {
                var oldestRevision = _readViewOrder.Dequeue();
                _readViews.Remove(oldestRevision);
            }

            return readView;
        }

        private void ClearReadViews()
        {
            _readViews.Clear();
            _readViewOrder.Clear();
        }

        private static int ResolveRecentFirstFrame(BattleDiagnosticEvent[] readView, int recentFrameCount)
        {
            if (recentFrameCount <= 0 || readView.Length == 0) return int.MinValue;

            var latestFrame = readView[readView.Length - 1].Frame;
            return latestFrame - recentFrameCount + 1;
        }

        private static bool Matches(BattleDiagnosticEvent diagnosticEvent, BattleDiagnosticFilter filter)
        {
            if (!filter.Frames.Contains(diagnosticEvent.Frame)) return false;
            if ((filter.Channels & diagnosticEvent.Channel) == 0) return false;
            if (filter.ConfigId != 0 && filter.ConfigId != diagnosticEvent.ConfigId) return false;
            if (filter.RootContextId != 0 && filter.RootContextId != diagnosticEvent.RootContextId) return false;
            if (filter.ContextId != 0 && filter.ContextId != diagnosticEvent.ContextId) return false;
            if (filter.SkillRuntimeId != 0 && filter.SkillRuntimeId != diagnosticEvent.SkillRuntime.RuntimeId) return false;
            if (filter.AttackId != 0 && filter.AttackId != diagnosticEvent.AttackId) return false;
            if (filter.FailuresOnly && !diagnosticEvent.IsFailure) return false;
            if (filter.UnfinishedOnly && !diagnosticEvent.IsUnfinished) return false;
            if (!MatchesTriggerAnalysisFilter(in diagnosticEvent, filter)) return false;
            if (filter.HasTextSearch && !MatchesSearchText(diagnosticEvent, filter.SearchText)) return false;

            if (!filter.HasActorFilter)
            {
                return true;
            }

            switch (filter.ActorRelation)
            {
                case BattleDiagnosticActorRelation.Source:
                    return diagnosticEvent.SourceActorId == filter.ActorId;
                case BattleDiagnosticActorRelation.Target:
                    return diagnosticEvent.TargetActorId == filter.ActorId;
                case BattleDiagnosticActorRelation.Either:
                    return diagnosticEvent.SourceActorId == filter.ActorId ||
                           diagnosticEvent.TargetActorId == filter.ActorId;
                default:
                    return diagnosticEvent.SourceActorId == filter.ActorId ||
                           diagnosticEvent.TargetActorId == filter.ActorId;
            }
        }

        private static bool MatchesSearchText(BattleDiagnosticEvent diagnosticEvent, string searchText)
        {
            if (diagnosticEvent.Summary.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (diagnosticEvent.Kind.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (diagnosticEvent.Channel.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (diagnosticEvent.Outcome.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if (diagnosticEvent.Payload.TryGetTriggerAnalysis(out var trigger) &&
                MatchesTriggerSearch(in trigger, searchText))
            {
                return true;
            }

            if (diagnosticEvent.Payload.TryGetSkillFailure(out var skillFailure) &&
                MatchesSkillFailureSearch(in skillFailure, searchText))
            {
                return true;
            }

            return MatchesNumber(diagnosticEvent.Sequence, searchText) ||
                   MatchesNumber(diagnosticEvent.Frame, searchText) ||
                   MatchesNumber(diagnosticEvent.SourceActorId, searchText) ||
                   MatchesNumber(diagnosticEvent.TargetActorId, searchText) ||
                   MatchesNumber(diagnosticEvent.ConfigId, searchText) ||
                   MatchesNumber(diagnosticEvent.RootContextId, searchText) ||
                   MatchesNumber(diagnosticEvent.ContextId, searchText) ||
                   MatchesNumber(diagnosticEvent.SkillRuntime.RuntimeId, searchText) ||
                   MatchesNumber(diagnosticEvent.AttackId, searchText);
        }

        private static bool MatchesTriggerAnalysisFilter(
            in BattleDiagnosticEvent diagnosticEvent,
            BattleDiagnosticFilter filter)
        {
            if (!filter.HasTriggerAnalysisFilter) return true;
            if (!diagnosticEvent.Payload.TryGetTriggerAnalysis(out var trigger)) return false;
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

            if (filter.TriggerContextKind != 0 &&
                trigger.ContextKind != filter.TriggerContextKind)
            {
                return false;
            }

            if (filter.TriggerOriginKind != 0 &&
                trigger.OriginKind != filter.TriggerOriginKind)
            {
                return false;
            }

            return true;
        }

        private static bool MatchesSkillFailureSearch(
            in BattleDiagnosticSkillFailurePayload failure,
            string searchText)
        {
            return failure.Source.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   failure.Stage.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   failure.Code.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   failure.Message.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   MatchesNumber(failure.Slot, searchText);
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
    }
}
