using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 同步执行候选遍历、评估与选择的目标搜索引擎。
    /// </summary>
    public sealed class TargetSearchEngine
    {
        /// <summary>
        /// 查询目标 ID，并将结果租约交给调用方释放；失败时由引擎自行归还结果。
        /// </summary>
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

        /// <summary>
        /// 清空调用方的列表，并写入当前查询选中的目标 ID。
        /// </summary>
        /// <remarks>
        /// 查询失败时不回滚已写入的结果，调用方应丢弃部分输出。
        /// 不得在并发查询之间共享可变上下文或输出列表。
        /// </remarks>
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
                var evaluator = new CandidateEvaluator(context, scoreBuffer, keyProvider, stats, seenKeys);
                var consumer = new FullSearchConsumer(in query, hits, in evaluator);
                query.Provider.ForEachCandidate(in query, context, ref consumer);

                if (hits.Count > 0)
                {
                    if (query.Selector != null)
                    {
                        query.Selector.Select(
                            in query,
                            context,
                            new SearchHitView(hits),
                            new SearchResultWriter(results, query.MaxCount));
                    }
                    else
                    {
                        SearchOrdering.Sort(hits, in query);
                        WriteAll(in query, hits, results);
                    }
                }

                stats?.OnResult(results.Count);
            }
            finally
            {
                TargetingPool.ReleaseEntityKeySet(seenKeys);
                TargetingPool.ReleaseHitList(hits);
                TargetingPool.ReleaseScoreBuffer(scoreBuffer);
            }
        }

        /// <summary>
        /// 清空输出列表，按目标 ID 的选择顺序映射并写入成功项，不替失败项另外补选。
        /// </summary>
        /// <remarks>
        /// 搜索统计在映射前完成；映射异常向调用方传播，不回滚已写入的成功项。
        /// </remarks>
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
                var evaluator = new CandidateEvaluator(context, scoreBuffer, keyProvider, stats, seenKeys);
                var consumer = new StreamingTopKConsumer(in query, hitBuffer.Items, scoreBuffer, in evaluator);
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

        private static bool PassRules(in SearchQuery query, SearchContext context, EntityId id,
            ISearchStats stats)
        {
            var rules = query.Rules;
            if (rules == null || rules.Count == 0) return true;

            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r == null) continue;
                if (!r.IsMatch(in query, context, id))
                {
                    if (stats is ISearchDetailedStats)
                        ReportDecision(stats, id, SearchCandidateDecision.RuleRejected, i, r.GetType().Name);
                    return false;
                }
            }
            return true;
        }

        private static void ReportDecision(ISearchStats stats, EntityId id,
            SearchCandidateDecision decision, int ruleIndex = -1, string ruleName = null,
            float? primaryScore = null)
        {
            if (stats is ISearchDetailedStats detailed)
            {
                try { detailed.OnDecision(id, decision, ruleIndex, ruleName, primaryScore); }
                catch
                {
                    /*
                     * 详细诊断仅用于观察，回调异常不能中断搜索。
                     */
                }
            }
        }

        private readonly struct CandidateEvaluator
        {
            private readonly SearchContext _context;
            private readonly SearchScoreBuffer _scoreBuffer;
            private readonly IEntityKeyProvider _keyProvider;
            private readonly ISearchStats _stats;
            private readonly HashSet<ulong> _seenKeys;

            public CandidateEvaluator(
                SearchContext context,
                SearchScoreBuffer scoreBuffer,
                IEntityKeyProvider keyProvider,
                ISearchStats stats,
                HashSet<ulong> seenKeys)
            {
                _context = context;
                _scoreBuffer = scoreBuffer;
                _keyProvider = keyProvider;
                _stats = stats;
                _seenKeys = seenKeys;
            }

            public bool TryEvaluate(in SearchQuery query, EntityId id, out SearchHit hit, int? fixedScoreOffset = null)
            {
                hit = default;
                _stats?.OnCandidate();
                if (!id.IsValid)
                {
                    ReportDecision(_stats, id, SearchCandidateDecision.Invalid);
                    return false;
                }

                var key = _keyProvider != null ? _keyProvider.GetKey(id) : id.Value;
                if (_seenKeys != null && _seenKeys.Contains(key))
                {
                    ReportDecision(_stats, id, SearchCandidateDecision.Duplicate);
                    return false;
                }
                if (!PassRules(in query, _context, id, _stats)) return false;

                var scoreOffset = fixedScoreOffset.HasValue
                    ? _scoreBuffer.WriteAt(fixedScoreOffset.Value, query.Orders, in query, _context, id)
                    : _scoreBuffer.Add(query.Orders, in query, _context, id);
                if (scoreOffset < 0)
                {
                    ReportDecision(_stats, id, SearchCandidateDecision.ScoreUnavailable);
                    return false;
                }

                /*
                 * 去重记录的是通过规则和评分的候选，而不是最终入选的结果。
                 */
                if (_seenKeys != null && !_seenKeys.Add(key))
                {
                    ReportDecision(_stats, id, SearchCandidateDecision.Duplicate);
                    return false;
                }

                hit = new SearchHit(id, key, _scoreBuffer, scoreOffset);
                _stats?.OnHit();
                ReportDecision(_stats, id, SearchCandidateDecision.Eligible,
                    primaryScore: query.Orders.Count > 0 ? hit.GetScore(0) : (float?)null);
                return true;
            }
        }

        private readonly struct FullSearchConsumer : ICandidateConsumer
        {
            private readonly SearchQuery _query;
            private readonly List<SearchHit> _hits;
            private readonly CandidateEvaluator _evaluator;

            public FullSearchConsumer(in SearchQuery query, List<SearchHit> hits, in CandidateEvaluator evaluator)
            {
                _query = query;
                _hits = hits;
                _evaluator = evaluator;
            }

            public void Consume(EntityId id)
            {
                if (_evaluator.TryEvaluate(in _query, id, out var hit))
                {
                    _hits.Add(hit);
                }
            }
        }

        private struct StreamingTopKConsumer : ICandidateConsumer
        {
            private readonly SearchQuery _query;
            private readonly SearchHit[] _hits;
            private readonly SearchScoreBuffer _scoreBuffer;
            private readonly CandidateEvaluator _evaluator;
            private readonly int _scoreCount;
            private readonly int _temporaryOffset;
            private int _count;

            public StreamingTopKConsumer(
                in SearchQuery query,
                SearchHit[] hits,
                SearchScoreBuffer scoreBuffer,
                in CandidateEvaluator evaluator)
            {
                _query = query;
                _hits = hits;
                _scoreBuffer = scoreBuffer;
                _evaluator = evaluator;
                _scoreCount = query.Orders.Count;
                _temporaryOffset = query.MaxCount * _scoreCount;
                _count = 0;
            }

            public void Consume(EntityId id)
            {
                if (_evaluator.TryEvaluate(in _query, id, out var hit, _temporaryOffset))
                {
                    Offer(in hit);
                }
            }

            public void WriteResults(List<EntityId> results)
            {
                for (int i = 0; i < _count; i++)
                {
                    results.Add(_hits[i].Id);
                }
            }

            private void Offer(in SearchHit hit)
            {
                var insertIndex = 0;
                while (insertIndex < _count &&
                       SearchOrdering.IsBetter(in _query, in _hits[insertIndex], in hit))
                {
                    insertIndex++;
                }

                var capacity = _query.MaxCount;
                if (insertIndex >= capacity) return;

                var lastIndex = _count < capacity ? _count : capacity - 1;
                for (int i = lastIndex; i > insertIndex; i--)
                {
                    var source = _hits[i - 1];
                    var destinationOffset = i * _scoreCount;
                    _scoreBuffer.Copy((i - 1) * _scoreCount, destinationOffset, _scoreCount);
                    _hits[i] = new SearchHit(
                        source.Id,
                        source.Key,
                        _scoreBuffer,
                        destinationOffset);
                }

                var insertOffset = insertIndex * _scoreCount;
                _scoreBuffer.Copy(_temporaryOffset, insertOffset, _scoreCount);
                _hits[insertIndex] = new SearchHit(hit.Id, hit.Key, _scoreBuffer, insertOffset);
                if (_count < capacity) _count++;
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
