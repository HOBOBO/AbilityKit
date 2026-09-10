#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;

namespace AbilityKit.Ability.Editor.Panels
{
    internal enum TriggerAuthoringTriggerTableColumn
    {
        Enabled,
        Id,
        Name,
        Entry,
        Event,
        Group,
        Priority,
        Template,
        Diagnostics
    }

    internal sealed class TriggerAuthoringTriggerTableRow
    {
        public int RowId;
        public int Index;
        public TriggerDefinitionData Trigger;
        public TriggerDefinitionData EffectiveTrigger;
        public TriggerAuthoringTriggerIndex.DiagnosticSummary Diagnostics;

        public bool UsesTemplate =>
            Trigger?.Template != null &&
            EffectiveTrigger != null &&
            !ReferenceEquals(Trigger, EffectiveTrigger);
    }

    internal static class TriggerAuthoringTriggerTableModel
    {
        public static List<TriggerAuthoringTriggerTableRow> BuildRows(
            IReadOnlyList<TriggerAuthoringTriggerIndex.Entry> entries,
            TriggerAuthoringTriggerTableColumn sortColumn,
            bool ascending)
        {
            var rows = new List<TriggerAuthoringTriggerTableRow>();
            if (entries == null) return rows;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                rows.Add(new TriggerAuthoringTriggerTableRow
                {
                    RowId = entry.Index + 1,
                    Index = entry.Index,
                    Trigger = entry.Trigger,
                    EffectiveTrigger = entry.EffectiveTrigger,
                    Diagnostics = entry.Diagnostics
                });
            }

            rows.Sort((left, right) => Compare(left, right, sortColumn, ascending));
            return rows;
        }

        public static List<int> CollectIndices(
            IReadOnlyList<TriggerAuthoringTriggerTableRow> rows,
            IList<int> selectedRowIds)
        {
            var result = new List<int>();
            if (rows == null || selectedRowIds == null) return result;
            var selected = new HashSet<int>(selectedRowIds);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && selected.Contains(row.RowId)) result.Add(row.Index);
            }
            result.Sort();
            return result;
        }

        private static int Compare(
            TriggerAuthoringTriggerTableRow left,
            TriggerAuthoringTriggerTableRow right,
            TriggerAuthoringTriggerTableColumn column,
            bool ascending)
        {
            var result = CompareColumn(left, right, column);
            if (!ascending) result = -result;
            return result != 0 ? result : left.Index.CompareTo(right.Index);
        }

        private static int CompareColumn(
            TriggerAuthoringTriggerTableRow left,
            TriggerAuthoringTriggerTableRow right,
            TriggerAuthoringTriggerTableColumn column)
        {
            var leftTrigger = left?.EffectiveTrigger;
            var rightTrigger = right?.EffectiveTrigger;
            switch (column)
            {
                case TriggerAuthoringTriggerTableColumn.Enabled:
                    return CompareBool(leftTrigger?.Enabled ?? false, rightTrigger?.Enabled ?? false);
                case TriggerAuthoringTriggerTableColumn.Id:
                    return (leftTrigger?.Id ?? 0).CompareTo(rightTrigger?.Id ?? 0);
                case TriggerAuthoringTriggerTableColumn.Name:
                    return CompareText(leftTrigger?.Name, rightTrigger?.Name);
                case TriggerAuthoringTriggerTableColumn.Entry:
                    return (leftTrigger?.EntryMode ?? TriggerEntryMode.Event)
                        .CompareTo(rightTrigger?.EntryMode ?? TriggerEntryMode.Event);
                case TriggerAuthoringTriggerTableColumn.Event:
                    return CompareText(leftTrigger?.Event, rightTrigger?.Event);
                case TriggerAuthoringTriggerTableColumn.Group:
                    return CompareText(leftTrigger?.GroupPath, rightTrigger?.GroupPath);
                case TriggerAuthoringTriggerTableColumn.Priority:
                    return (leftTrigger?.Priority ?? 0).CompareTo(rightTrigger?.Priority ?? 0);
                case TriggerAuthoringTriggerTableColumn.Template:
                    return CompareText(
                        left?.Trigger?.Template?.TemplateId,
                        right?.Trigger?.Template?.TemplateId);
                case TriggerAuthoringTriggerTableColumn.Diagnostics:
                    var leftWeight = left.Diagnostics.Errors * 1000 + left.Diagnostics.Warnings;
                    var rightWeight = right.Diagnostics.Errors * 1000 + right.Diagnostics.Warnings;
                    return leftWeight.CompareTo(rightWeight);
                default:
                    return left.Index.CompareTo(right.Index);
            }
        }

        private static int CompareBool(bool left, bool right)
        {
            return left == right ? 0 : left ? 1 : -1;
        }

        private static int CompareText(string left, string right)
        {
            return string.Compare(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
