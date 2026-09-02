#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace AbilityKit.Pipeline.Editor
{
    internal readonly struct PipelineDebuggerContextRow
    {
        public PipelineDebuggerContextRow(
            string name,
            string initialValue,
            string currentValue)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            InitialValue = initialValue
                ?? throw new ArgumentNullException(nameof(initialValue));
            CurrentValue = currentValue
                ?? throw new ArgumentNullException(nameof(currentValue));
        }

        public string Name { get; }
        public string InitialValue { get; }
        public string CurrentValue { get; }
        public bool IsChanged => InitialValue != CurrentValue;
    }

    internal sealed class PipelineDebuggerContextModel
    {
        internal const string InitialValueFallback = "<not captured>";
        internal const string CurrentValueFallback = "<removed>";

        private readonly List<string> _names = new List<string>();
        private readonly List<PipelineDebuggerContextRow> _visibleRows =
            new List<PipelineDebuggerContextRow>();

        public IReadOnlyList<PipelineDebuggerContextRow> VisibleRows =>
            _visibleRows;

        public void Rebuild(
            IReadOnlyList<EditorPipelineRegistry.DebugValue> initialValues,
            IReadOnlyList<EditorPipelineRegistry.DebugValue> currentValues,
            bool showOnlyChanged,
            string? search)
        {
            if (initialValues == null)
                throw new ArgumentNullException(nameof(initialValues));
            if (currentValues == null)
                throw new ArgumentNullException(nameof(currentValues));

            BuildNames(initialValues, currentValues);
            _visibleRows.Clear();

            for (int i = 0; i < _names.Count; i++)
            {
                string name = _names[i];
                var row = new PipelineDebuggerContextRow(
                    name,
                    FindValue(initialValues, name, InitialValueFallback),
                    FindValue(currentValues, name, CurrentValueFallback));

                if (showOnlyChanged && !row.IsChanged)
                    continue;
                if (!PipelineDebuggerViewPolicy.MatchesContext(
                        row.Name,
                        row.InitialValue,
                        row.CurrentValue,
                        search))
                {
                    continue;
                }

                _visibleRows.Add(row);
            }
        }

        public string BuildVisibleText()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < _visibleRows.Count; i++)
            {
                PipelineDebuggerContextRow row = _visibleRows[i];
                if (builder.Length > 0)
                    builder.AppendLine();

                builder.Append(row.Name).Append(" = ");
                if (row.IsChanged)
                    builder.Append(row.InitialValue).Append(" -> ");
                builder.Append(row.CurrentValue);
            }

            return builder.ToString();
        }

        private void BuildNames(
            IReadOnlyList<EditorPipelineRegistry.DebugValue> initialValues,
            IReadOnlyList<EditorPipelineRegistry.DebugValue> currentValues)
        {
            _names.Clear();
            AddNames(initialValues);
            AddNames(currentValues);
            _names.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private void AddNames(
            IReadOnlyList<EditorPipelineRegistry.DebugValue> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (!_names.Contains(values[i].Name))
                    _names.Add(values[i].Name);
            }
        }

        private static string FindValue(
            IReadOnlyList<EditorPipelineRegistry.DebugValue> values,
            string name,
            string fallback)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Name == name)
                    return values[i].Value;
            }

            return fallback;
        }
    }
}

#endif
