using System.Collections.Generic;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 目标查找模块的统一对象池入口，用于高频目标查询场景。
    /// </summary>
    public static class TargetingPool
    {
        private const int HitListInitialCapacity = 128;
        private const int EntityIdListInitialCapacity = 64;
        private const int RuleListInitialCapacity = 4;
        private const int OrderListInitialCapacity = 4;
        private const int MaxRetainedHitListCapacity = 4096;
        private const int MaxRetainedEntityIdListCapacity = 4096;
        private const int MaxRetainedRuleListCapacity = 64;
        private const int MaxRetainedOrderListCapacity = 32;
        private const int MaxRetainedHitBufferCapacity = 4096;
        private const int MaxRetainedScoreBufferCapacity = 16384;
        private const int MaxRetainedEntityKeySetCount = 4096;

        private static readonly ObjectPool<List<SearchHit>> HitListPool = Pools.GetPool(
            createFunc: () => new List<SearchHit>(HitListInitialCapacity),
            onRelease: l => ResetList(l, MaxRetainedHitListCapacity, HitListInitialCapacity),
            defaultCapacity: 32,
            maxSize: 1024,
            collectionCheck: false);

        private static readonly ObjectPool<List<EntityId>> EntityIdListPool = Pools.GetPool(
            createFunc: CreateEntityIdList,
            onRelease: ResetEntityIdList,
            defaultCapacity: 32,
            maxSize: 1024,
            collectionCheck: false);

        private static readonly ObjectPool<List<ITargetRule>> RuleListPool = Pools.GetPool(
            createFunc: () => new List<ITargetRule>(RuleListInitialCapacity),
            onRelease: l => ResetList(l, MaxRetainedRuleListCapacity, RuleListInitialCapacity),
            defaultCapacity: 32,
            maxSize: 1024,
            collectionCheck: false);

        private static readonly ObjectPool<List<SearchOrder>> OrderListPool = Pools.GetPool(
            createFunc: () => new List<SearchOrder>(OrderListInitialCapacity),
            onRelease: l => ResetList(l, MaxRetainedOrderListCapacity, OrderListInitialCapacity),
            defaultCapacity: 32,
            maxSize: 1024,
            collectionCheck: false);

        private static readonly ObjectPool<HashSet<ulong>> EntityKeySetPool = Pools.GetPool(
            createFunc: () => new HashSet<ulong>(),
            onRelease: ResetEntityKeySet,
            defaultCapacity: 16,
            maxSize: 512,
            collectionCheck: false);

        private static readonly ObjectPool<SearchContext> ContextPool = Pools.GetPool(
            createFunc: () => new SearchContext(),
            onGet: c => c.ResetForRent(),
            onRelease: c => c.ResetForRelease(),
            defaultCapacity: 16,
            maxSize: 512,
            collectionCheck: false);

        private static readonly ObjectPool<SearchResult> ResultPool = Pools.GetPool(
            createFunc: () => new SearchResult(),
            onGet: r => r.ResetForRent(),
            onRelease: r => r.ResetForRelease(),
            defaultCapacity: 32,
            maxSize: 1024,
            collectionCheck: false);

        private static readonly ObjectPool<SearchHitBuffer> HitBufferPool = Pools.GetPool(
            createFunc: () => new SearchHitBuffer(),
            onRelease: b => b.Reset(MaxRetainedHitBufferCapacity),
            defaultCapacity: 16,
            maxSize: 512,
            collectionCheck: false);

        private static readonly ObjectPool<SearchScoreBuffer> ScoreBufferPool = Pools.GetPool(
            createFunc: () => new SearchScoreBuffer(),
            onRelease: b => b.Reset(MaxRetainedScoreBufferCapacity),
            defaultCapacity: 16,
            maxSize: 512,
            collectionCheck: false);

        public static SearchContext RentContext()
        {
            return ContextPool.Get();
        }

        public static void Release(SearchContext context)
        {
            if (context == null || !context.TryBeginPoolRelease()) return;
            ContextPool.Release(context);
        }

        /// <summary>
        /// 租用一个空结果；使用完毕后由调用方释放或归还。
        /// </summary>
        public static SearchResult RentResult()
        {
            return ResultPool.Get();
        }

        /// <summary>
        /// 归还当前结果并断开已取得的视图；空引用被忽略，不得再次使用已归还的对象引用。
        /// </summary>
        public static void Release(SearchResult result)
        {
            if (result == null || !result.TryBeginPoolRelease()) return;
            ResultPool.Release(result);
        }

        internal static List<SearchHit> RentHitList()
        {
            return HitListPool.Get();
        }

        internal static void ReleaseHitList(List<SearchHit> list)
        {
            if (list == null) return;
            HitListPool.Release(list);
        }

        internal static List<EntityId> RentEntityIdList()
        {
            return EntityIdListPool.Get();
        }

        internal static void ReleaseEntityIdList(List<EntityId> list)
        {
            if (list == null) return;
            EntityIdListPool.Release(list);
        }

        internal static List<ITargetRule> RentRuleList()
        {
            return RuleListPool.Get();
        }

        internal static void ReleaseRuleList(List<ITargetRule> list)
        {
            if (list == null) return;
            RuleListPool.Release(list);
        }

        internal static List<SearchOrder> RentOrderList()
        {
            return OrderListPool.Get();
        }

        internal static void ReleaseOrderList(List<SearchOrder> list)
        {
            if (list == null) return;
            OrderListPool.Release(list);
        }

        internal static HashSet<ulong> RentEntityKeySet()
        {
            return EntityKeySetPool.Get();
        }

        internal static void ReleaseEntityKeySet(HashSet<ulong> set)
        {
            if (set == null) return;
            EntityKeySetPool.Release(set);
        }

        internal static SearchScoreBuffer RentScoreBuffer()
        {
            return ScoreBufferPool.Get();
        }

        internal static void ReleaseScoreBuffer(SearchScoreBuffer buffer)
        {
            if (buffer == null) return;
            ScoreBufferPool.Release(buffer);
        }

        internal static SearchHitBuffer RentHitBuffer(int capacity)
        {
            var buffer = HitBufferPool.Get();
            buffer.EnsureCapacity(capacity);
            return buffer;
        }

        internal static void ReleaseHitBuffer(SearchHitBuffer buffer)
        {
            if (buffer == null) return;
            HitBufferPool.Release(buffer);
        }

        internal static List<EntityId> CreateEntityIdList() => new List<EntityId>(EntityIdListInitialCapacity);

        internal static void ResetEntityIdList(List<EntityId> list) =>
            ResetList(list, MaxRetainedEntityIdListCapacity, EntityIdListInitialCapacity);

        private static void ResetList<T>(List<T> list, int maxRetainedCapacity, int initialCapacity)
        {
            list.Clear();
            if (list.Capacity > maxRetainedCapacity)
            {
                list.Capacity = initialCapacity;
            }
        }

        private static void ResetEntityKeySet(HashSet<ulong> set)
        {
            var shouldTrim = set.Count > MaxRetainedEntityKeySetCount;
            set.Clear();
            if (shouldTrim)
            {
                set.TrimExcess();
            }
        }
    }
}
