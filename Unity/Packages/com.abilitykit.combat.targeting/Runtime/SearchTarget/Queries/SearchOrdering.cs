using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 一个有序排序项。排序项按查询中的声明顺序参与字典序比较。
    /// </summary>
    public readonly struct SearchOrder
    {
        public readonly ITargetScorer Scorer;
        public readonly SearchSortDirection Direction;

        public SearchOrder(
            ITargetScorer scorer,
            SearchSortDirection direction = SearchSortDirection.ScoreDescending)
        {
            Scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
            if (direction != SearchSortDirection.ScoreDescending &&
                direction != SearchSortDirection.ScoreAscending)
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }
            Direction = direction;
        }
    }

    internal sealed class SearchScoreBuffer
    {
        private float[] _values = Array.Empty<float>();
        private int _count;

        internal int Capacity => _values.Length;

        public int Add(IReadOnlyList<SearchOrder> orders, in SearchQuery query, SearchContext context, EntityId candidate)
        {
            var offset = _count;
            return WriteAt(offset, orders, in query, context, candidate);
        }

        public int WriteAt(
            int offset,
            IReadOnlyList<SearchOrder> orders,
            in SearchQuery query,
            SearchContext context,
            EntityId candidate)
        {
            var count = orders == null ? 0 : orders.Count;
            EnsureCapacity(offset + count);
            for (int i = 0; i < count; i++)
            {
                var scorer = orders[i].Scorer;
                var score = scorer == null ? 0f : scorer.Score(in query, context, candidate);
                if (float.IsNaN(score)) return -1;
                _values[offset + i] = score;
            }

            var required = offset + count;
            if (required > _count) _count = required;
            return offset;
        }

        public void Copy(int sourceOffset, int destinationOffset, int count)
        {
            if (count <= 0 || sourceOffset == destinationOffset) return;
            Array.Copy(_values, sourceOffset, _values, destinationOffset, count);
            var required = destinationOffset + count;
            if (required > _count) _count = required;
        }

        public float Get(int offset, int index, float fallback)
        {
            return offset < 0 || index < 0 || offset + index >= _count
                ? fallback
                : _values[offset + index];
        }

        public void Reset(int maxRetainedCapacity)
        {
            _count = 0;
            if (_values.Length > maxRetainedCapacity)
            {
                _values = Array.Empty<float>();
            }
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _values.Length) return;
            var capacity = _values.Length == 0 ? 16 : _values.Length * 2;
            while (capacity < required) capacity *= 2;
            Array.Resize(ref _values, capacity);
        }
    }

    internal static class SearchOrdering
    {
        [ThreadStatic]
        private static SearchHitComparer _threadComparer;

        public static void Sort(List<SearchHit> hits, in SearchQuery query)
        {
            var comparer = _threadComparer ?? (_threadComparer = new SearchHitComparer());
            comparer.SetQuery(in query);
            try
            {
                hits.Sort(comparer);
            }
            finally
            {
                comparer.Clear();
            }
        }

        public static int Compare(in SearchQuery query, in SearchHit left, in SearchHit right)
        {
            var orders = query.Orders;
            for (int i = 0; i < orders.Count; i++)
            {
                var leftScore = left.GetScore(i);
                var rightScore = right.GetScore(i);
                var scoreOrder = leftScore.CompareTo(rightScore);
                if (scoreOrder != 0)
                {
                    return orders[i].Direction == SearchSortDirection.ScoreAscending
                        ? scoreOrder
                        : -scoreOrder;
                }
            }
            return left.Key.CompareTo(right.Key);
        }

        public static bool IsBetter(in SearchQuery query, in SearchHit left, in SearchHit right)
        {
            return Compare(in query, in left, in right) < 0;
        }

        private sealed class SearchHitComparer : IComparer<SearchHit>
        {
            private SearchQuery _query;

            public void SetQuery(in SearchQuery query)
            {
                _query = query;
            }

            public void Clear()
            {
                _query = default;
            }

            public int Compare(SearchHit x, SearchHit y)
            {
                return SearchOrdering.Compare(in _query, in x, in y);
            }
        }
    }
}
