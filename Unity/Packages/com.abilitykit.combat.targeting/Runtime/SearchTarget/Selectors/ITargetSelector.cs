using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 选择器可读取的候选命中视图。
    /// </summary>
    public readonly struct SearchHitView
    {
        private readonly List<SearchHit> _hits;

        internal SearchHitView(List<SearchHit> hits)
        {
            _hits = hits ?? throw new ArgumentNullException(nameof(hits));
        }

        public int Count => _hits.Count;
        public SearchHit this[int index] => _hits[index];

        internal List<SearchHit> MutableHits => _hits;
    }

    /// <summary>
    /// 选择器使用的受控结果写入器。
    /// </summary>
    public readonly struct SearchResultWriter
    {
        private readonly List<EntityId> _results;
        private readonly int _maxCount;

        internal SearchResultWriter(List<EntityId> results, int maxCount)
        {
            _results = results ?? throw new ArgumentNullException(nameof(results));
            _maxCount = maxCount;
        }

        public int Count => _results.Count;
        public int RemainingCount => _maxCount <= 0 ? int.MaxValue : Math.Max(0, _maxCount - _results.Count);

        public void Add(EntityId id)
        {
            if (id.IsValid && (_maxCount <= 0 || _results.Count < _maxCount))
            {
                _results.Add(id);
            }
        }
    }

    /// <summary>
    /// 从合格候选中选择最终结果的策略。
    /// </summary>
    public interface ITargetSelector
    {
        void Select(
            in SearchQuery query,
            SearchContext context,
            SearchHitView hits,
            SearchResultWriter results);
    }

    /// <summary>
    /// 声明选择器使用按查询排序项计算的 Top-K 语义，可由引擎融合到候选遍历中。
    /// </summary>
    public interface IStreamingTopKByScoreSelector : ITargetSelector
    {
    }
}
