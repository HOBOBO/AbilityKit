using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public sealed class TargetSearchEngine
    {
        private readonly List<SearchHit> _hits = new List<SearchHit>(128);
        private readonly List<EcsEntityId> _selectedIds = new List<EcsEntityId>(64);

        public void SearchIds(in SearchQuery query, SearchContext context, List<EcsEntityId> results)
        {
            results.Clear();
            _hits.Clear();
            _selectedIds.Clear();

            if (query.Provider == null) return;

            var requiresPosition = query.Provider.RequiresPosition ||
                                   (query.Scorer != null && query.Scorer.RequiresPosition) ||
                                   (query.Selector != null && query.Selector.RequiresPosition) ||
                                   RequiresPosition(query.Rules);

            if (requiresPosition && !context.TryGetService<IPositionProvider>(out _))
            {
                return;
            }

            context.TryGetService<IEntityKeyProvider>(out var keyProvider);

            context.TryGetService<ISearchStats>(out var stats);
            stats?.Reset();

            if (query.Selector is IStreamingHitSelector streaming && streaming.CanStream(in query))
            {
                streaming.Begin(in query, context);
                var consumer = new StreamingCandidateConsumer(in query, context, streaming, keyProvider, stats);
                query.Provider.ForEachCandidate(in query, context, ref consumer);
                streaming.End(in query, context, results);
                stats?.OnResult(results.Count);
                return;
            }
            else
            {
                var consumer = new CandidateConsumer(in query, context, _hits, keyProvider, stats);
                query.Provider.ForEachCandidate(in query, context, ref consumer);

                if (_hits.Count == 0) return;

                if (query.Selector != null)
                {
                    query.Selector.Select(in query, context, _hits, results);
                    stats?.OnResult(results.Count);
                    return;
                }

                _hits.Sort(DefaultHitComparer.Instance);
                WriteAll(in query, _hits, results);
                stats?.OnResult(results.Count);
            }
        }

        public void Search<T>(in SearchQuery query, SearchContext context, List<T> results, ITargetMapper<T> mapper)
        {
            results.Clear();
            if (mapper == null) return;

            SearchIds(in query, context, _selectedIds);
            if (_selectedIds.Count == 0) return;

            for (int i = 0; i < _selectedIds.Count; i++)
            {
                if (mapper.TryMap(context, _selectedIds[i], out var v))
                {
                    results.Add(v);
                }
            }
        }

        private readonly struct StreamingCandidateConsumer : ICandidateConsumer
        {
            private readonly SearchQuery _query;
            private readonly SearchContext _context;
            private readonly IStreamingHitSelector _selector;
            private readonly IEntityKeyProvider _keyProvider;
            private readonly ISearchStats _stats;

            public StreamingCandidateConsumer(in SearchQuery query, SearchContext context, IStreamingHitSelector selector, IEntityKeyProvider keyProvider, ISearchStats stats)
            {
                _query = query;
                _context = context;
                _selector = selector;
                _keyProvider = keyProvider;
                _stats = stats;
            }

            public void Consume(EcsEntityId id)
            {
                _stats?.OnCandidate();
                if (!id.IsValid) return;
                if (!PassRules(in _query, _context, id)) return;

                _stats?.OnHit();

                var score = _query.Scorer != null ? _query.Scorer.Score(in _query, _context, id) : 0f;
                var key = _keyProvider != null ? _keyProvider.GetKey(id) : (ulong)id.ActorId;
                var hit = new SearchHit(id, score, key);
                _selector.Offer(in hit);
            }
        }

        private static bool RequiresPosition(IReadOnlyList<ITargetRule> rules)
        {
            if (rules == null || rules.Count == 0) return false;
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r != null && r.RequiresPosition) return true;
            }
            return false;
        }

        private static bool PassRules(in SearchQuery query, SearchContext context, EcsEntityId id)
        {
            var rules = query.Rules;
            if (rules == null || rules.Count == 0) return true;

            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r == null) continue;
                if (!r.Test(in query, context, id)) return false;
            }

            return true;
        }

        private readonly struct CandidateConsumer : ICandidateConsumer
        {
            private readonly SearchQuery _query;
            private readonly SearchContext _context;
            private readonly List<SearchHit> _hits;
            private readonly IEntityKeyProvider _keyProvider;
            private readonly ISearchStats _stats;

            public CandidateConsumer(in SearchQuery query, SearchContext context, List<SearchHit> hits, IEntityKeyProvider keyProvider, ISearchStats stats)
            {
                _query = query;
                _context = context;
                _hits = hits;
                _keyProvider = keyProvider;
                _stats = stats;
            }

            public void Consume(EcsEntityId id)
            {
                _stats?.OnCandidate();
                if (!id.IsValid) return;
                if (!PassRules(in _query, _context, id)) return;

                _stats?.OnHit();

                var score = _query.Scorer != null ? _query.Scorer.Score(in _query, _context, id) : 0f;
                var key = _keyProvider != null ? _keyProvider.GetKey(id) : (ulong)id.ActorId;
                _hits.Add(new SearchHit(id, score, key));
            }
        }

        private static void WriteAll(in SearchQuery query, List<SearchHit> hits, List<EcsEntityId> results)
        {
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
