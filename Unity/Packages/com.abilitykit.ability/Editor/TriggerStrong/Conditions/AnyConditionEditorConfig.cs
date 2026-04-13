using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerConditionType(TriggerConditionTypes.Any, "任意满足(OR)", "条件/复合", 10)]
    public sealed class AnyConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => TriggerConditionTypes.Any;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子条件")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddChild))]
        public List<ConditionEditorConfigBase> Items = new List<ConditionEditorConfigBase>();

        private void AddChild()
        {
            StrongConfigTypeSelector.ShowAddConditionSelector(Items, null);
        }

        public override ConditionConfigBase ToRuntimeConfig()
        {
            var list = new List<ConditionConfigBase>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var c = Items[i];
                    if (c == null) continue;
                    var rt = c.ToRuntimeConfig();
                    if (rt != null) list.Add(rt);
                }
            }
            return new AnyConditionConfig
            {
                Items = list
            };
        }
    }
}
