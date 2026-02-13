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
                list.Add(new ActionCallPlanDto
                {
                    ActionId = a.Id.Value,
                    Arity = a.Arity,
                    Arg0 = BuildNumericValueRefDto(in a.Arg0),
                    Arg1 = BuildNumericValueRefDto(in a.Arg1)
                });
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
