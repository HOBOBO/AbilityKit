using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 池化目标查找结果。使用完成后调用释放方法，或通过目标查找对象池归还。
    /// </summary>
    public sealed class SearchResult : IDisposable
    {
        internal readonly List<EntityId> MutableIds = TargetingPool.RentEntityIdList();

        private int _poolLeaseState;

        internal SearchResult()
        {
        }

        public IReadOnlyList<EntityId> Ids
        {
            get
            {
                ThrowIfReleased();
                return MutableIds;
            }
        }

        public int Count
        {
            get
            {
                ThrowIfReleased();
                return MutableIds.Count;
            }
        }

        public EntityId this[int index]
        {
            get
            {
                ThrowIfReleased();
                return MutableIds[index];
            }
        }

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

        public void Clear()
        {
            ThrowIfReleased();
            MutableIds.Clear();
        }

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
            MutableIds.Clear();
        }

        private void ThrowIfReleased()
        {
            if (Volatile.Read(ref _poolLeaseState) != 1)
            {
                throw new ObjectDisposedException(nameof(SearchResult));
            }
        }
    }
}
