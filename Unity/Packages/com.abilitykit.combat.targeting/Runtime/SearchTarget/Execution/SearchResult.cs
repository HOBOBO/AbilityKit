using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 持有一次目标 ID 查询结果的池化租约，调用方负责释放。
    /// </summary>
    /// <remarks>
    /// 归还后不得再使用此对象引用；对象可能被下一次租约复用，旧引用不能区分新旧租约。
    /// 读取、枚举、清空和复制都不得与释放并发。
    /// </remarks>
    public sealed class SearchResult : IDisposable
    {
        internal readonly List<EntityId> MutableIds = TargetingPool.CreateEntityIdList();

        private int _poolLeaseState;
        private ResultIdView _idsView;

        internal SearchResult()
        {
        }

        /// <summary>
        /// 返回本次租约的只读实时视图，不是快照；调用 <see cref="Clear"/> 后视图也随之清空。
        /// </summary>
        /// <remarks>
        /// 归还时视图与底层列表断开并变为空；已创建的枚举器不得继续使用。
        /// 需要跨租约保存结果时，使用 <see cref="CopyTo"/>。
        /// </remarks>
        public IReadOnlyList<EntityId> Ids
        {
            get
            {
                ThrowIfReleased();
                return _idsView ?? (_idsView = new ResultIdView(MutableIds));
            }
        }

        /// <summary>
        /// 获取当前租约内的结果数。
        /// </summary>
        public int Count
        {
            get
            {
                ThrowIfReleased();
                return MutableIds.Count;
            }
        }

        /// <summary>
        /// 按从零开始的索引读取当前租约内的结果。
        /// </summary>
        public EntityId this[int index]
        {
            get
            {
                ThrowIfReleased();
                return MutableIds[index];
            }
        }

        /// <summary>
        /// 清空调用方的列表并按当前顺序复制结果；副本可独立于租约保存。
        /// </summary>
        public void CopyTo(List<EntityId> results)
        {
            ThrowIfReleased();
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            for (int i = 0; i < MutableIds.Count; i++)
            {
                results.Add(MutableIds[i]);
            }
        }

        /// <summary>
        /// 清空当前租约内的结果，不归还本对象。
        /// </summary>
        public void Clear()
        {
            ThrowIfReleased();
            MutableIds.Clear();
        }

        /// <summary>
        /// 归还当前结果租约；实例被重新租出之前，重复归还无效。
        /// </summary>
        public void Dispose()
        {
            TargetingPool.Release(this);
        }

        internal void ResetForRent()
        {
            Volatile.Write(ref _poolLeaseState, 1);
            MutableIds.Clear();
        }

        internal bool TryBeginPoolRelease()
        {
            return Interlocked.Exchange(ref _poolLeaseState, 0) == 1;
        }

        internal void ResetForRelease()
        {
            _idsView?.Detach();
            _idsView = null;
            TargetingPool.ResetEntityIdList(MutableIds);
        }

        private void ThrowIfReleased()
        {
            if (Volatile.Read(ref _poolLeaseState) != 1)
            {
                throw new ObjectDisposedException(nameof(SearchResult));
            }
        }

        private sealed class ResultIdView : IReadOnlyList<EntityId>
        {
            private IReadOnlyList<EntityId> _ids;

            internal ResultIdView(List<EntityId> ids) => _ids = ids;

            public int Count => _ids.Count;

            public EntityId this[int index] => _ids[index];

            public IEnumerator<EntityId> GetEnumerator() => _ids.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            internal void Detach() => _ids = Array.Empty<EntityId>();
        }
    }
}
