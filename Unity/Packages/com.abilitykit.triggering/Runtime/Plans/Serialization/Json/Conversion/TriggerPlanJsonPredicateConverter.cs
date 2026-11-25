using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// ? JSON ???????????????????????????
    /// </summary>
    internal static class TriggerPlanJsonPredicateConverter
    {
        internal static ITriggerPlanCondition ConvertCondition(
            TriggerPlanJsonDatabase.PredicatePlanDto dto, in TriggerPlanJsonValueConverter values)
        {
            var expr = ConvertPredicateExpr(dto, in values);
            return expr.Nodes == null || expr.Nodes.Length == 0 ? null : new PredicateExprTriggerPlanCondition(expr);
        }

        internal static PredicateExprPlan ConvertPredicateExpr(
            TriggerPlanJsonDatabase.PredicatePlanDto dto, in TriggerPlanJsonValueConverter values)
        {
            if (dto == null || string.Equals(dto.Kind, "none", StringComparison.OrdinalIgnoreCase))
                return default;

            if (!string.Equals(dto.Kind, "expr", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Predicate kind not supported: {dto.Kind}");
            }

            return new PredicateExprPlan(BuildExprNodes(dto.Nodes, in values));
        }

        private static BoolExprNode[] BuildExprNodes(
            List<TriggerPlanJsonDatabase.BoolExprNodeDto> dtos, in TriggerPlanJsonValueConverter values)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<BoolExprNode>();

            var arr = new BoolExprNode[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var node = dtos[i];
                if (node == null)
                {
                    arr[i] = BoolExprNode.Const(true);
                    continue;
                }

                if (!Enum.TryParse<EBoolExprNodeKind>(node.Kind, out var kind))
                {
                    throw new InvalidOperationException($"Unknown expr node kind: {node.Kind}");
                }

                switch (kind)
                {
                    case EBoolExprNodeKind.Const:
                        arr[i] = BoolExprNode.Const(node.ConstValue);
                        break;
                    case EBoolExprNodeKind.Not:
                        arr[i] = BoolExprNode.Not();
                        break;
                    case EBoolExprNodeKind.And:
                        arr[i] = BoolExprNode.And();
                        break;
                    case EBoolExprNodeKind.Or:
                        arr[i] = BoolExprNode.Or();
                        break;
                    case EBoolExprNodeKind.CompareNumeric:
                        if (!Enum.TryParse<ECompareOp>(node.CompareOp, out var op))
                        {
                            throw new InvalidOperationException($"Unknown compare op: {node.CompareOp}");
                        }
                        arr[i] = BoolExprNode.Compare(op, values.ConvertNumeric(node.Left), values.ConvertNumeric(node.Right));
                        break;
                    case EBoolExprNodeKind.Function:
                        arr[i] = BoolExprNode.Function(new FunctionId(node.FunctionId), (byte)node.FunctionArity, values.ConvertNumeric(node.Left), values.ConvertNumeric(node.Right));
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported expr node kind: {kind}");
                }
            }

            return arr;
        }
    }
}
