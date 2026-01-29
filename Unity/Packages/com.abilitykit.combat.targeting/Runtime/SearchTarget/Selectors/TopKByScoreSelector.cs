using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Selectors
{
    public sealed class TopKByScoreSelector : ITargetSelector
    {
        public bool RequiresPosition => false;

        public void Select(in SearchQuery query, SearchContext context, List<SearchHit> hits, List<EcsEntityId> results)
        {
            hits.Sort(DefaultHitComparer.Instance);

            if (query.HasMaxCount)
            {
                var count = query.MaxCount;
                if (count > hits.Count) count = hits.Count;
                for (int i = 0; i < count; i++)
                {
                    results.Add(hits[i].Id);
                }
                return;
            }

            for (int i = 0; i < hits.Count; i++)
            {
                results.Add(hits[i].Id);
            }
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
