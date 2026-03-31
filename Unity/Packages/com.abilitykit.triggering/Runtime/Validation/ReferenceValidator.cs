using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 引用校验器
    /// 检测未注册的 FunctionId、ActionId、黑板域等引用
    /// </summary>
    public sealed class ReferenceValidator<TCtx> : ITriggerValidator<TCtx>
    {
        public string Name => "引用校验";
        public int Priority => 1;
        public bool IsCritical => true;

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
        {
            var result = new ValidationResult();

            foreach (var entry in database.Plans)
            {
                ValidateEntry(entry, context, ref result);
            }

            return result;
        }

        private void ValidateEntry(TriggerPlanEntry<TCtx> entry, in ValidationContext<TCtx> context, ref ValidationResult result)
        {
            var path = entry.GetPath();
            var plan = entry.Plan;

            // 校验 Predicate 引用的 FunctionId
            if (plan.HasPredicate && plan.PredicateKind == EPredicateKind.Function)
            {
                var funcIdStr = plan.PredicateId.Value.ToString();
                if (context.DefinedFunctionIds != null &&
                    context.DefinedFunctionIds.Count > 0 &&
                    !context.DefinedFunctionIds.Contains(funcIdStr))
                {
                    result.AddError(
                        ValidationErrorCodes.FUNCTION_NOT_FOUND,
                        $"引用的函数 '{funcIdStr}' 未注册",
                        $"{path}.predicate");
                }
            }

            // 校验 Action 引用的 ActionId
            if (plan.Actions != null)
            {
                for (int i = 0; i < plan.Actions.Length; i++)
                {
                    var action = plan.Actions[i];
                    var actionIdStr = action.Id.Value.ToString();

                    if (context.DefinedActionIds != null &&
                        context.DefinedActionIds.Count > 0 &&
                        !context.DefinedActionIds.Contains(actionIdStr))
                    {
                        result.AddError(
                            ValidationErrorCodes.ACTION_NOT_FOUND,
                            $"引用的动作 '{actionIdStr}' 未注册",
                            $"{path}.actions[{i}]");
                    }

                    // 校验 Action 参数中的引用
                    ValidateNumericValueRef(action.Arg0, path, $"actions[{i}].arg0", ref result);
                    ValidateNumericValueRef(action.Arg1, path, $"actions[{i}].arg1", ref result);
                }
            }

            // 校验 Predicate 参数中的引用
            if (plan.HasPredicate)
            {
                ValidateNumericValueRef(plan.PredicateArg0, path, "predicate.arg0", ref result);
                ValidateNumericValueRef(plan.PredicateArg1, path, "predicate.arg1", ref result);

                // 校验 PredicateExpr 中的引用
                if (plan.PredicateKind == EPredicateKind.Expr)
                {
                    ValidatePredicateExpr(plan.PredicateExpr, path, ref result);
                }
            }
        }

        private void ValidateNumericValueRef(
            NumericValueRef valueRef,
            string entryPath,
            string fieldPath,
            ref ValidationResult result)
        {
            switch (valueRef.Kind)
            {
                case ENumericValueRefKind.Var:
                    if (!string.IsNullOrEmpty(valueRef.ExprText))
                    {
                        result.AddError(
                            ValidationErrorCodes.INVALID_EXPRESSION,
                            $"表达式引用为空",
                            $"{entryPath}.{fieldPath}");
                    }
                    break;
            }
        }

        private void ValidatePredicateExpr(
            PredicateExprPlan expr,
            string entryPath,
            ref ValidationResult result)
        {
            if (expr.Nodes == null)
                return;

            for (int i = 0; i < expr.Nodes.Length; i++)
            {
                var node = expr.Nodes[i];
                var nodePath = $"{entryPath}.predicate.nodes[{i}]";

                if (node.Kind == EBoolExprNodeKind.CompareNumeric)
                {
                    ValidateNumericValueRef(node.Left, nodePath, "left", ref result);
                    ValidateNumericValueRef(node.Right, nodePath, "right", ref result);
                }
            }
        }
    }
}
