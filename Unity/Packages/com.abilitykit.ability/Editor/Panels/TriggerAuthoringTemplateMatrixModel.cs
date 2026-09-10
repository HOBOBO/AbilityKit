#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;

namespace AbilityKit.Ability.Editor.Panels
{
    internal sealed class TriggerAuthoringUsedTemplate
    {
        public string Id;
        public string DisplayName;
        public TriggerAuthoringTemplateAsset Asset;
        public int InstanceCount;
    }

    internal sealed class TriggerAuthoringTemplateMatrixRow
    {
        public int RowId;
        public int Index;
        public TriggerDefinitionData Trigger;
        public TriggerAuthoringTriggerIndex.DiagnosticSummary Diagnostics;

        public TriggerArgumentData FindBinding(string parameterName)
        {
            var bindings = Trigger?.Template?.Bindings;
            if (bindings == null) return null;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding != null && string.Equals(binding.Name, parameterName, StringComparison.Ordinal))
                    return binding;
            }
            return null;
        }
    }

    internal static class TriggerAuthoringTemplateMatrixModel
    {
        public static List<TriggerAuthoringTemplateParameterData> BuildParameters(
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            var result = new List<TriggerAuthoringTemplateParameterData>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (parameters == null) return result;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name) || !names.Add(parameter.Name))
                    continue;
                result.Add(parameter);
            }
            return result;
        }

        public static List<TriggerAuthoringUsedTemplate> BuildUsedTemplates(
            IReadOnlyList<TriggerDefinitionData> triggers,
            TriggerTemplateDescriptorCatalog catalog)
        {
            var result = new List<TriggerAuthoringUsedTemplate>();
            var byId = new Dictionary<string, TriggerAuthoringUsedTemplate>(StringComparer.Ordinal);
            if (triggers == null || catalog == null) return result;
            for (var i = 0; i < triggers.Count; i++)
            {
                var id = triggers[i]?.Template?.TemplateId;
                if (string.IsNullOrWhiteSpace(id) || !catalog.TryGet(id, out var asset) || asset?.Template == null)
                    continue;
                if (!byId.TryGetValue(id, out var item))
                {
                    item = new TriggerAuthoringUsedTemplate
                    {
                        Id = id,
                        DisplayName = string.IsNullOrWhiteSpace(asset.Template.DisplayName)
                            ? id
                            : asset.Template.DisplayName,
                        Asset = asset
                    };
                    byId.Add(id, item);
                    result.Add(item);
                }
                item.InstanceCount++;
            }
            result.Sort((left, right) => string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static List<TriggerAuthoringTemplateMatrixRow> BuildRows(
            IReadOnlyList<TriggerAuthoringTriggerIndex.Entry> entries,
            string templateId,
            string search,
            int sortColumn,
            bool ascending,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            var rows = new List<TriggerAuthoringTemplateMatrixRow>();
            if (entries == null || string.IsNullOrWhiteSpace(templateId)) return rows;
            var filter = (search ?? string.Empty).Trim();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var trigger = entry.Trigger;
                if (!string.Equals(trigger?.Template?.TemplateId, templateId, StringComparison.Ordinal)) continue;
                var row = new TriggerAuthoringTemplateMatrixRow
                {
                    RowId = entry.Index + 1,
                    Index = entry.Index,
                    Trigger = trigger,
                    Diagnostics = entry.Diagnostics
                };
                if (Matches(row, filter, parameters)) rows.Add(row);
            }
            rows.Sort((left, right) => Compare(left, right, sortColumn, ascending, parameters));
            return rows;
        }

        public static List<int> CollectIndices(
            IReadOnlyList<TriggerAuthoringTemplateMatrixRow> rows,
            IList<int> selectedRowIds)
        {
            var result = new List<int>();
            if (rows == null || selectedRowIds == null) return result;
            var selected = new HashSet<int>(selectedRowIds);
            for (var i = 0; i < rows.Count; i++)
                if (rows[i] != null && selected.Contains(rows[i].RowId)) result.Add(rows[i].Index);
            result.Sort();
            return result;
        }

        private static bool Matches(
            TriggerAuthoringTemplateMatrixRow row,
            string filter,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            if (filter.Length == 0) return true;
            var trigger = row.Trigger;
            if (Contains(trigger.Id.ToString(), filter) || Contains(trigger.Name, filter) ||
                Contains(trigger.GroupPath, filter)) return true;
            if (parameters == null) return false;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null) continue;
                var binding = row.FindBinding(parameter.Name);
                if (Contains(parameter.Name, filter) ||
                    Contains(TriggerAuthoringTemplateValueTextCodec.Format(binding?.Value), filter))
                    return true;
            }
            return false;
        }

        private static int Compare(
            TriggerAuthoringTemplateMatrixRow left,
            TriggerAuthoringTemplateMatrixRow right,
            int column,
            bool ascending,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            int result;
            switch (column)
            {
                case 0: result = left.Trigger.Id.CompareTo(right.Trigger.Id); break;
                case 1: result = CompareText(left.Trigger.Name, right.Trigger.Name); break;
                case 2: result = CompareText(left.Trigger.GroupPath, right.Trigger.GroupPath); break;
                case 3:
                    result = (left.Diagnostics.Errors * 1000 + left.Diagnostics.Warnings)
                        .CompareTo(right.Diagnostics.Errors * 1000 + right.Diagnostics.Warnings);
                    break;
                default:
                    var parameterIndex = column - 4;
                    var parameterName = parameters != null && parameterIndex >= 0 && parameterIndex < parameters.Count
                        ? parameters[parameterIndex]?.Name
                        : null;
                    result = CompareText(
                        TriggerAuthoringTemplateValueTextCodec.Format(left.FindBinding(parameterName)?.Value),
                        TriggerAuthoringTemplateValueTextCodec.Format(right.FindBinding(parameterName)?.Value));
                    break;
            }
            if (!ascending) result = -result;
            return result != 0 ? result : left.Index.CompareTo(right.Index);
        }

        private static int CompareText(string left, string right)
        {
            return string.Compare(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string text, string filter)
        {
            return !string.IsNullOrEmpty(text) &&
                   text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
