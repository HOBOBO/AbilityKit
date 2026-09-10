#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;

namespace AbilityKit.Ability.Editor.Panels
{
    internal sealed class TriggerAuthoringTemplateBindingPastePlan
    {
        private readonly List<Change> _changes = new List<Change>();
        private readonly HashSet<int> _changedTriggerIds = new HashSet<int>();

        public List<string> Errors { get; } = new List<string>();
        public int ChangeCount => _changes.Count;
        public int ChangedTriggerCount => _changedTriggerIds.Count;
        public bool CanApply => _changes.Count > 0;

        public static TriggerAuthoringTemplateBindingPastePlan Create(
            string tsv,
            IReadOnlyList<TriggerDefinitionData> triggers,
            TriggerAuthoringTemplateData template)
        {
            var plan = new TriggerAuthoringTemplateBindingPastePlan();
            if (template == null || string.IsNullOrWhiteSpace(template.TemplateId))
            {
                plan.Errors.Add("当前函数库定义无效。 ");
                return plan;
            }
            if (!TriggerAuthoringTemplateBindingTsvCodec.TryParseTable(tsv, out var rows, out var parseError))
            {
                plan.Errors.Add(parseError);
                return plan;
            }
            if (rows.Count < 2)
            {
                plan.Errors.Add("TSV 只有表头，没有数据行。 ");
                return plan;
            }

            var columns = TriggerAuthoringTemplateBindingPasteSchema.BuildColumns(
                rows[0], template.Parameters, plan.Errors, out var hasTriggerId);
            if (!hasTriggerId) return plan;
            var targets = TriggerAuthoringTemplateBindingPasteSchema.BuildTargetMap(
                triggers, template.TemplateId, plan.Errors);
            var seenIds = new HashSet<int>();
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
                plan.ReadRow(rows[rowIndex], rowIndex + 1, columns, targets, seenIds);
            return plan;
        }

        public void Apply()
        {
            for (var i = 0; i < _changes.Count; i++) _changes[i].Apply();
        }

        public string BuildPreview(int maxIssues = 10)
        {
            var builder = new StringBuilder();
            builder.Append("将修改 ").Append(ChangedTriggerCount).Append(" 个触发器，共 ")
                .Append(ChangeCount).Append(" 个单元格。 ");
            if (Errors.Count == 0)
            {
                builder.Append("\n\n所有数据均已通过格式、类型、来源与 TriggerId 校验。 ");
                return builder.ToString();
            }
            builder.Append("\n\n发现 ").Append(Errors.Count)
                .Append(" 个问题，问题单元格不会写入：\n");
            var count = Math.Min(maxIssues, Errors.Count);
            for (var i = 0; i < count; i++) builder.Append("\n- ").Append(Errors[i]);
            if (Errors.Count > count) builder.Append("\n- 另有 ").Append(Errors.Count - count).Append(" 个问题");
            return builder.ToString();
        }

