using System.Buffers;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Selectors
{
    public sealed class StreamingTopKByScoreSelector : ITargetSelector, IStreamingHitSelector
    {
        private SearchHit[] _buffer;
        private int _count;
        private int _k;

        public bool RequiresPosition => false;

        public bool CanStream(in SearchQuery query)
        {
            return query.HasMaxCount && query.MaxCount > 0;
        }

        public void Begin(in SearchQuery query, SearchContext context)
        {
            _count = 0;
            _k = query.MaxCount;
            if (_k <= 0)
            {
                _buffer = null;
                return;
            }

            _buffer = ArrayPool<SearchHit>.Shared.Rent(_k);
        }

        public void Offer(in SearchHit hit)
        {
            if (_buffer == null) return;

            if (_count == 0)
            {
                _buffer[0] = hit;
                _count = 1;
                return;
            }

            if (_count < _k)
            {
                InsertSorted(_buffer, ref _count, hit);
                return;
            }

            if (BetterThan(hit, _buffer[_count - 1]))
            {
                InsertAndTrim(_buffer, _count, hit);
            }
        }

        public void End(in SearchQuery query, SearchContext context, List<EcsEntityId> results)
        {
            if (_buffer == null) return;

            for (int i = 0; i < _count; i++)
            {
                results.Add(_buffer[i].Id);
            }

            ArrayPool<SearchHit>.Shared.Return(_buffer, clearArray: false);
            _buffer = null;
            _count = 0;
            _k = 0;
        }

        // Non-stream fallback required by ITargetSelector.
        public void Select(in SearchQuery query, SearchContext context, List<SearchHit> hits, List<EcsEntityId> results)
        {
            if (!query.HasMaxCount)
            {
                hits.Sort(DefaultHitComparer.Instance);
                for (int i = 0; i < hits.Count; i++)
                {
                    results.Add(hits[i].Id);
                }
                return;
            }

            Begin(in query, context);
            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                Offer(in h);
            }
            End(in query, context, results);
        }

        private static void InsertSorted(SearchHit[] buffer, ref int count, in SearchHit h)
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

        private static void InsertAndTrim(SearchHit[] buffer, int count, in SearchHit h)
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
