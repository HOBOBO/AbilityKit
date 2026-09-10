#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Panels
{
    internal static class TriggerAuthoringTemplateMatrixLayout
    {
        public static MultiColumnHeaderState CreateHeaderState(
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            var columns = new List<MultiColumnHeaderState.Column>
            {
                Column("TriggerId", "稳定匹配键；粘贴不会按行号覆盖", 82f, 68f),
                Column("名称", "调用实例名称", 170f, 100f),
                Column("业务分组", "编辑器业务分组", 150f, 90f),
                Column("诊断", "当前实例的错误与警告", 72f, 62f)
            };
            if (parameters != null)
                for (var i = 0; i < parameters.Count; i++)
                {
                    var parameter = parameters[i];
                    if (parameter == null) continue;
                    var tooltip = TriggerAuthoringEditorLabels.ValueType(parameter.Type) +
                                  (parameter.Required && !parameter.HasDefault ? " · 必填" : " · 可选");
                    if (!string.IsNullOrWhiteSpace(parameter.Description)) tooltip += "\n" + parameter.Description;
                    columns.Add(Column(parameter.Name, tooltip, 150f, 92f));
                }
            return new MultiColumnHeaderState(columns.ToArray());
        }

        public static int ComputeHeaderSignature(
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            unchecked
            {
                var hash = 17;
                var count = parameters?.Count ?? 0;
                for (var i = 0; i < count; i++)
                {
                    hash = hash * 31 + (parameters[i]?.Name?.GetHashCode() ?? 0);
                    hash = hash * 31 + (int)(parameters[i]?.Type ?? TriggerValueType.None);
                }
                return hash;
            }
        }

        public static int ComputeContentSignature(
            IReadOnlyList<TriggerAuthoringTriggerIndex.Entry> entries,
            string templateId,
            string search)
        {
            unchecked
            {
                var hash = (templateId?.GetHashCode() ?? 0) * 31 + (search?.GetHashCode() ?? 0);
                var count = entries?.Count ?? 0;
                for (var i = 0; i < count; i++)
                {
                    var trigger = entries[i].Trigger;
                    hash = hash * 31 + entries[i].Index;
                    hash = hash * 31 + (trigger?.GetHashCode() ?? 0);
                    hash = hash * 31 + (trigger?.Template?.TemplateId?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }

        public static bool Contains(IReadOnlyList<TriggerAuthoringUsedTemplate> items, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (var i = 0; i < items.Count; i++)
                if (string.Equals(items[i].Id, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static MultiColumnHeaderState.Column Column(
            string label, string tooltip, float width, float minimumWidth)
        {
            return new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent(label, tooltip),
                width = width,
                minWidth = minimumWidth,
                autoResize = false,
                allowToggleVisibility = true,
                canSort = true
            };
        }
    }
}
#endif
