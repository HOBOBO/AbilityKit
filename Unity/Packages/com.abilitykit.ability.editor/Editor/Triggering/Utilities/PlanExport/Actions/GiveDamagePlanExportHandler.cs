#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    [TriggerPlanExportHandler(order: 10)]
    internal sealed class GiveDamagePlanExportHandler : ITriggerPlanExportActionHandler
    {
        public bool TryConvertActionNode(ActionEditorConfigBase strongNode, out JsonActionEditorConfig jsonNode)
        {
            jsonNode = null;
            return false;
        }

        public bool TryCompileAction(JsonActionEditorConfig action,
            Dictionary<int, string> stringTable,
            Func<string, int> payloadFieldIdResolver,
            Func<string, ActionId> actionIdResolver,
            out ActionCallPlan[] plans)
        {
            plans = null;
            if (action == null) return false;
            if (!string.Equals(action.TypeValue, TriggerActionTypes.GiveDamage, StringComparison.Ordinal)) return false;
            if (actionIdResolver == null) return false;

            var id = actionIdResolver(action.TypeValue);
            if (id.Value == 0) return false;

            var value = 0d;
            var reasonParam = 0d;
            if (action.Args != null)
            {
                if (!PlanExportArgReadUtil.TryReadDouble(action.Args, "value", out value))
                {
                    PlanExportArgReadUtil.TryReadDouble(action.Args, "Value", out value);
                }

                if (value == 0d)
                {
                    PlanExportArgReadUtil.TryReadDouble(action.Args, "damageValue", out value);
                }

                if (!PlanExportArgReadUtil.TryReadDouble(action.Args, "reasonParam", out reasonParam))
                {
                    PlanExportArgReadUtil.TryReadDouble(action.Args, "ReasonParam", out reasonParam);
                }

                if (value == 0d && action.Args.Count > 0)
                {
                    var dump = PlanExportArgReadUtil.DumpArgs(action.Args);
                    Debug.LogWarning($"[AbilityTriggerJsonExporter] give_damage compiled with value=0. type='{action.TypeValue}' actionId={id.Value} Available args: {dump}");
                }
            }
            else
            {
                Debug.LogWarning($"[AbilityTriggerJsonExporter] give_damage has null args. type='{action.TypeValue}' actionId={id.Value}");
            }

            if (action.Args != null && action.Args.Count == 0)
            {
                Debug.LogWarning($"[AbilityTriggerJsonExporter] give_damage has empty args. type='{action.TypeValue}' actionId={id.Value}");
            }

            plans = new[]
            {
                new ActionCallPlan(id, NumericValueRef.Const(value), NumericValueRef.Const(reasonParam))
            };
            return true;
        }
    }
}
#endif
