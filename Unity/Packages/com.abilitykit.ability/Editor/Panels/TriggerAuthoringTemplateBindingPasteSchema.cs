#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Panels
{
    internal enum TriggerAuthoringTemplateBindingPasteColumnKind
    {
        TriggerId,
        Name,
        Group,
        Parameter
    }

    internal sealed class TriggerAuthoringTemplateBindingPasteColumn
    {
        public TriggerAuthoringTemplateBindingPasteColumn(
            int index,
            TriggerAuthoringTemplateBindingPasteColumnKind kind,
            TriggerAuthoringTemplateParameterData parameter)
        {
            Index = index;
            Kind = kind;
            Parameter = parameter;
        }

        public int Index { get; }
        public TriggerAuthoringTemplateBindingPasteColumnKind Kind { get; }
        public TriggerAuthoringTemplateParameterData Parameter { get; }
    }

    internal static class TriggerAuthoringTemplateBindingPasteSchema
    {
        public static List<TriggerAuthoringTemplateBindingPasteColumn> BuildColumns(
            IReadOnlyList<string> headers,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters,
            ICollection<string> errors,
            out bool hasTriggerId)
        {
            var result = new List<TriggerAuthoringTemplateBindingPasteColumn>();
            var parameterMap = BuildParameterMap(parameters);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            hasTriggerId = false;
            for (var i = 0; i < headers.Count; i++)
            {
                var name = (headers[i] ?? string.Empty).Trim();
                if (name.Length == 0) continue;
                if (!seen.Add(name))
                {
                    errors.Add("表头“" + name + "”重复。 ");
                    continue;
                }
                if (name == "TriggerId")
                {
                    hasTriggerId = true;
                    result.Add(Column(i, TriggerAuthoringTemplateBindingPasteColumnKind.TriggerId));
                }
                else if (name == "名称")
                    result.Add(Column(i, TriggerAuthoringTemplateBindingPasteColumnKind.Name));
                else if (name == "业务分组")
                    result.Add(Column(i, TriggerAuthoringTemplateBindingPasteColumnKind.Group));
                else if (parameterMap.TryGetValue(name, out var parameter))
                    result.Add(new TriggerAuthoringTemplateBindingPasteColumn(
                        i, TriggerAuthoringTemplateBindingPasteColumnKind.Parameter, parameter));
                else errors.Add("未知列“" + name + "”，该列将被忽略。 ");
            }
            if (!hasTriggerId) errors.Add("缺少必需的 TriggerId 列，无法安全匹配触发器。 ");
            return result;
        }

        public static Dictionary<int, TriggerDefinitionData> BuildTargetMap(
            IReadOnlyList<TriggerDefinitionData> triggers,
            string templateId,
            ICollection<string> errors)
        {
            var result = new Dictionary<int, TriggerDefinitionData>();
            var ambiguous = new HashSet<int>();
            if (triggers == null) return result;
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (!string.Equals(trigger?.Template?.TemplateId, templateId, StringComparison.Ordinal)) continue;
                if (result.ContainsKey(trigger.Id))
                {
                    errors.Add("当前函数库存在重复 TriggerId " + trigger.Id + "，该 ID 无法安全粘贴。 ");
                    ambiguous.Add(trigger.Id);
                }
                else if (!ambiguous.Contains(trigger.Id)) result.Add(trigger.Id, trigger);
            }
            foreach (var id in ambiguous) result.Remove(id);
            return result;
        }

        private static Dictionary<string, TriggerAuthoringTemplateParameterData> BuildParameterMap(
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            var result = new Dictionary<string, TriggerAuthoringTemplateParameterData>(StringComparer.Ordinal);
            if (parameters == null) return result;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter != null && !string.IsNullOrWhiteSpace(parameter.Name) &&
                    !result.ContainsKey(parameter.Name)) result.Add(parameter.Name, parameter);
            }
            return result;
        }

        private static TriggerAuthoringTemplateBindingPasteColumn Column(
            int index,
            TriggerAuthoringTemplateBindingPasteColumnKind kind)
        {
            return new TriggerAuthoringTemplateBindingPasteColumn(index, kind, null);
        }
    }
}
#endif
