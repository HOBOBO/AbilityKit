using System.Buffers;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Selectors
{
    public sealed class OnlineTopKByScoreSelector : ITargetSelector
    {
        public bool RequiresPosition => false;

        public void Select(in SearchQuery query, SearchContext context, List<SearchHit> hits, List<EcsEntityId> results)
        {
            if (hits == null || hits.Count == 0) return;

            if (!query.HasMaxCount)
            {
                hits.Sort(DefaultHitComparer.Instance);
                for (int i = 0; i < hits.Count; i++)
                {
                    results.Add(hits[i].Id);
                }
                return;
            }

            var k = query.MaxCount;
            if (k <= 0) return;
            if (k > hits.Count) k = hits.Count;

            var pool = ArrayPool<SearchHit>.Shared;
            var buffer = pool.Rent(k);
            var count = 0;

            try
            {
                for (int i = 0; i < hits.Count; i++)
                {
                    var h = hits[i];

                    if (count == 0)
                    {
                        buffer[0] = h;
                        count = 1;
                        continue;
                    }

                    if (count < k)
                    {
                        InsertSorted(buffer, ref count, h);
                        continue;
                    }

                    // Replace the current worst if this hit is better.
                    if (BetterThan(h, buffer[count - 1]))
                    {
                        InsertAndTrim(buffer, count, h);
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    results.Add(buffer[i].Id);
                }
            }
            finally
            {
                pool.Return(buffer, clearArray: false);
            }
        }

        private static void InsertSorted(SearchHit[] buffer, ref int count, SearchHit h)
        {
            var idx = 0;
            while (idx < count && BetterThan(buffer[idx], h)) idx++;

            for (int j = count; j > idx; j--)
            {
                buffer[j] = buffer[j - 1];
            }

            buffer[idx] = h;
            count++;
        }

        private static void InsertAndTrim(SearchHit[] buffer, int count, SearchHit h)
        {
            var idx = 0;
            while (idx < count && BetterThan(buffer[idx], h)) idx++;

            if (idx >= count) return;

            for (int j = count - 1; j > idx; j--)
            {
                buffer[j] = buffer[j - 1];
            }

            buffer[idx] = h;
        }

        private static bool BetterThan(in SearchHit a, in SearchHit b)
        {
            if (a.Score > b.Score) return true;
            if (a.Score < b.Score) return false;
            return a.Key < b.Key;
        }

        private sealed class DefaultHitComparer : IComparer<SearchHit>
        {
            public static readonly DefaultHitComparer Instance = new DefaultHitComparer();

            public int Compare(SearchHit x, SearchHit y)
            {
                var s = y.Score.CompareTo(x.Score);
                if (s != 0) return s;
                return x.Key.CompareTo(y.Key);
            }
        }
    }
}
