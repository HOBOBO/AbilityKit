using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    public sealed class JsonActionEditorConfig : ActionEditorConfigBase
    {
        public string TypeValue;

        [ShowInInspector]
        public override string Type => TypeValue;

        [LabelText("Args")]
        public Dictionary<string, object> Args;

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("Items")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle")]
        public List<ActionEditorConfigBase> Items;

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            var list = new List<ActionRuntimeConfigBase>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var a = Items[i];
                    if (a == null) continue;
                    var rt = a.ToRuntimeStrong();
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
            if (hasArgs && hasItems) return $"args={Args.Count}, items={Items.Count}";
            if (hasArgs) return $"args={Args.Count}";
            if (hasItems) return $"items={Items.Count}";
            return null;
        }
    }
}
