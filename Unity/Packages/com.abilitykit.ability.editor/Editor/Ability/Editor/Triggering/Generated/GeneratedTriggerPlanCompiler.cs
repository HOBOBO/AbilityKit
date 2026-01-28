#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static partial class GeneratedTriggerPlanCompiler
    {
        public static bool TryCompileActionTree(
            JsonActionEditorConfig action,
            Func<string, int> payloadFieldIdResolver,
            Func<string, ActionId> actionIdResolver,
            out ActionCallPlan[] plans)
        {
            plans = null;
            if (action == null) return false;

            if (string.Equals(action.TypeValue, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                if (action.Items == null || action.Items.Count == 0)
                {
                    plans = Array.Empty<ActionCallPlan>();
                    return true;
                }

                var list = new List<ActionCallPlan>(action.Items.Count);
                for (int i = 0; i < action.Items.Count; i++)
                {
                    if (!(action.Items[i] is JsonActionEditorConfig child)) return false;
                    if (!TryCompileActionTree(child, payloadFieldIdResolver, actionIdResolver, out var childPlans)) return false;
                    if (childPlans == null || childPlans.Length == 0) continue;
                    for (int j = 0; j < childPlans.Length; j++) list.Add(childPlans[j]);
                }

                plans = list.ToArray();
                return true;
            }

            if (actionIdResolver == null) return false;
            var id = actionIdResolver(action.TypeValue);
            if (id.Value == 0) return false;

            return TryCompileAction(id, action.Args, payloadFieldIdResolver, out plans);
        }

        public static bool TryCompileConditionTree(
            JsonConditionEditorConfig condition,
            Func<string, int> payloadFieldIdResolver,
            out PredicateExprPlan plan)
        {
            plan = default;
            if (condition == null) return false;

            if (string.Equals(condition.TypeValue, TriggerConditionTypes.All, StringComparison.Ordinal))
            {
                return TryCompileAll(condition.Items, payloadFieldIdResolver, out plan);
            }

            if (string.Equals(condition.TypeValue, TriggerConditionTypes.Any, StringComparison.Ordinal))
            {
                return TryCompileAny(condition.Items, payloadFieldIdResolver, out plan);
            }

            if (string.Equals(condition.TypeValue, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                var inner = condition.Item as JsonConditionEditorConfig;
                var nodes = new List<BoolExprNode>();
                if (!AppendExpr(nodes, inner, payloadFieldIdResolver)) return false;
                nodes.Add(BoolExprNode.Not());
                plan = new PredicateExprPlan(nodes.ToArray());
                return true;
            }

            if (!TryCompileCondition(condition.TypeValue, condition.Args, payloadFieldIdResolver, out var expr))
            {
                return false;
            }

            plan = expr;
            return true;
        }

        private static bool TryCompileAll(List<ConditionEditorConfigBase> items, Func<string, int> payloadFieldIdResolver, out PredicateExprPlan plan)
        {
            plan = default;
            if (items == null || items.Count == 0)
            {
                plan = new PredicateExprPlan(new[] { BoolExprNode.Const(true) });
                return true;
            }

            var nodes = new List<BoolExprNode>();
            var first = true;
            for (int i = 0; i < items.Count; i++)
            {
                var c = items[i] as JsonConditionEditorConfig;
                if (c == null) return false;
                if (!AppendExpr(nodes, c, payloadFieldIdResolver)) return false;
                if (!first) nodes.Add(BoolExprNode.And());
                first = false;
            }

            if (first) nodes.Add(BoolExprNode.Const(true));
            plan = new PredicateExprPlan(nodes.ToArray());
            return true;
        }

        private static bool TryCompileAny(List<ConditionEditorConfigBase> items, Func<string, int> payloadFieldIdResolver, out PredicateExprPlan plan)
        {
            plan = default;
            if (items == null || items.Count == 0)
            {
                plan = new PredicateExprPlan(new[] { BoolExprNode.Const(false) });
                return true;
            }

            var nodes = new List<BoolExprNode>();
            var first = true;
            for (int i = 0; i < items.Count; i++)
            {
                var c = items[i] as JsonConditionEditorConfig;
                if (c == null) return false;
                if (!AppendExpr(nodes, c, payloadFieldIdResolver)) return false;
                if (!first) nodes.Add(BoolExprNode.Or());
                first = false;
            }

            if (first) nodes.Add(BoolExprNode.Const(false));
            plan = new PredicateExprPlan(nodes.ToArray());
            return true;
        }

        private static bool AppendExpr(List<BoolExprNode> nodes, JsonConditionEditorConfig cond, Func<string, int> payloadFieldIdResolver)
        {
            if (cond == null)
            {
                nodes.Add(BoolExprNode.Const(true));
                return true;
            }

            if (!TryCompileConditionTree(cond, payloadFieldIdResolver, out var plan)) return false;

            if (plan.Nodes != null)
            {
                for (int i = 0; i < plan.Nodes.Length; i++) nodes.Add(plan.Nodes[i]);
            }
            return true;
        }

        // Implementations are provided by GeneratedTriggerPlanCompiler.g.cs (or other generated partials).
    }
}
#endif
