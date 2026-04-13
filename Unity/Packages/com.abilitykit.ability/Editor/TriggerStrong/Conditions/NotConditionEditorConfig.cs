using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerConditionType(TriggerConditionTypes.Not, "取反(NOT)", "条件/复合", 20)]
    public sealed class NotConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => TriggerConditionTypes.Not;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子条件")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddChild), DraggableItems = false)]
        public List<ConditionEditorConfigBase> Items = new List<ConditionEditorConfigBase>();

        private void AddChild()
        {
            if (Items == null) Items = new List<ConditionEditorConfigBase>();
            if (Items.Count > 0) return;
            StrongConfigTypeSelector.ShowAddConditionSelector(Items, null);
        }

        public override ConditionConfigBase ToRuntimeConfig()
        {
            return new NotConditionConfig
            {
                Item = Items != null && Items.Count > 0 && Items[0] != null ? Items[0].ToRuntimeConfig() : null
            };
        }
    }
}
