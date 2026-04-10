using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public readonly struct SearchQuery
    {
        public readonly ICandidateProvider Provider;
        public readonly IReadOnlyList<ITargetRule> Rules;
        public readonly ITargetScorer Scorer;
        public readonly ITargetSelector Selector;
        public readonly int MaxCount;

        public SearchQuery(
            ICandidateProvider provider,
            IReadOnlyList<ITargetRule> rules,
            ITargetScorer scorer,
            ITargetSelector selector,
            int maxCount)
        {
            Provider = provider;
            Rules = rules;
            Scorer = scorer;
            Selector = selector;
            MaxCount = maxCount;
        }

        public bool HasMaxCount => MaxCount > 0;
    }
}
