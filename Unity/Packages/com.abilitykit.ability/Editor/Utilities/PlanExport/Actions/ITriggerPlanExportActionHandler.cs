#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal interface ITriggerPlanExportActionHandler
    {
        bool TryConvertActionNode(ActionEditorConfigBase strongNode, out JsonActionEditorConfig jsonNode);

        bool TryCompileAction(JsonActionEditorConfig action,
            Dictionary<int, string> stringTable,
            Func<string, int> payloadFieldIdResolver,
            Func<string, ActionId> actionIdResolver,
            out ActionCallPlan[] plans);
    }
}
#endif
