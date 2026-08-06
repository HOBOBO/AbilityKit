using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    public ref struct SearchPipelineBuilder
    {
        private ICandidateProvider _provider;
        private List<ITargetRule> _rules;
        private bool _ownsRules;
        private List<SearchOrder> _orders;
        private bool _ownsOrders;
        private ITargetSelector _selector;
        private int _maxCount;
        private SearchSortDirection _sortDirection;
        private SearchDuplicatePolicy _duplicatePolicy;

        private static readonly List<ITargetRule> s_emptyRules = new List<ITargetRule>(0);

        private SearchPipelineBuilder(bool initialize)
        {
            _provider = null;
            _rules = null;
            _ownsRules = false;
            _orders = null;
            _ownsOrders = false;
            _selector = null;
            _maxCount = 0;
            _sortDirection = SearchSortDirection.ScoreDescending;
            _duplicatePolicy = SearchDuplicatePolicy.Preserve;
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
            EnsureRuleList();
            _rules.Add(rule);
            return this;
        }

        public SearchPipelineBuilder Filter(params ITargetRule[] rules)
        {
            if (rules == null || rules.Length == 0) return this;
            EnsureRuleList();
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
            return ScoreBy(scorer, _sortDirection);
        }

        public SearchPipelineBuilder ScoreBy(ITargetScorer scorer, SearchSortDirection direction)
        {
            EnsureOrderList();
            _orders.Clear();
            if (scorer != null) _orders.Add(new SearchOrder(scorer, direction));
            _sortDirection = direction;
            return this;
        }

        public SearchPipelineBuilder ScoreById(int scorerId)
        {
            var scorer = TargetScorerRegistry.Instance.Create(scorerId);
            return scorer != null ? ScoreBy(scorer) : this;
        }

        public SearchPipelineBuilder ScoreById(int scorerId, SearchSortDirection direction)
        {
            var scorer = TargetScorerRegistry.Instance.Create(scorerId);
            return scorer != null ? ScoreBy(scorer, direction) : this;
        }

        public SearchPipelineBuilder ThenScoreBy(
            ITargetScorer scorer,
            SearchSortDirection direction = SearchSortDirection.ScoreDescending)
        {
            if (scorer == null) return this;
            EnsureOrderList();
            _orders.Add(new SearchOrder(scorer, direction));
            return this;
        }

        public SearchPipelineBuilder ThenScoreById(
            int scorerId,
            SearchSortDirection direction = SearchSortDirection.ScoreDescending)
        {
            return ThenScoreBy(TargetScorerRegistry.Instance.Create(scorerId), direction);
        }

        public SearchPipelineBuilder Select(ITargetSelector selector)
        {
            _selector = selector;
            return this;
        }

        public SearchPipelineBuilder SelectById(int selectorId)
        {
            var selector = TargetSelectorRegistry.Instance.Create(selectorId);
            if (selector != null) _selector = selector;
            return this;
        }

        public SearchPipelineBuilder Take(int maxCount)
        {
            if (maxCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            _maxCount = maxCount;
            return this;
        }

        public SearchPipelineBuilder PreserveDuplicateCandidates()
        {
            _duplicatePolicy = SearchDuplicatePolicy.Preserve;
            return this;
        }

        public SearchPipelineBuilder DistinctCandidatesByEntityKey()
        {
            _duplicatePolicy = SearchDuplicatePolicy.DistinctByEntityKey;
            return this;
        }

        public SearchPipelineBuilder OrderByScoreDescending()
        {
            return SetPrimaryDirection(SearchSortDirection.ScoreDescending);
        }

        public SearchPipelineBuilder OrderByScoreAscending()
        {
            return SetPrimaryDirection(SearchSortDirection.ScoreAscending);
        }

        public SearchQuery Build()
        {
            var rules = _rules != null && _rules.Count > 0 ? _rules : s_emptyRules;
            return new SearchQuery(
                _provider,
                rules,
                _orders,
                _selector,
                _maxCount,
                _duplicatePolicy);
        }

        [Obsolete("Build already creates an owned rule snapshot. Use Build instead.")]
        public SearchQuery BuildCopy()
        {
            return Build();
        }

        public SearchResult Execute(TargetSearchEngine engine, SearchContext context)
        {
            var query = Build();
            return engine.SearchIds(in query, context);
        }

        public void Execute(TargetSearchEngine engine, SearchContext context, List<EntityId> results)
        {
            var query = Build();
            engine.SearchIds(in query, context, results);
        }

        public void Dispose()
        {
            if (_ownsRules)
            {
                TargetingPool.ReleaseRuleList(_rules);
            }
            if (_ownsOrders)
            {
                TargetingPool.ReleaseOrderList(_orders);
            }

            _provider = null;
            _rules = null;
            _ownsRules = false;
            _orders = null;
            _ownsOrders = false;
            _selector = null;
            _maxCount = 0;
            _sortDirection = SearchSortDirection.ScoreDescending;
            _duplicatePolicy = SearchDuplicatePolicy.Preserve;
        }

        private SearchPipelineBuilder SetPrimaryDirection(SearchSortDirection direction)
        {
            _sortDirection = direction;
            if (_orders != null && _orders.Count > 0)
            {
                _orders[0] = new SearchOrder(_orders[0].Scorer, direction);
            }
            return this;
        }

        private void EnsureRuleList()
        {
            if (_rules != null) return;
            _rules = TargetingPool.RentRuleList();
            _ownsRules = true;
        }

        private void EnsureOrderList()
        {
            if (_orders != null) return;
            _orders = TargetingPool.RentOrderList();
            _ownsOrders = true;
        }
    }
}
