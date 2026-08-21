#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerPlanDtoBuilder
    {
        public static TriggerPlanDto BuildTriggerPlanDto(TriggerEditorConfig tr, in TriggerPlan<object> plan, int phase, int priority)
        {
            try
            {
                var eventName = tr.EventId;
                var eventId = !string.IsNullOrEmpty(eventName) ? StableStringId.Get("event:" + eventName) : 0;

                return new TriggerPlanDto
                {
                    TriggerId = tr.TriggerId,
                    EventName = eventName,
                    EventId = eventId,
                    Phase = phase,
                    Priority = priority,
                    Predicate = BuildPredicateDto(in plan),
                    Actions = BuildActionsDto(in plan),
                    LegacyPredicate = null,
                    LegacyActions = null,
                };
            }
            catch (Exception ex)
            {
                var triggerId = tr != null ? tr.TriggerId : 0;
                var eventName = tr != null ? tr.EventId : null;
                ExportLog.Exception(ex, $"BuildTriggerPlanDto failed. triggerId={triggerId} eventId='{eventName ?? string.Empty}'");
                throw;
            }
        }

        private static PredicatePlanDto BuildPredicateDto(in TriggerPlan<object> plan)
        {
            if (!plan.HasPredicate || plan.PredicateKind == EPredicateKind.None)
            {
                return new PredicatePlanDto { Kind = "none", Nodes = null };
            }

            if (plan.PredicateKind == EPredicateKind.Expr)
            {
                var nodes = plan.PredicateExpr.Nodes;
                var list = nodes != null ? new List<BoolExprNodeDto>(nodes.Length) : null;
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var n = nodes[i];
                        list.Add(new BoolExprNodeDto
                        {
                            Kind = n.Kind.ToString(),
                            ConstValue = n.ConstValue,
                            CompareOp = n.CompareOp.ToString(),
                            Left = BuildNumericValueRefDto(in n.Left),
                            Right = BuildNumericValueRefDto(in n.Right)
                        });
                    }
                }

                return new PredicatePlanDto { Kind = "expr", Nodes = list };
            }

            return new PredicatePlanDto { Kind = "function", Nodes = null };
        }

        private static List<ActionCallPlanDto> BuildActionsDto(in TriggerPlan<object> plan)
        {
            var actions = plan.Actions;
            if (actions == null || actions.Length == 0) return null;

            var list = new List<ActionCallPlanDto>(actions.Length);
            for (int i = 0; i < actions.Length; i++)
            {
                var a = actions[i];
                var dto = new ActionCallPlanDto
                {
                    ActionId = a.Id.Value,
                    Arity = a.Arity,
                    Arg0 = BuildNumericValueRefDto(in a.Arg0),
                    Arg1 = BuildNumericValueRefDto(in a.Arg1)
                };

                // 具名参数模式下，运行时执行器只接受 0/1/2 三种 arity；
                // 额外业务参数通过 Args 字典传递，不应把字典键数直接当作可执行参数个数写回 DTO。
                if (a.HasNamedArgs)
                {
                    dto.Arity = (byte)Math.Min(2, a.Args?.Count ?? 0);
                    dto.Args = new Dictionary<string, NumericValueRefDto>(a.Args.Count);
                    foreach (var kv in a.Args)
                    {
                        if (kv.Value.Kind == ActionArgKind.BlackboardTarget)
                        {
                            var target = kv.Value.BlackboardTarget;
                            dto.Args[kv.Key] = new NumericValueRefDto
                            {
                                Kind = "BlackboardTarget",
                                BoardId = target.BoardId,
                                KeyId = target.KeyId,
                                KeyType = target.KeyType,
                                Scope = target.Scope
                            };
                            continue;
                        }

                        if (kv.Value.Kind == ActionArgKind.BooleanValue || kv.Value.Kind == ActionArgKind.StringValue)
                        {
                            dto.Args[kv.Key] = new NumericValueRefDto
                            {
                                Kind = kv.Value.Kind == ActionArgKind.BooleanValue ? "Bool" : "String",
                                BoolValue = kv.Value.BooleanValue,
                                StringValue = kv.Value.StringValue
                            };
                            continue;
                        }

                        dto.Args[kv.Key] = new NumericValueRefDto
                        {
                            Kind = kv.Value.Ref.Kind.ToString(),
                            ConstValue = kv.Value.Ref.ConstValue,
                            BoardId = kv.Value.Ref.BoardId,
                            KeyId = kv.Value.Ref.KeyId,
                            FieldId = kv.Value.Ref.FieldId,
                            DomainId = kv.Value.Ref.DomainId,
                            Key = kv.Value.Ref.Key,
                            ExprText = kv.Value.Ref.ExprText
                        };
                    }
                }

                list.Add(dto);
            }
            return list;
        }

        private static NumericValueRefDto BuildNumericValueRefDto(in NumericValueRef r)
        {
            return new NumericValueRefDto
            {
                Kind = r.Kind.ToString(),
                ConstValue = r.ConstValue,
                BoardId = r.BoardId,
                KeyId = r.KeyId,
                FieldId = r.FieldId,
                DomainId = r.DomainId,
                Key = r.Key,
                ExprText = r.ExprText
            };
        }
    }
}
#endif
