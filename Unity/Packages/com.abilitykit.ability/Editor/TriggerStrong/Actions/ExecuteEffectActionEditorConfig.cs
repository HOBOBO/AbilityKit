using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.EffectExecute, "执行效果", "行为/效果", 0)]
    public sealed class ExecuteEffectActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.EffectExecute;

        [LabelText("效果Id")]
        public int EffectId;

        protected override string GetTitleSuffix()
        {
            return EffectId > 0 ? EffectId.ToString() : null;
        }

        public override ActionConfigBase ToRuntimeConfig()
        {
            return new ExecuteEffectActionConfig
            {
                EffectId = EffectId
            };
        }
    }
}
