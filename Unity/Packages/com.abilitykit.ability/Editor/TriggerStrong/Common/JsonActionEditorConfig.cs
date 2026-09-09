using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    public sealed class JsonActionEditorConfig : ActionEditorConfigBase
    {
        public string TypeValue;

        [ShowInInspector]
        public override string Type => TypeValue;

        [LabelText("参数")]
        [OdinSerialize]
        public Dictionary<string, object> Args;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子行为")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle")]
        public List<ActionEditorConfigBase> Items;

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

            return new JsonActionConfig
            {
                TypeValue = TypeValue,
                Args = Args != null ? new Dictionary<string, object>(Args, StringComparer.Ordinal) : null,
                Items = list
            };
        }

        public static JsonActionEditorConfig FromDto(ActionDTO dto)
        {
            if (dto == null) return null;

            var node = new JsonActionEditorConfig
            {
                TypeValue = dto.Type,
                Args = dto.Args != null ? new Dictionary<string, object>(dto.Args, StringComparer.Ordinal) : null
            };

            if (dto.Items != null && dto.Items.Count > 0)
            {
                node.Items = new List<ActionEditorConfigBase>(dto.Items.Count);
                for (int i = 0; i < dto.Items.Count; i++)
                {
                    var child = FromDto(dto.Items[i]);
                    if (child != null) node.Items.Add(child);
                }
            }

            return node;
        }

        protected override string GetTitleSuffix()
        {
            var hasArgs = Args != null && Args.Count > 0;
            var hasItems = Items != null && Items.Count > 0;
            if (hasArgs && hasItems) return $"参数={Args.Count}，子行为={Items.Count}";
            if (hasArgs) return $"参数={Args.Count}";
            if (hasItems) return $"子行为={Items.Count}";
            return null;
        }
    }
}
