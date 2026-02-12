#if UNITY_EDITOR
using System;
using AbilityKit.Ability.Editor;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal interface ITriggerPlanExportConditionHandler
    {
        bool TryConvertConditionNode(ConditionEditorConfigBase strongNode, out JsonConditionEditorConfig jsonNode);

        bool TryCompileCondition(JsonConditionEditorConfig condition,
            Func<string, int> payloadFieldIdResolver,
            out PredicateExprPlan plan);
    }
}
#endif
