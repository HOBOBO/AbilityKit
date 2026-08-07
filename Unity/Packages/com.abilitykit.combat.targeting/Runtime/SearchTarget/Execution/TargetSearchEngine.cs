using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 目标搜索引擎
    /// </summary>
    public sealed class TargetSearchEngine
    {
        public SearchResult SearchIds(in SearchQuery query, SearchContext context)
        {
            var result = TargetingPool.RentResult();
            try
            {
                SearchIds(in query, context, result.MutableIds);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        public void SearchIds(in SearchQuery query, SearchContext context, List<EntityId> results)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            var keyProvider = context.EntityKeyProvider;
            var stats = context.SearchStats;
            stats?.Reset();

            if (query.Provider == null)
            {
                stats?.OnResult(0);
                return;
            }

            if (query.HasMaxCount && query.Selector is IStreamingTopKByScoreSelector)
            {
                SearchStreamingTopK(in query, context, results, keyProvider, stats);
                return;
            }

            var hits = TargetingPool.RentHitList();
            var scoreBuffer = TargetingPool.RentScoreBuffer();
            var seenKeys = query.DuplicatePolicy == SearchDuplicatePolicy.DistinctByEntityKey
                ? TargetingPool.RentEntityKeySet()
                : null;
            try
            {
                var consumer2 = new CandidateConsumer(
                    in query,
                    context,
                    hits,
                    scoreBuffer,
                    keyProvider,
                    stats,
                    seenKeys);
                query.Provider.ForEachCandidate(in query, context, ref consumer2);

                if (hits.Count == 0) return;

                if (query.Selector != null)
                {
                    query.Selector.Select(
                        in query,
                        context,
                        new SearchHitView(hits),
                        new SearchResultWriter(results, query.MaxCount));
                    stats?.OnResult(results.Count);
                    return;
                }

                SearchOrdering.Sort(hits, in query);
                WriteAll(in query, hits, results);
                stats?.OnResult(results.Count);
            }
            finally
            {
                TargetingPool.ReleaseEntityKeySet(seenKeys);
                TargetingPool.ReleaseHitList(hits);
                TargetingPool.ReleaseScoreBuffer(scoreBuffer);
            }
        }

        public void Search<T>(in SearchQuery query, SearchContext context, List<T> results, ITargetMapper<T> mapper)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            results.Clear();

            var selectedIds = TargetingPool.RentEntityIdList();
            try
            {
                SearchIds(in query, context, selectedIds);
                if (selectedIds.Count == 0) return;

                for (int i = 0; i < selectedIds.Count; i++)
                {
                    if (mapper.TryMap(context, selectedIds[i], out var v))
                    {
                        results.Add(v);
                    }
                }
            }
            finally
            {
                TargetingPool.ReleaseEntityIdList(selectedIds);
            }
        }

        private static void SearchStreamingTopK(
            in SearchQuery query,
            SearchContext context,
            List<EntityId> results,
            IEntityKeyProvider keyProvider,
            ISearchStats stats)
        {
            var hitBuffer = TargetingPool.RentHitBuffer(query.MaxCount);
            var scoreBuffer = TargetingPool.RentScoreBuffer();
            var seenKeys = query.DuplicatePolicy == SearchDuplicatePolicy.DistinctByEntityKey
                ? TargetingPool.RentEntityKeySet()
                : null;
            try
            {
                var consumer = new StreamingTopKConsumer(
                    in query,
                    context,
                    hitBuffer.Items,
                    query.MaxCount,
                    scoreBuffer,
                    keyProvider,
                    stats,
                    seenKeys);
                query.Provider.ForEachCandidate(in query, context, ref consumer);
                consumer.WriteResults(results);
                stats?.OnResult(results.Count);
            }
            finally
            {
                TargetingPool.ReleaseEntityKeySet(seenKeys);
                TargetingPool.ReleaseHitBuffer(hitBuffer);
                TargetingPool.ReleaseScoreBuffer(scoreBuffer);
            }
        }

        private static bool PassRules(in SearchQuery query, SearchContext context, EntityId id)
        {
            var rules = query.Rules;
            if (rules == null || rules.Count == 0) return true;

            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r == null) continue;
                if (!r.IsMatch(in query, context, id)) return false;
            }
            return true;
        }

        private readonly struct CandidateConsumer : ICandidateConsumer
        {
            private readonly SearchQuery _query;
            private readonly SearchContext _context;
            private readonly List<SearchHit> _hits;
            private readonly SearchScoreBuffer _scoreBuffer;
            private readonly IEntityKeyProvider _keyProvider;
            private readonly ISearchStats _stats;
            private readonly HashSet<ulong> _seenKeys;

            public CandidateConsumer(
                in SearchQuery query,
                SearchContext context,
                List<SearchHit> hits,
                SearchScoreBuffer scoreBuffer,
                IEntityKeyProvider keyProvider,
                ISearchStats stats,
                HashSet<ulong> seenKeys)
            {
                _query = query;
                _context = context;
                _hits = hits;
                _scoreBuffer = scoreBuffer;
                _keyProvider = keyProvider;
                _stats = stats;
                _seenKeys = seenKeys;
            }

            public void Consume(EntityId id)
            {
                _stats?.OnCandidate();
                if (!id.IsValid) return;

                var key = _keyProvider != null ? _keyProvider.GetKey(id) : id.Value;
                if (_seenKeys != null && _seenKeys.Contains(key)) return;
                if (!PassRules(in _query, _context, id)) return;

                var scoreOffset = _scoreBuffer.Add(_query.Orders, in _query, _context, id);
                if (scoreOffset < 0) return;
                if (_seenKeys != null && !_seenKeys.Add(key)) return;

                _stats?.OnHit();
                _hits.Add(new SearchHit(id, key, _scoreBuffer, scoreOffset));
            }
        }

        private struct StreamingTopKConsumer : ICandidateConsumer
        {
            private readonly SearchQuery _query;
            private readonly SearchContext _context;
            private readonly SearchHit[] _hits;
            private readonly int _capacity;
            private readonly SearchScoreBuffer _scoreBuffer;
            private readonly IEntityKeyProvider _keyProvider;
            private readonly ISearchStats _stats;
            private readonly HashSet<ulong> _seenKeys;
            private int _count;

            public StreamingTopKConsumer(
                in SearchQuery query,
                SearchContext context,
                SearchHit[] hits,
                int capacity,
                SearchScoreBuffer scoreBuffer,
                IEntityKeyProvider keyProvider,
                ISearchStats stats,
                HashSet<ulong> seenKeys)
            {
                _query = query;
                _context = context;
                _hits = hits;
                _capacity = capacity;
                _scoreBuffer = scoreBuffer;
                _keyProvider = keyProvider;
                _stats = stats;
                _seenKeys = seenKeys;
                _count = 0;
            }

            public void Consume(EntityId id)
            {
                _stats?.OnCandidate();
                if (!id.IsValid) return;

                var key = _keyProvider != null ? _keyProvider.GetKey(id) : id.Value;
                if (_seenKeys != null && _seenKeys.Contains(key)) return;
                if (!PassRules(in _query, _context, id)) return;

                var scoreCount = _query.Orders.Count;
                var temporaryOffset = _capacity * scoreCount;
                if (_scoreBuffer.WriteAt(
                        temporaryOffset,
                        _query.Orders,
                        in _query,
                        _context,
                        id) < 0)
                {
                    return;
                }
                if (_seenKeys != null && !_seenKeys.Add(key)) return;

                _stats?.OnHit();
                Offer(id, key, temporaryOffset, scoreCount);
            }

            public void WriteResults(List<EntityId> results)
            {
                for (int i = 0; i < _count; i++)
                {
                    results.Add(_hits[i].Id);
                }
            }

            private void Offer(EntityId id, ulong key, int temporaryOffset, int scoreCount)
            {
                var hit = new SearchHit(id, key, _scoreBuffer, temporaryOffset);
                var insertIndex = 0;
                while (insertIndex < _count &&
                       SearchOrdering.IsBetter(in _query, in _hits[insertIndex], in hit))
                {
                    insertIndex++;
                }

                if (insertIndex >= _capacity) return;

                var lastIndex = _count < _capacity ? _count : _capacity - 1;
                for (int i = lastIndex; i > insertIndex; i--)
                {
                    var source = _hits[i - 1];
                    var destinationOffset = i * scoreCount;
                    _scoreBuffer.Copy((i - 1) * scoreCount, destinationOffset, scoreCount);
                    _hits[i] = new SearchHit(
                        source.Id,
                        source.Key,
                        _scoreBuffer,
                        destinationOffset);
                }

                var insertOffset = insertIndex * scoreCount;
                _scoreBuffer.Copy(temporaryOffset, insertOffset, scoreCount);
                _hits[insertIndex] = new SearchHit(id, key, _scoreBuffer, insertOffset);
                if (_count < _capacity) _count++;
            }
        }

        private static void WriteAll(in SearchQuery query, List<SearchHit> hits, List<EntityId> results)
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

    }
}
