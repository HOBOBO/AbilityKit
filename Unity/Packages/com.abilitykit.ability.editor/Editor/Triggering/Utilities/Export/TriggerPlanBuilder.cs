#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerPlanBuilder
    {
        internal static bool TryCompilePlanFromEditor(
            TriggerEditorConfig tr,
            Dictionary<int, string> stringTable,
            out TriggerPlan<object> plan,
            out int phase,
            out int priority,
            out string failReason)
        {
            plan = default;
            phase = 0;
            priority = 0;
            failReason = null;

            JsonConditionEditorConfig cond = null;
            if (tr.ConditionsStrong != null && tr.ConditionsStrong.Count > 0)
            {
                if (tr.ConditionsStrong.Count == 1)
                {
                    cond = ToJsonConditionNode(tr.ConditionsStrong[0]);
                }
                else
                {
                    var items = new List<ConditionEditorConfigBase>(tr.ConditionsStrong.Count);
                    for (int i = 0; i < tr.ConditionsStrong.Count; i++)
                    {
                        var n = ToJsonConditionNode(tr.ConditionsStrong[i]);
                        if (n != null) items.Add(n);
                    }
                    cond = new JsonConditionEditorConfig { TypeValue = TriggerConditionTypes.All, Items = items };
                }
            }

            JsonActionEditorConfig act = null;
            if (tr.ActionsStrong != null && tr.ActionsStrong.Count > 0)
            {
                if (tr.ActionsStrong.Count == 1)
                {
                    act = ToJsonActionNode(tr.ActionsStrong[0]);
                }
                else
                {
                    var items = new List<ActionEditorConfigBase>(tr.ActionsStrong.Count);
                    for (int i = 0; i < tr.ActionsStrong.Count; i++)
                    {
                        var n = ToJsonActionNode(tr.ActionsStrong[i]);
                        if (n != null) items.Add(n);
                    }
                    act = new JsonActionEditorConfig { TypeValue = TriggerActionTypes.Seq, Items = items };
                }
            }

            if (act == null)
            {
                failReason = "no_actions";
                return false;
            }

            if (!TryCompileActionTree(
                    act,
                    stringTable,
                    TriggerPlanCompilerResolvers.ResolvePayloadFieldId,
                    TriggerPlanCompilerResolvers.ResolveActionId,
                    out var actions) || actions == null || actions.Length == 0)
            {
                failReason = "action_compile_failed";
                return false;
            }

            if (cond != null)
            {
                if (!TryCompileConditionTree(
                        cond,
                        TriggerPlanCompilerResolvers.ResolvePayloadFieldId,
                        out var predExpr))
                {
                    failReason = "condition_compile_failed";
                    return false;
                }

                plan = new TriggerPlan<object>(phase, priority, predExpr, actions);
                return true;
            }

            plan = new TriggerPlan<object>(phase, priority, actions);
            return true;
        }

        internal static bool TryCompileActionTree(
            JsonActionEditorConfig action,
            Dictionary<int, string> stringTable,
            Func<string, int> payloadFieldIdResolver,
            Func<string, ActionId> actionIdResolver,
            out ActionCallPlan[] plans)
        {
            plans = null;
            if (action == null) return false;

            // Custom: seq flatten
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
                    if (!TryCompileActionTree(child, stringTable, payloadFieldIdResolver, actionIdResolver, out var childPlans)) return false;
                    if (childPlans == null || childPlans.Length == 0) continue;
                    for (int j = 0; j < childPlans.Length; j++) list.Add(childPlans[j]);
                }

                plans = list.ToArray();
                return true;
            }

            var handlers = TriggerPlanExportActionHandlerRegistry.Handlers;
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var h = handlers[i];
                    if (h == null) continue;
                    if (h.TryCompileAction(action, stringTable, payloadFieldIdResolver, actionIdResolver, out plans))
                    {
                        return plans != null;
                    }
                }
            }

            // Default: codegen/fallback compiler
            return GeneratedTriggerPlanCompiler.TryCompileActionTree(action, payloadFieldIdResolver, actionIdResolver, out plans);
        }

        internal static bool TryCompileConditionTree(
            JsonConditionEditorConfig condition,
            Func<string, int> payloadFieldIdResolver,
            out PredicateExprPlan plan)
        {
            plan = default;
            if (condition == null) return false;

            var handlers = TriggerPlanExportConditionHandlerRegistry.Handlers;
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var h = handlers[i];
                    if (h == null) continue;
                    if (h.TryCompileCondition(condition, payloadFieldIdResolver, out plan))
                    {
                        return true;
                    }
                }
            }

            return GeneratedTriggerPlanCompiler.TryCompileConditionTree(condition, payloadFieldIdResolver, out plan);
        }

        internal static JsonActionEditorConfig ToJsonActionNode(ActionEditorConfigBase node)
        {
            if (node == null) return null;
            if (node is JsonActionEditorConfig j) return j;

            var handlers = TriggerPlanExportActionHandlerRegistry.Handlers;
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var h = handlers[i];
                    if (h == null) continue;
                    if (h.TryConvertActionNode(node, out var converted) && converted != null)
                    {
                        return converted;
                    }
                }
            }

            try
            {
                var rt = node.ToRuntimeStrong();
                if (rt == null) return null;
                var def = rt.ToActionDef();
                var dto = BuildActionDto(def);
                return JsonActionEditorConfig.FromDto(dto);
            }
            catch (Exception ex)
            {
                ExportLog.Exception(ex, $"ToJsonActionNode failed. nodeType={node.GetType().FullName}");
                return null;
            }
        }

        internal static JsonConditionEditorConfig ToJsonConditionNode(ConditionEditorConfigBase node)
        {
            if (node == null) return null;
            if (node is JsonConditionEditorConfig j) return j;

            var handlers = TriggerPlanExportConditionHandlerRegistry.Handlers;
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var h = handlers[i];
                    if (h == null) continue;
                    if (h.TryConvertConditionNode(node, out var converted) && converted != null)
                    {
                        return converted;
                    }
                }
            }

            try
            {
                var rt = node.ToRuntimeStrong();
                if (rt == null) return null;
                var def = rt.ToConditionDef();
                var dto = BuildConditionDto(def);
                return JsonConditionEditorConfig.FromDto(dto);
            }
            catch (Exception ex)
            {
                ExportLog.Exception(ex, $"ToJsonConditionNode failed. nodeType={node.GetType().FullName}");
                return null;
            }
        }

        internal static ConditionDTO BuildConditionDto(AbilityKit.Ability.Triggering.Definitions.ConditionDef def)
        {
            if (def == null) return null;

            if (string.Equals(def.Type, TriggerConditionTypes.All, StringComparison.Ordinal) || string.Equals(def.Type, TriggerConditionTypes.Any, StringComparison.Ordinal))
            {
                var node = new ConditionDTO
                {
                    Type = def.Type,
                    Items = new List<ConditionDTO>()
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList items))
                {
                    throw new InvalidOperationException($"Condition '{def.Type}' requires args['items'] as IList<ConditionDef>");
                }

                node.Items = new List<ConditionDTO>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    if (!(items[i] is AbilityKit.Ability.Triggering.Definitions.ConditionDef childDef)) continue;
                    var child = BuildConditionDto(childDef);
                    if (child != null) node.Items.Add(child);
                }
                return node;
            }

            if (string.Equals(def.Type, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                var node = new ConditionDTO
                {
                    Type = def.Type
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Item, out var itemObj) || !(itemObj is AbilityKit.Ability.Triggering.Definitions.ConditionDef item))
                {
                    throw new InvalidOperationException("Condition 'not' requires args['item'] as ConditionDef");
                }

                node.Item = BuildConditionDto(item);
                return node;
            }

            var dto = new ConditionDTO
            {
                Type = def.Type,
                Args = AbilityTriggerExportUtils.CopyArgs(def.Args)
            };
            return dto;
        }

        internal static ActionDTO BuildActionDto(AbilityKit.Ability.Triggering.Definitions.ActionDef def)
        {
            if (def == null) return null;

            if (string.Equals(def.Type, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                var dto = new ActionDTO
                {
                    Type = def.Type,
                    Items = new List<ActionDTO>()
                };

                if (def.Args == null || !def.Args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList items))
                {
                    throw new InvalidOperationException("seq action requires args['items'] as IList<ActionDef>");
                }

                dto.Items = new List<ActionDTO>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    if (!(items[i] is AbilityKit.Ability.Triggering.Definitions.ActionDef childDef)) continue;
                    var child = BuildActionDto(childDef);
                    if (child != null) dto.Items.Add(child);
                }
                return dto;
            }

            var node = new ActionDTO
            {
                Type = def.Type,
                Args = AbilityTriggerExportUtils.CopyArgs(def.Args)
            };
            return node;
        }
    }
}
#endif
