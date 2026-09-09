using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    public sealed class JsonConditionEditorConfig : ConditionEditorConfigBase
    {
        public string TypeValue;

        [ShowInInspector]
        public override string Type => TypeValue;

        [LabelText("参数")]
        [OdinSerialize]
        public Dictionary<string, object> Args;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子条件")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle")]
        public List<ConditionEditorConfigBase> Items;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("条件")]
        public ConditionEditorConfigBase Item;

        public override ConditionConfigBase ToRuntimeConfig()
        {
            var items = new List<ConditionConfigBase>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var c = Items[i];
                    if (c == null) continue;
                    var rt = c.ToRuntimeConfig();
                    if (rt != null) items.Add(rt);
                }
            }

            return new JsonConditionConfig
            {
                TypeValue = TypeValue,
                Args = Args != null ? new Dictionary<string, object>(Args, StringComparer.Ordinal) : null,
                Items = items,
                Item = Item != null ? Item.ToRuntimeConfig() : null
            };
        }

        public static JsonConditionEditorConfig FromDto(ConditionDTO dto)
        {
            if (dto == null) return null;

            var node = new JsonConditionEditorConfig
            {
                TypeValue = dto.Type,
                Args = dto.Args != null ? new Dictionary<string, object>(dto.Args, StringComparer.Ordinal) : null
            };

            if (dto.Items != null && dto.Items.Count > 0)
            {
                node.Items = new List<ConditionEditorConfigBase>(dto.Items.Count);
                for (int i = 0; i < dto.Items.Count; i++)
                {
                    var child = FromDto(dto.Items[i]);
                    if (child != null) node.Items.Add(child);
                }
            }

            if (dto.Item != null)
            {
                node.Item = FromDto(dto.Item);
            }

            return node;
        }

        protected override string GetTitleSuffix()
        {
            if (string.Equals(TypeValue, TriggerConditionTypes.All, StringComparison.Ordinal) || string.Equals(TypeValue, TriggerConditionTypes.Any, StringComparison.Ordinal))
            {
                return Items != null ? $"子条件={Items.Count}" : "子条件=0";
            }

            if (string.Equals(TypeValue, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                return Item != null ? "包含条件" : "条件为空";
            }

            if (Args != null && Args.Count > 0) return $"参数={Args.Count}";
            return null;
        }
    }
}
