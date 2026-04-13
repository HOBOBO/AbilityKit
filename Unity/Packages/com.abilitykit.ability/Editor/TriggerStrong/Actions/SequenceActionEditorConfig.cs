using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.Seq, "顺序组", "行为/流程", 0)]
    public sealed class SequenceActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.Seq;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子行为")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddChild))]
        public List<ActionEditorConfigBase> Items = new List<ActionEditorConfigBase>();

        private void AddChild()
        {
            StrongConfigTypeSelector.ShowAddActionSelector(Items, null);
        }

        public override ActionConfigBase ToRuntimeConfig()
        {
            var list = new List<ActionConfigBase>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var a = Items[i];
                    if (a == null) continue;
                    var rt = a.ToRuntimeConfig();
                    if (rt != null) list.Add(rt);
                }
            }
            return new SequenceActionConfig
            {
                Items = list
            };
        }
    }
}
