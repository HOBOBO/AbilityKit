using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    public ref struct SearchPipelineBuilder
    {
        private ICandidateProvider _provider;
        private List<ITargetRule> _rules;
        private ITargetScorer _scorer;
        private ITargetSelector _selector;
        private int _maxCount;
        private int _flags;

        private static readonly List<ITargetRule> s_emptyRules = new List<ITargetRule>(0);

        private SearchPipelineBuilder(bool initialize)
        {
            _provider = null;
            _rules = null;
            _scorer = null;
            _selector = null;
            _maxCount = 0;
            _flags = 0;
        }

        public static SearchPipelineBuilder Create() => new SearchPipelineBuilder(true);

        public SearchPipelineBuilder From(ICandidateProvider provider)
        {
            _provider = provider;
            return this;
        }

        public SearchPipelineBuilder Filter(ITargetRule rule)
        {
            if (rule == null) return this;
            if (_rules == null) _rules = new List<ITargetRule>(4);
            _rules.Add(rule);
            return this;
        }

        public SearchPipelineBuilder Filter(params ITargetRule[] rules)
        {
            if (rules == null || rules.Length == 0) return this;
            if (_rules == null) _rules = new List<ITargetRule>(rules.Length);
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] != null) _rules.Add(rules[i]);
            }
            return this;
        }

        public SearchPipelineBuilder FilterById(int ruleId)
        {
            var rule = TargetRuleRegistry.Instance.Create(ruleId);
            return Filter(rule);
        }

        public SearchPipelineBuilder ScoreBy(ITargetScorer scorer)
        {
            _scorer = scorer;
            return this;
        }

        public SearchPipelineBuilder ScoreById(int scorerId)
        {
            _scorer = TargetScorerRegistry.Instance.Create(scorerId);
            return this;
        }

        public SearchPipelineBuilder Select(ITargetSelector selector)
        {
            _selector = selector;
            return this;
        }

        public SearchPipelineBuilder SelectById(int selectorId)
        {
            _selector = TargetSelectorRegistry.Instance.Create(selectorId);
            return this;
        }

        public SearchPipelineBuilder Take(int maxCount)
        {
            _maxCount = maxCount;
            return this;
        }

        public SearchPipelineBuilder OrderByScoreDescending()
        {
            _flags |= (int)PipelineFlags.OrderByScoreDesc;
            return this;
        }

        public SearchPipelineBuilder OrderByScoreAscending()
        {
            _flags |= (int)PipelineFlags.OrderByScoreAsc;
            return this;
        }

        public SearchQuery Build()
        {
            var rules = _rules != null && _rules.Count > 0 ? _rules : s_emptyRules;
            return new SearchQuery(_provider, rules, _scorer, _selector, _maxCount, _flags);
        }

        public void Execute(TargetSearchEngine engine, SearchContext context, List<IEntityId> results)
        {
            var query = Build();
            engine.SearchIds(in query, context, results);
        }
    }
}
