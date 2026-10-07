using System;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaTagConditionCompiler
    {
        private const int MaxDepth = 16;
        private const int MaxNodes = 64;

        public static GameplayTagQuery Compile(TagConditionDTO condition)
        {
            if (condition == null) return GameplayTagQuery.None;
            var count = 0;
            return GameplayTagQuery.Create(CompileNode(condition, 0, ref count));
        }

        private static GameplayTagQueryNode CompileNode(TagConditionDTO condition, int depth, ref int count)
        {
            if (condition == null || depth >= MaxDepth || ++count > MaxNodes)
                throw new InvalidOperationException("Invalid or oversized MOBA tag condition tree.");

            var op = condition.Operator;
            if (string.Equals(op, "Tag", StringComparison.Ordinal))
            {
                if (condition.Children != null && condition.Children.Length > 0 ||
                    !MobaGameplayTagCatalog.TryGet(condition.Tag, out var tag))
                    throw new InvalidOperationException($"Unknown or malformed MOBA tag condition: {condition.Tag}");
                return GameplayTagQueryNode.CreateTagQuery(
                    condition.Exact ? GameplayTagQueryOperator.IncludeExactTag : GameplayTagQueryOperator.IncludeTags,
                    tag);
            }

            if (condition.Exact || !string.IsNullOrEmpty(condition.Tag))
                throw new InvalidOperationException($"Compound MOBA tag condition has leaf fields: {op}");
            var children = condition.Children;
            if (children == null || children.Length == 0 ||
                string.Equals(op, "Not", StringComparison.Ordinal) && children.Length != 1)
                throw new InvalidOperationException($"MOBA tag condition has invalid children: {op}");

            GameplayTagQueryOperator queryOp;
            switch (op)
            {
                case "All": queryOp = GameplayTagQueryOperator.And; break;
                case "Any": queryOp = GameplayTagQueryOperator.Or; break;
                case "Not": queryOp = GameplayTagQueryOperator.Not; break;
                default: throw new InvalidOperationException($"Unknown MOBA tag condition operator: {op}");
            }
            var compiled = new GameplayTagQueryNode[children.Length];
            for (var i = 0; i < children.Length; i++)
                compiled[i] = CompileNode(children[i], depth + 1, ref count);
            return GameplayTagQueryNode.CreateCompound(queryOp, compiled);
        }
    }
}
