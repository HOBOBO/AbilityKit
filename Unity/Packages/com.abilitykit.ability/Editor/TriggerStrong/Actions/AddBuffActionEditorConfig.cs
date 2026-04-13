using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.AddBuff, "添加Buff", "行为/Buff", 0)]
    public sealed class AddBuffActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.AddBuff;

        [LabelText("BuffId列表")]
        [ListDrawerSettings(Expanded = true)]
        public List<int> BuffIds = new List<int>();

        protected override string GetTitleSuffix()
        {
            if (BuffIds == null || BuffIds.Count == 0) return null;
            if (BuffIds.Count == 1) return BuffIds[0].ToString();
            return BuffIds.Count.ToString();
        }

        public override ActionConfigBase ToRuntimeConfig()
        {
            return new AddBuffActionConfig
            {
                BuffIds = BuffIds != null ? new List<int>(BuffIds) : new List<int>()
            };
        }
    }
}
