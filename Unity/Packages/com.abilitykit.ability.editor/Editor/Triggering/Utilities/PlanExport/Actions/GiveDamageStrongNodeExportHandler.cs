#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Editor.Utilities
{
    [TriggerPlanExportHandler(order: -10)]
    internal sealed class GiveDamageStrongNodeExportHandler : ITriggerPlanExportActionHandler
    {
        public bool TryConvertActionNode(ActionEditorConfigBase strongNode, out JsonActionEditorConfig jsonNode)
        {
            jsonNode = null;
            if (strongNode is GiveDamageActionEditorConfig gd)
            {
                ExportLog.Warning($"give_damage strong node exported. value={gd.Value:0.###} reasonParam={gd.ReasonParam}");
                jsonNode = new JsonActionEditorConfig
                {
                    TypeValue = TriggerActionTypes.GiveDamage,
                    Args = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["value"] = gd.Value,
                        ["reasonParam"] = gd.ReasonParam,
                    },
                };
                return true;
            }
            return false;
        }

        public bool TryCompileAction(JsonActionEditorConfig action,
            Dictionary<int, string> stringTable,
            Func<string, int> payloadFieldIdResolver,
            Func<string, ActionId> actionIdResolver,
            out ActionCallPlan[] plans)
        {
            plans = null;
            return false;
        }
    }
}
#endif
