#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Panels
{
    internal static class TriggerAuthoringTemplateBindingTsvCodec
    {
        public const string DefaultMarker = "<default>";

        public static string Build(
            IReadOnlyList<TriggerAuthoringTemplateMatrixRow> rows,
            TriggerAuthoringTemplateData template)
        {
            var parameters = TriggerAuthoringTemplateMatrixModel.BuildParameters(template?.Parameters);
            var builder = new StringBuilder();
            AppendCell(builder, "TriggerId");
            AppendCell(builder, "名称");
            AppendCell(builder, "业务分组");
            for (var i = 0; i < parameters.Count; i++)
                AppendCell(builder, parameters[i].Name);
            TrimLastTab(builder);
            builder.AppendLine();

            if (rows == null) return builder.ToString();
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row?.Trigger == null) continue;
                AppendCell(builder, row.Trigger.Id.ToString(CultureInfo.InvariantCulture));
                AppendCell(builder, row.Trigger.Name ?? string.Empty);
                AppendCell(builder, row.Trigger.GroupPath ?? string.Empty);
                for (var parameterIndex = 0; parameterIndex < parameters.Count; parameterIndex++)
                {
                    var parameter = parameters[parameterIndex];
                    var binding = row.FindBinding(parameter.Name);
                    var cell = binding != null
                        ? TriggerAuthoringTemplateValueTextCodec.Format(binding.Value)
                        : parameter.HasDefault || !parameter.Required ? DefaultMarker : string.Empty;
                    AppendCell(builder, cell);
                }
                TrimLastTab(builder);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        public static bool TryParseTable(string text, out List<List<string>> rows, out string error)
        {
            rows = new List<List<string>>();
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "剪贴板中没有可粘贴的表格内容。";
                return false;
            }

            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else quoted = !quoted;
                    continue;
                }
                if (!quoted && ch == '\t')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    continue;
                }
                if (!quoted && (ch == '\r' || ch == '\n'))
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    if (!IsEmpty(row)) rows.Add(row);
                    row = new List<string>();
                    continue;
                }
                cell.Append(ch);
            }
            if (quoted)
            {
                error = "TSV 中存在未闭合的引号。";
                rows.Clear();
                return false;
            }
            row.Add(cell.ToString());
            if (!IsEmpty(row)) rows.Add(row);
            if (rows.Count > 0) return true;
            error = "剪贴板中没有可粘贴的数据行。";
            return false;
        }

        private static void AppendCell(StringBuilder builder, string text)
        {
            text = text ?? string.Empty;
            if (text.IndexOfAny(new[] { '\t', '\r', '\n', '"' }) >= 0)
                builder.Append('"').Append(text.Replace("\"", "\"\"")).Append('"');
            else builder.Append(text);
            builder.Append('\t');
        }

        private static void TrimLastTab(StringBuilder builder)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] == '\t') builder.Length--;
        }

        private static bool IsEmpty(IReadOnlyList<string> row)
        {
            for (var i = 0; i < row.Count; i++)
                if (!string.IsNullOrWhiteSpace(row[i])) return false;
            return true;
        }
    }
}
#endif
