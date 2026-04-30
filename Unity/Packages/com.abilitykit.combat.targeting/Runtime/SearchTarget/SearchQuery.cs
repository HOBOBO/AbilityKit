using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 搜索查询结构
    /// </summary>
    public readonly struct SearchQuery
    {
        public readonly ICandidateProvider Provider;
        public readonly IReadOnlyList<ITargetRule> Rules;
        public readonly ITargetScorer Scorer;
        public readonly ITargetSelector Selector;
        public readonly int MaxCount;
        public readonly int Flags;

        public SearchQuery(
            ICandidateProvider provider,
            IReadOnlyList<ITargetRule> rules,
            ITargetScorer scorer,
            ITargetSelector selector,
            int maxCount,
            int flags = 0)
        {
            Provider = provider;
            Rules = rules;
            Scorer = scorer;
            Selector = selector;
            MaxCount = maxCount;
            Flags = flags;
        }

        public SearchQuery(
            ICandidateProvider provider,
            IReadOnlyList<ITargetRule> rules,
            ITargetScorer scorer,
            ITargetSelector selector,
            int maxCount)
            : this(provider, rules, scorer, selector, maxCount, 0)
        {
        }

        public bool HasMaxCount => MaxCount > 0;

        public bool HasFlags(PipelineFlags flags) => (Flags & (int)flags) != 0;
    }

    /// <summary>
    /// Pipeline 标志位
    /// </summary>
    [System.Flags]
    public enum PipelineFlags
    {
        None = 0,
        OrderByScoreDesc = 1 << 0,
        OrderByScoreAsc = 1 << 1,
        ShortCircuitOnFirstHit = 1 << 2,
        CacheResults = 1 << 3,
    }
}
