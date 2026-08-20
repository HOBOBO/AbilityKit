using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public readonly struct BattleDiagnosticQueryResult<T>
    {
        public BattleDiagnosticQueryResult(BattleDiagnosticQueryStatus status, IList<T> items)
        {
            Status = status;
            Items = new ReadOnlyCollection<T>(items == null ? Array.Empty<T>() : new List<T>(items));
        }

        public BattleDiagnosticQueryStatus Status { get; }
        public IReadOnlyList<T> Items { get; }

        public static BattleDiagnosticQueryResult<T> FromItems(
            long requestId,
            long storeRevision,
            IList<T> items,
            bool hasMore)
        {
            var count = items?.Count ?? 0;
            return new BattleDiagnosticQueryResult<T>(
                BattleDiagnosticQueryStatus.Ready(requestId, storeRevision, count, hasMore),
                items);
        }

        public static BattleDiagnosticQueryResult<T> Unavailable(
            long requestId,
            long storeRevision,
            BattleDiagnosticDataAvailability availability,
            string message = "")
        {
            return new BattleDiagnosticQueryResult<T>(
                BattleDiagnosticQueryStatus.Unavailable(requestId, storeRevision, availability, message),
                Array.Empty<T>());
        }

        public static BattleDiagnosticQueryResult<T> Failed(
            long requestId,
            long storeRevision,
            string errorCode,
            string message)
        {
            return new BattleDiagnosticQueryResult<T>(
                BattleDiagnosticQueryStatus.Failed(requestId, storeRevision, errorCode, message),
                Array.Empty<T>());
        }
    }

    public readonly struct BattleDiagnosticEventQuery : IEquatable<BattleDiagnosticEventQuery>
    {
        public BattleDiagnosticEventQuery(
            long requestId,
            BattleDiagnosticFilter filter,
            BattleDiagnosticPageRequest page,
            bool newestFirst = false,
            int recentFrameCount = 0)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (recentFrameCount < 0) throw new ArgumentOutOfRangeException(nameof(recentFrameCount));

            RequestId = requestId;
            Filter = filter;
            Page = page;
            NewestFirst = newestFirst;
            RecentFrameCount = recentFrameCount;
        }

        public long RequestId { get; }
        public BattleDiagnosticFilter Filter { get; }
        public BattleDiagnosticPageRequest Page { get; }
        public bool NewestFirst { get; }
        public int RecentFrameCount { get; }

        public bool Equals(BattleDiagnosticEventQuery other)
        {
            return RequestId == other.RequestId &&
                   Filter.Equals(other.Filter) &&
                   Page.Equals(other.Page) &&
                   NewestFirst == other.NewestFirst &&
                   RecentFrameCount == other.RecentFrameCount;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticEventQuery other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RequestId.GetHashCode();
                hashCode = (hashCode * 397) ^ Filter.GetHashCode();
                hashCode = (hashCode * 397) ^ Page.GetHashCode();
                hashCode = (hashCode * 397) ^ NewestFirst.GetHashCode();
                hashCode = (hashCode * 397) ^ RecentFrameCount;
                return hashCode;
            }
        }
    }

    public interface IBattleDiagnosticActorAttributeReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }
        int SnapshotFrame { get; }

        BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(
            long requestId,
            int frame,
            long actorId);

        BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(
            long requestId,
            int frame,
            long actorId);
    }

    public interface IBattleDiagnosticActorBuffReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }
        int SnapshotFrame { get; }

        BattleDiagnosticQueryResult<BattleDiagnosticActorBuff> QueryActorBuffs(
            long requestId,
            int frame,
            long actorId);
    }

    public interface IBattleDiagnosticActorEffectReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }
        int SnapshotFrame { get; }

        BattleDiagnosticQueryResult<BattleDiagnosticActorEffect> QueryActorEffects(
            long requestId,
            int frame,
            long actorId);
    }

    public interface IBattleDiagnosticActorTagReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }
        int SnapshotFrame { get; }

        BattleDiagnosticQueryResult<BattleDiagnosticActorTag> QueryActorTags(
            long requestId,
            int frame,
            long actorId);
    }

    public interface IBattleDiagnosticTraceReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }

        BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary> QueryTrace(
            long requestId,
            long rootContextId);
    }

    public readonly struct BattleDiagnosticMetricQuery
    {
        public BattleDiagnosticMetricQuery(
            long requestId,
            BattleDiagnosticFrameRange frames,
            BattleDiagnosticPageRequest page,
            BattleDiagnosticMetricCategory category = BattleDiagnosticMetricCategory.Unknown,
            string metric = "",
            string dimension = "")
        {
            if (requestId <= 0L) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (!frames.IsValid) throw new ArgumentException("A valid frame range is required.", nameof(frames));
            if (!Enum.IsDefined(typeof(BattleDiagnosticMetricCategory), category))
                throw new ArgumentOutOfRangeException(nameof(category));
            RequestId = requestId;
            Frames = frames;
            Page = page;
            Category = category;
            Metric = metric ?? string.Empty;
            Dimension = dimension ?? string.Empty;
        }

        public long RequestId { get; }
        public BattleDiagnosticFrameRange Frames { get; }
        public BattleDiagnosticPageRequest Page { get; }
        public BattleDiagnosticMetricCategory Category { get; }
        public string Metric { get; }
        public string Dimension { get; }

        public bool Matches(in BattleDiagnosticMetricSample sample)
        {
            return Frames.Contains(sample.Frame) &&
                   (Category == BattleDiagnosticMetricCategory.Unknown || sample.Category == Category) &&
                   (string.IsNullOrEmpty(Metric) || string.Equals(sample.Metric, Metric, StringComparison.Ordinal)) &&
                   (string.IsNullOrEmpty(Dimension) || string.Equals(sample.Dimension, Dimension, StringComparison.Ordinal));
        }
    }

    public interface IBattleDiagnosticMetricReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }
        BattleDiagnosticQueryResult<BattleDiagnosticMetricSample> QueryMetrics(BattleDiagnosticMetricQuery query);
    }

    public interface IBattleDiagnosticMetricSink
    {
        bool IsEnabled { get; }

        bool TryRecordMetric(
            int frame,
            long monotonicTimestamp,
            BattleDiagnosticMetricCategory category,
            BattleDiagnosticMetricValueKind valueKind,
            string metric,
            double value,
            string dimension = "");
    }

    public interface IBattleDiagnosticMetricSession
    {
        long MetricStoreRevision { get; }
        BattleDiagnosticQueryResult<BattleDiagnosticMetricSample> QueryMetrics(BattleDiagnosticMetricQuery query);
    }

    public interface IBattleDiagnosticRuntimeObjectReadStore
    {
        BattleDiagnosticSessionScope Scope { get; }
        long Revision { get; }

        BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObject(
            long requestId,
            in BattleDiagnosticRuntimeObjectReference reference,
            int frame);
    }

    public readonly struct BattleDiagnosticRuntimeObjectFilter : IEquatable<BattleDiagnosticRuntimeObjectFilter>
    {
        public BattleDiagnosticRuntimeObjectFilter(
            BattleDiagnosticRuntimeObjectKind kind = BattleDiagnosticRuntimeObjectKind.Unknown,
            BattleDiagnosticRuntimeObjectState state = BattleDiagnosticRuntimeObjectState.Unknown,
            BattleDiagnosticDataCompleteness completeness = BattleDiagnosticDataCompleteness.Unknown)
        {
            if (!Enum.IsDefined(typeof(BattleDiagnosticRuntimeObjectKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(BattleDiagnosticRuntimeObjectState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (!Enum.IsDefined(typeof(BattleDiagnosticDataCompleteness), completeness))
                throw new ArgumentOutOfRangeException(nameof(completeness));
            Kind = kind;
            State = state;
            Completeness = completeness;
        }

        public BattleDiagnosticRuntimeObjectKind Kind { get; }
        public BattleDiagnosticRuntimeObjectState State { get; }
        public BattleDiagnosticDataCompleteness Completeness { get; }

        public bool Matches(in BattleDiagnosticRuntimeObject item)
        {
            return (Kind == BattleDiagnosticRuntimeObjectKind.Unknown || item.Kind == Kind) &&
                   (State == BattleDiagnosticRuntimeObjectState.Unknown || item.State == State) &&
                   (Completeness == BattleDiagnosticDataCompleteness.Unknown ||
                    item.Completeness == Completeness);
        }

        public bool Equals(BattleDiagnosticRuntimeObjectFilter other)
        {
            return Kind == other.Kind && State == other.State && Completeness == other.Completeness;
        }

        public override bool Equals(object obj) =>
            obj is BattleDiagnosticRuntimeObjectFilter other && Equals(other);

        public override int GetHashCode() =>
            (((int)Kind * 397) ^ (int)State) * 397 ^ (int)Completeness;
    }

    public readonly struct BattleDiagnosticRuntimeObjectQuery
    {
        public BattleDiagnosticRuntimeObjectQuery(
            long requestId,
            BattleDiagnosticRuntimeObjectFilter filter,
            BattleDiagnosticPageRequest page)
        {
            if (requestId <= 0L) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (page.Limit <= 0) throw new ArgumentException(
                "A valid page request is required.",
                nameof(page));
            RequestId = requestId;
            Filter = filter;
            Page = page;
        }

        public long RequestId { get; }
        public BattleDiagnosticRuntimeObjectFilter Filter { get; }
        public BattleDiagnosticPageRequest Page { get; }
    }

    public interface IBattleDiagnosticRuntimeObjectCatalogReadStore :
        IBattleDiagnosticRuntimeObjectReadStore
    {
        BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObjects(
            BattleDiagnosticRuntimeObjectQuery query);

        BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObjectCatalogSummary>
            QueryRuntimeObjectSummary(long requestId);
    }

    public interface IBattleDiagnosticRuntimeObjectSession
    {
        long RuntimeObjectStoreRevision { get; }

        BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObject(
            long requestId,
            in BattleDiagnosticRuntimeObjectReference reference,
            int frame);
    }

    public interface IBattleDiagnosticRuntimeObjectCatalogSession :
        IBattleDiagnosticRuntimeObjectSession
    {
        BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObjects(
            BattleDiagnosticRuntimeObjectQuery query);

        BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObjectCatalogSummary>
            QueryRuntimeObjectSummary(long requestId);
    }

    public interface IBattleDiagnosticReadOnlySession
    {
        BattleDiagnosticSessionInfo SessionInfo { get; }
        long EventStoreRevision { get; }
        long StateStoreRevision { get; }
        long TraceStoreRevision { get; }
        long ActorAttributeStoreRevision { get; }
        long ActorBuffStoreRevision { get; }
        long ActorTagStoreRevision { get; }
        long ActorEffectStoreRevision { get; }

        /// <summary>事件 Store revision 的兼容别名。</summary>
        long StoreRevision { get; }

        BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary> QueryWorld(long requestId, int frame);

        BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> QueryActors(long requestId, int frame);

        BattleDiagnosticQueryResult<BattleDiagnosticEvent> QueryEvents(BattleDiagnosticEventQuery query);

        BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary> QueryTrace(
            long requestId,
            long rootContextId);

        BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(
            long requestId,
            int frame,
            long actorId);

        BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(
            long requestId,
            int frame,
            long actorId);

        BattleDiagnosticQueryResult<BattleDiagnosticActorBuff> QueryActorBuffs(
            long requestId,
            int frame,
            long actorId);

        BattleDiagnosticQueryResult<BattleDiagnosticActorTag> QueryActorTags(
            long requestId,
            int frame,
            long actorId);

        BattleDiagnosticQueryResult<BattleDiagnosticActorEffect> QueryActorEffects(
            long requestId,
            int frame,
            long actorId);
    }
}
