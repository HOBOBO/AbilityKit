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
