#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

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
                    ExportLog.Warning($"give_damage compiled with value=0. type='{action.TypeValue}' actionId={id.Value} Available args: {dump}");
                }
            }
            else
            {
                ExportLog.Warning($"give_damage has null args. type='{action.TypeValue}' actionId={id.Value}");
            }

            if (action.Args != null && action.Args.Count == 0)
            {
                ExportLog.Warning($"give_damage has empty args. type='{action.TypeValue}' actionId={id.Value}");
            }

            // 优先使用具名参数（新 Schema API）
            if (action.Args != null && action.Args.Count > 0)
            {
                var args = new System.Collections.Generic.Dictionary<string, ActionArgValue>();

                if (PlanExportArgReadUtil.TryReadDouble(action.Args, "value", out var v))
                    args["damage_value"] = ActionArgValue.OfConst(v, "damage_value");
                else if (PlanExportArgReadUtil.TryReadDouble(action.Args, "Value", out var v2))
                    args["damage_value"] = ActionArgValue.OfConst(v2, "damage_value");
                else if (PlanExportArgReadUtil.TryReadDouble(action.Args, "damageValue", out var v3))
                    args["damage_value"] = ActionArgValue.OfConst(v3, "damage_value");

                if (PlanExportArgReadUtil.TryReadDouble(action.Args, "reasonParam", out var rp))
                    args["reason_param"] = ActionArgValue.OfConst(rp, "reason_param");
                else if (PlanExportArgReadUtil.TryReadDouble(action.Args, "ReasonParam", out var rp2))
                    args["reason_param"] = ActionArgValue.OfConst(rp2, "reason_param");

                if (PlanExportArgReadUtil.TryReadDouble(action.Args, "damageType", out var dt))
                    args["damage_type"] = ActionArgValue.OfConst(dt, "damage_type");

                plans = new[] { ActionCallPlan.WithArgs(id, args) };
            }
            else
            {
                plans = Array.Empty<ActionCallPlan>();
            }
            return true;
        }
    }
}
#endif
