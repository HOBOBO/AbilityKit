using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 搜索查询结构
    /// </summary>
    public readonly struct SearchQuery
    {
        private static readonly ITargetRule[] EmptyRules = new ITargetRule[0];
        private static readonly SearchOrder[] EmptyOrders = new SearchOrder[0];

        public readonly ICandidateProvider Provider;
        public readonly IReadOnlyList<ITargetRule> Rules;
        public readonly IReadOnlyList<SearchOrder> Orders;
        public readonly ITargetSelector Selector;
        public readonly int MaxCount;
        public readonly SearchDuplicatePolicy DuplicatePolicy;

        public SearchQuery(
            ICandidateProvider provider,
            IReadOnlyList<ITargetRule> rules,
            IReadOnlyList<SearchOrder> orders,
            ITargetSelector selector,
            int maxCount,
            SearchDuplicatePolicy duplicatePolicy = SearchDuplicatePolicy.Preserve)
        {
            if (maxCount < 0) throw new System.ArgumentOutOfRangeException(nameof(maxCount));
            if (duplicatePolicy != SearchDuplicatePolicy.Preserve &&
                duplicatePolicy != SearchDuplicatePolicy.DistinctByEntityKey)
            {
                throw new System.ArgumentOutOfRangeException(nameof(duplicatePolicy));
            }

            Provider = provider;
            Rules = CopyRules(rules);
            Orders = CopyOrders(orders);
            Selector = selector;
            MaxCount = maxCount;
            DuplicatePolicy = duplicatePolicy;
        }

        public bool HasMaxCount => MaxCount > 0;

        private static IReadOnlyList<SearchOrder> CopyOrders(IReadOnlyList<SearchOrder> orders)
        {
            if (orders == null || orders.Count == 0) return EmptyOrders;
            var copy = new SearchOrder[orders.Count];
            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order.Scorer == null)
                {
                    throw new System.ArgumentException(
                        $"Search order at index {i} requires a scorer.",
                        nameof(orders));
                }
                if (order.Direction != SearchSortDirection.ScoreDescending &&
                    order.Direction != SearchSortDirection.ScoreAscending)
                {
                    throw new System.ArgumentException(
                        $"Search order at index {i} has an invalid direction.",
                        nameof(orders));
                }
                copy[i] = order;
            }
            return copy;
        }

        private static IReadOnlyList<ITargetRule> CopyRules(IReadOnlyList<ITargetRule> rules)
        {
            if (rules == null || rules.Count == 0) return EmptyRules;

            var copy = new ITargetRule[rules.Count];
            for (int i = 0; i < rules.Count; i++)
            {
                copy[i] = rules[i];
            }
            return copy;
        }

    }

    public enum SearchSortDirection
    {
        ScoreDescending = 0,
        ScoreAscending = 1,
    }

    public enum SearchDuplicatePolicy
    {
        Preserve = 0,
        DistinctByEntityKey = 1,
    }
}
