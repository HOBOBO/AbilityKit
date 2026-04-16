using System;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 触发器计划转换器
    /// 统一 TriggerPlanJsonDatabase 的转换逻辑
    /// </summary>
    internal sealed class TriggerPlanConverter
    {
        internal TriggerPlan<object> Convert(TriggerPlanJsonDatabase.TriggerPlanDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var actions = ConvertActions(dto.Actions);
            var pred = dto.Predicate;

            if (pred == null || string.Equals(pred.Kind, "none", StringComparison.OrdinalIgnoreCase))
            {
                return new TriggerPlan<object>(
                    phase: dto.Phase,
                    priority: dto.Priority,
                    triggerId: dto.TriggerId,
                    actions: actions,
                    interruptPriority: 0,
                    cue: null,
                    schedule: default);
            }

            if (string.Equals(pred.Kind, "expr", StringComparison.OrdinalIgnoreCase))
            {
                var expr = new PredicateExprPlan(BuildExprNodes(pred.Nodes));
                return new TriggerPlan<object>(
                    phase: dto.Phase,
                    priority: dto.Priority,
                    triggerId: dto.TriggerId,
                    predicateExpr: expr,
                    actions: actions,
                    interruptPriority: 0,
                    cue: null,
                    schedule: default);
            }

            throw new NotSupportedException($"Predicate kind not supported: {pred.Kind}");
        }

        internal ActionCallPlan[] ConvertActions(System.Collections.Generic.List<TriggerPlanJsonDatabase.ActionCallPlanDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<ActionCallPlan>();

            var arr = new ActionCallPlan[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                arr[i] = ConvertAction(dtos[i]);
            }
            return arr;
        }

        private ActionCallPlan ConvertAction(TriggerPlanJsonDatabase.ActionCallPlanDto dto)
        {
            if (dto == null) return default;

            var id = new ActionId(dto.ActionId);

            if (dto.Args != null && dto.Args.Count > 0)
            {
                var namedArgs = new System.Collections.Generic.Dictionary<string, ActionArgValue>(dto.Args.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var kv in dto.Args)
                {
                    namedArgs[kv.Key] = new ActionArgValue(ConvertNumericValueRef(kv.Value), kv.Key);
                }
                return ActionCallPlan.WithArgs(id, namedArgs);
            }

            switch (dto.Arity)
            {
                case 0:
                    return new ActionCallPlan(id);
                case 1:
                    return new ActionCallPlan(id, ConvertNumericValueRef(dto.Arg0));
                case 2:
                    return new ActionCallPlan(id, ConvertNumericValueRef(dto.Arg0), ConvertNumericValueRef(dto.Arg1));
                default:
                    throw new InvalidOperationException($"Unsupported action arity: {dto.Arity} actionId={dto.ActionId}");
            }
        }

        private NumericValueRef ConvertNumericValueRef(TriggerPlanJsonDatabase.NumericValueRefDto dto)
        {
            if (dto == null) return default;

            if (!Enum.TryParse<ENumericValueRefKind>(dto.Kind, out var kind))
            {
                throw new InvalidOperationException($"Unknown NumericValueRef kind: {dto.Kind}");
            }

            return kind switch
            {
                ENumericValueRefKind.Const => NumericValueRef.Const(dto.ConstValue),
                ENumericValueRefKind.Blackboard => NumericValueRef.Blackboard(dto.BoardId, dto.KeyId),
                ENumericValueRefKind.PayloadField => NumericValueRef.PayloadField(dto.FieldId),
                ENumericValueRefKind.Var => NumericValueRef.Var(dto.DomainId, dto.Key),
                ENumericValueRefKind.Expr => throw new NotSupportedException("UGC numeric value source kind 'Expr' is not allowed in JSON"),
                _ => throw new InvalidOperationException($"Unsupported NumericValueRef kind: {kind}")
            };
        }

        private BoolExprNode[] BuildExprNodes(System.Collections.Generic.List<TriggerPlanJsonDatabase.BoolExprNodeDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<BoolExprNode>();

            var arr = new BoolExprNode[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                if (d == null)
                {
                    arr[i] = BoolExprNode.Const(true);
                    continue;
                }

                if (!Enum.TryParse<EBoolExprNodeKind>(d.Kind, out var kind))
                {
                    throw new InvalidOperationException($"Unknown expr node kind: {d.Kind}");
                }

                switch (kind)
                {
                    case EBoolExprNodeKind.Const:
                        arr[i] = BoolExprNode.Const(d.ConstValue);
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
                        if (!Enum.TryParse<ECompareOp>(d.CompareOp, out var op))
                        {
                            throw new InvalidOperationException($"Unknown compare op: {d.CompareOp}");
                        }
                        arr[i] = BoolExprNode.Compare(op, ConvertNumericValueRef(d.Left), ConvertNumericValueRef(d.Right));
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported expr node kind: {kind}");
                }
            }

            return arr;
        }
    }
}
