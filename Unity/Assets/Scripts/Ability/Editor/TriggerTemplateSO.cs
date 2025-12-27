using System;
using System.Collections.Generic;
using AbilityKit.Configs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class TriggerTemplateSO : ScriptableObject
    {
        [ListDrawerSettings(Expanded = true)]
        public List<TemplateParamDef> Params = new List<TemplateParamDef>();

        [ListDrawerSettings(Expanded = true)]
        public List<TriggerTemplateConfig> Triggers = new List<TriggerTemplateConfig>();

        private void OnEnable()
        {
            AssignOwners();
        }

        private void OnValidate()
        {
            AssignOwners();
        }

        private void AssignOwners()
        {
            if (Triggers == null) return;
            for (int i = 0; i < Triggers.Count; i++)
            {
                var t = Triggers[i];
                if (t == null) continue;
                t.Owner = this;
            }
        }

        public Dictionary<string, ArgRuntimeEntry> BuildDefaultBindings()
        {
            var dict = new Dictionary<string, ArgRuntimeEntry>(StringComparer.Ordinal);
            if (Params == null) return dict;

            for (int i = 0; i < Params.Count; i++)
            {
                var p = Params[i];
                if (p == null || string.IsNullOrEmpty(p.Name)) continue;
                dict[p.Name] = p.DefaultValue != null ? p.DefaultValue.Clone() : null;
            }

            return dict;
        }
    }

    [Serializable]
    public sealed class TemplateParamDef
    {
        public string Name;

        public ArgRuntimeEntry DefaultValue = new ArgRuntimeEntry();
    }

    [Serializable]
    public sealed class TriggerTemplateConfig
    {
        [NonSerialized]
        internal TriggerTemplateSO Owner;

        public bool Enabled = true;
        public string EventId;

        [TextArea]
        public string Note;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("条件列表")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddConditionStrong))]
        public List<ConditionEditorConfigBase> ConditionsStrong = new List<ConditionEditorConfigBase>();

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("行为列表")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddActionStrong))]
        public List<ActionEditorConfigBase> ActionsStrong = new List<ActionEditorConfigBase>();

        private void AddConditionStrong()
        {
            StrongConfigTypeSelector.ShowAddConditionSelector(ConditionsStrong, Owner);
        }

        private void AddActionStrong()
        {
            StrongConfigTypeSelector.ShowAddActionSelector(ActionsStrong, Owner);
        }
    }
}
