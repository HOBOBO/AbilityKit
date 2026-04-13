#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Editor.Utilities
{
    [TriggerPlanExportHandler(order: 0)]
    internal sealed class DebugLogPlanExportHandler : ITriggerPlanExportActionHandler
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
            if (!string.Equals(action.TypeValue, TriggerActionTypes.DebugLog, StringComparison.Ordinal)) return false;
            if (actionIdResolver == null) return false;

            var id = actionIdResolver(action.TypeValue);
            if (id.Value == 0) return false;

            var msg = string.Empty;
            var dump = false;

            if (action.Args != null)
            {
                if (!PlanExportArgReadUtil.TryReadString(action.Args, "message", out msg)) msg = string.Empty;
                PlanExportArgReadUtil.TryReadBool(action.Args, "dump_args", out dump);
            }

            var strId = StableStringId.Get("str:" + (msg ?? string.Empty));
            if (stringTable != null)
            {
                if (!stringTable.TryGetValue(strId, out var existing))
                {
                    stringTable[strId] = msg ?? string.Empty;
                }
                else if (!string.Equals(existing, msg ?? string.Empty, StringComparison.Ordinal))
                {
                    stringTable[strId] = msg ?? string.Empty;
                }
            }

            // 具名参数格式
            var args = new Dictionary<string, ActionArgValue>
            {
                ["msg_id"] = ActionArgValue.OfConst(strId, "msg_id"),
                ["dump"] = ActionArgValue.OfConst(dump ? 1d : 0d, "dump")
            };

            plans = new[] { ActionCallPlan.WithArgs(id, args) };
            return true;
        }
    }
}
#endif
