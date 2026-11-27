using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 选择器可读取的候选命中视图。
    /// </summary>
    /// <remarks>
    /// 视图借用引擎的临时缓冲区，只能在当前选择回调内同步消费。
    /// </remarks>
    public readonly struct SearchHitView
    {
        private readonly List<SearchHit> _hits;

        internal SearchHitView(List<SearchHit> hits)
        {
            _hits = hits ?? throw new ArgumentNullException(nameof(hits));
        }

        /// <summary>
        /// 获取当前视图中的命中数，不受结果上限限制。
        /// </summary>
        public int Count => _hits.Count;

        /// <summary>
        /// 按从零开始的索引读取命中；命中值不是独立的评分快照。
        /// </summary>
        public SearchHit this[int index] => _hits[index];

        internal List<SearchHit> MutableHits => _hits;
    }

    /// <summary>
    /// 选择器使用的受控结果写入器。
    /// </summary>
    /// <remarks>
    /// 只允许在当前选择回调内同步追加，不得保留写入器或跨回调使用。
    /// </remarks>
    public readonly struct SearchResultWriter
    {
        private readonly List<EntityId> _results;
        private readonly int _maxCount;

        internal SearchResultWriter(List<EntityId> results, int maxCount)
        {
            _results = results ?? throw new ArgumentNullException(nameof(results));
            _maxCount = maxCount;
        }

        /// <summary>
        /// 获取已经写入的结果数。
        /// </summary>
        public int Count => _results.Count;

        /// <summary>
        /// 获取剩余输出名额；未设置上限时返回 <see cref='int.MaxValue'/>。
        /// </summary>
        public int RemainingCount => _maxCount <= 0 ? int.MaxValue : Math.Max(0, _maxCount - _results.Count);

        /// <summary>
        /// 按调用顺序追加目标；无效句柄和超限写入被忽略。
        /// </summary>
        /// <remarks>
        /// 选择器负责从命中视图挑选目标；写入器不检查成员资格，也不执行去重。
        /// </remarks>
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
        /// <summary>
        /// 在引擎完成候选过滤和评分后，从完整命中序列中选择并写入结果。
        /// </summary>
        /// <remarks>
        /// 引擎仅在有命中时同步调用本方法；输入是合格候选的遍历顺序，而非按分数预排序的序列。
        /// 视图与写入器不得逃逸出本次回调。回调异常传播给调用方，已经写入的结果不回滚。
        /// </remarks>
        void Select(
            in SearchQuery query,
            SearchContext context,
            SearchHitView hits,
            SearchResultWriter results);
    }

    /// <summary>
    /// 声明选择器使用按查询排序项计算的 Top-K 语义，可由引擎融合到候选遍历中。
    /// </summary>
    /// <remarks>
    /// 查询上限大于零时，引擎直接执行排序项和实体键定义的 Top-K，不调用 <see cref='ITargetSelector.Select'/>。
    /// 需要完整命中列表或自定义后处理的选择器不要实现本接口；无上限时仍走普通选择回调。
    /// </remarks>
    public interface IStreamingTopKByScoreSelector : ITargetSelector
    {
    }
}