        private void ReadRow(
            IReadOnlyList<string> cells,
            int displayRow,
            IReadOnlyList<TriggerAuthoringTemplateBindingPasteColumn> columns,
            IReadOnlyDictionary<int, TriggerDefinitionData> targets,
            ISet<int> seenIds)
        {
            var idColumn = FindColumn(columns, TriggerAuthoringTemplateBindingPasteColumnKind.TriggerId);
            var idText = ReadCell(cells, idColumn.Index).Trim();
            if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                Errors.Add("第 " + displayRow + " 行的 TriggerId 不是有效整数。 ");
                return;
            }
            if (!seenIds.Add(id))
            {
                Errors.Add("第 " + displayRow + " 行重复使用 TriggerId " + id + "。 ");
                return;
            }
            if (!targets.TryGetValue(id, out var trigger) || trigger == null)
            {
                Errors.Add("第 " + displayRow + " 行的 TriggerId " + id + " 不属于当前函数库。 ");
                return;
            }

            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                if (column.Kind == TriggerAuthoringTemplateBindingPasteColumnKind.TriggerId) continue;
                var text = ReadCell(cells, column.Index);
                if (string.IsNullOrEmpty(text)) continue;
                if (column.Kind == TriggerAuthoringTemplateBindingPasteColumnKind.Name)
                {
                    if (!string.Equals(trigger.Name, text, StringComparison.Ordinal))
                        AddChange(trigger, () => trigger.Name = text);
                    continue;
                }
                if (column.Kind == TriggerAuthoringTemplateBindingPasteColumnKind.Group)
                {
                    if (!string.Equals(trigger.GroupPath, text, StringComparison.Ordinal))
                        AddChange(trigger, () => trigger.GroupPath = text);
                    continue;
                }
                ReadBindingCell(trigger, column.Parameter, text, displayRow);
            }
        }

        private void ReadBindingCell(
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateParameterData parameter,
            string text,
            int displayRow)
        {
            var bindings = trigger.Template.Bindings;
            var current = FindBinding(bindings, parameter.Name);
            if (string.Equals(text.Trim(), TriggerAuthoringTemplateBindingTsvCodec.DefaultMarker,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (parameter.Required && !parameter.HasDefault)
                {
                    Errors.Add(CellError(displayRow, parameter.Name, "必填参数没有默认值，不能移除绑定。 "));
                    return;
                }
                if (current != null) AddChange(trigger, () => trigger.Template.Bindings?.Remove(current));
                return;
            }
            if (!TriggerAuthoringTemplateValueTextCodec.TryParse(text, parameter, out var value, out var error))
            {
                Errors.Add(CellError(displayRow, parameter.Name, error));
                return;
            }
            if (current != null && ValuesEqual(current.Value, value)) return;
            AddChange(trigger, () =>
            {
                var targetBindings = trigger.Template.Bindings ??
                                     (trigger.Template.Bindings = new List<TriggerArgumentData>());
                if (current == null)
                    targetBindings.Add(new TriggerArgumentData { Name = parameter.Name, Value = value });
                else current.Value = value;
            });
        }

        private void AddChange(TriggerDefinitionData trigger, Action apply)
        {
            _changes.Add(new Change(apply));
            _changedTriggerIds.Add(trigger.Id);
        }

        private static TriggerArgumentData FindBinding(IReadOnlyList<TriggerArgumentData> bindings, string name)
        {
            if (bindings == null) return null;
            for (var i = 0; i < bindings.Count; i++)
                if (bindings[i] != null && string.Equals(bindings[i].Name, name, StringComparison.Ordinal))
                    return bindings[i];
            return null;
        }

        private static bool ValuesEqual(TriggerValueRefData left, TriggerValueRefData right)
        {
            if (left == null || right == null || left.Source != right.Source || left.Type != right.Type) return false;
            if (left.Source == TriggerValueSource.Expression)
                return string.Equals(left.Expression, right.Expression, StringComparison.Ordinal);
            if (left.Source != TriggerValueSource.Constant)
                return string.Equals(left.Path, right.Path, StringComparison.Ordinal);
            return string.Equals(
                TriggerAuthoringTemplateValueTextCodec.Format(left),
                TriggerAuthoringTemplateValueTextCodec.Format(right),
                StringComparison.Ordinal);
        }

        private static TriggerAuthoringTemplateBindingPasteColumn FindColumn(
            IReadOnlyList<TriggerAuthoringTemplateBindingPasteColumn> columns,
            TriggerAuthoringTemplateBindingPasteColumnKind kind)
        {
            for (var i = 0; i < columns.Count; i++) if (columns[i].Kind == kind) return columns[i];
            return null;
        }

        private static string ReadCell(IReadOnlyList<string> cells, int index)
        {
            return cells != null && index >= 0 && index < cells.Count ? cells[index] ?? string.Empty : string.Empty;
        }

        private static string CellError(int row, string parameter, string error)
        {
            return "第 " + row + " 行参数“" + parameter + "”：" + error;
        }

        private sealed class Change
        {
            private readonly Action _apply;
            public Change(Action apply) { _apply = apply; }
            public void Apply() { _apply(); }
        }
    }
}
#endif
