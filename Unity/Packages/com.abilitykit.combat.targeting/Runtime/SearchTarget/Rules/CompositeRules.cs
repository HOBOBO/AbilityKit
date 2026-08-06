using System;

namespace AbilityKit.Battle.SearchTarget.Rules
{
    /// <summary>
    /// 所有非空子规则均通过时通过，并在首个失败结果处停止求值。
    /// 空规则集合视为通过。
    /// </summary>
    public sealed class AndRule : ITargetRule
    {
        private readonly ITargetRule[] _rules;

        public AndRule(params ITargetRule[] rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            _rules = Copy(rules);
        }

        public bool Test(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (rule != null && !rule.Test(in query, context, candidate)) return false;
            }
            return true;
        }

        private static ITargetRule[] Copy(ITargetRule[] rules)
        {
            if (rules.Length == 0) return Array.Empty<ITargetRule>();

            var snapshot = new ITargetRule[rules.Length];
            Array.Copy(rules, snapshot, rules.Length);
            return snapshot;
        }
    }

    /// <summary>
    /// 任一非空子规则通过时通过，并在首个成功结果处停止求值。
    /// 空规则集合视为不通过。
    /// </summary>
    public sealed class OrRule : ITargetRule
    {
        private readonly ITargetRule[] _rules;

        public OrRule(params ITargetRule[] rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            _rules = Copy(rules);
        }

        public bool Test(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (rule != null && rule.Test(in query, context, candidate)) return true;
            }
            return false;
        }

        private static ITargetRule[] Copy(ITargetRule[] rules)
        {
            if (rules.Length == 0) return Array.Empty<ITargetRule>();

            var snapshot = new ITargetRule[rules.Length];
            Array.Copy(rules, snapshot, rules.Length);
            return snapshot;
        }
    }

    /// <summary>
    /// 对单个子规则的结果取反。
    /// </summary>
    public sealed class NotRule : ITargetRule
    {
        private readonly ITargetRule _rule;

        public NotRule(ITargetRule rule)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        public bool Test(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return !_rule.Test(in query, context, candidate);
        }
    }
}
