#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Panels
{
    internal sealed class TriggerAuthoringTemplateMatrixTreeView : TreeView
    {
        private List<TriggerAuthoringTemplateMatrixRow> _rows = new List<TriggerAuthoringTemplateMatrixRow>();
        private IReadOnlyList<TriggerAuthoringTemplateParameterData> _parameters;
        private readonly Dictionary<int, TriggerAuthoringTemplateMatrixRow> _byId =
            new Dictionary<int, TriggerAuthoringTemplateMatrixRow>();

        public TriggerAuthoringTemplateMatrixTreeView(TreeViewState state, MultiColumnHeader header)
            : base(state, header)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            rowHeight = 23f;
            columnIndexForTreeFoldouts = 1;
            Reload();
        }

        public event Action<IReadOnlyList<int>> SelectionChangedRequested;
        public event Action<int> OpenRequested;

        public void SetRows(
            List<TriggerAuthoringTemplateMatrixRow> rows,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            var selectedTriggers = GetSelectedTriggers();
            _rows = rows ?? new List<TriggerAuthoringTemplateMatrixRow>();
            _parameters = parameters ?? Array.Empty<TriggerAuthoringTemplateParameterData>();
            _byId.Clear();
            for (var i = 0; i < _rows.Count; i++) _byId[_rows[i].RowId] = _rows[i];
            Reload();
            RestoreSelection(selectedTriggers);
        }

        public IReadOnlyList<int> GetSelectedIndices()
        {
            return TriggerAuthoringTemplateMatrixModel.CollectIndices(_rows, GetSelection());
        }

        public IReadOnlyList<TriggerAuthoringTemplateMatrixRow> GetSelectedRows()
        {
            var result = new List<TriggerAuthoringTemplateMatrixRow>();
            var selected = GetSelection();
            for (var i = 0; i < selected.Count; i++)
                if (_byId.TryGetValue(selected[i], out var row)) result.Add(row);
            return result;
        }

        public void EnsureSelection(TriggerDefinitionData trigger)
        {
            if (trigger == null) return;
            var selection = GetSelection();
            for (var i = 0; i < selection.Count; i++)
                if (_byId.TryGetValue(selection[i], out var selected) && ReferenceEquals(selected.Trigger, trigger))
                    return;
            for (var i = 0; i < _rows.Count; i++)
                if (ReferenceEquals(_rows[i].Trigger, trigger))
                {
                    SetSelection(new[] { _rows[i].RowId }, TreeViewSelectionOptions.RevealAndFrame);
                    return;
                }
        }

        public void SelectEveryRow()
        {
            var ids = new List<int>(_rows.Count);
            for (var i = 0; i < _rows.Count; i++) ids.Add(_rows[i].RowId);
            SetSelection(ids);
            SelectionChanged(GetSelection());
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(0, -1, "root");
            var items = new List<TreeViewItem>(_rows.Count);
            for (var i = 0; i < _rows.Count; i++) items.Add(new MatrixItem(_rows[i]));
            SetupParentsAndChildrenFromDepths(root, items);
            return root;
        }

        protected override bool CanMultiSelect(TreeViewItem item) { return true; }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            SelectionChangedRequested?.Invoke(GetSelectedIndices());
        }

        protected override void DoubleClickedItem(int id)
        {
            if (_byId.TryGetValue(id, out var row)) OpenRequested?.Invoke(row.Index);
        }

        protected override void ContextClickedItem(int id)
        {
            if (_byId.TryGetValue(id, out var row)) OpenRequested?.Invoke(row.Index);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var row = ((MatrixItem)args.item).Row;
            for (var visible = 0; visible < args.GetNumVisibleColumns(); visible++)
            {
                var column = args.GetColumn(visible);
                DrawCell(args.GetCellRect(visible), row, column);
            }
        }

        private void DrawCell(Rect rect, TriggerAuthoringTemplateMatrixRow row, int column)
        {
            CenterRectUsingSingleLineHeight(ref rect);
            var trigger = row.Trigger;
            if (trigger == null) return;
            switch (column)
            {
                case 0: EditorGUI.LabelField(rect, trigger.Id.ToString()); return;
                case 1: trigger.Name = EditorGUI.TextField(rect, trigger.Name ?? string.Empty); return;
                case 2: trigger.GroupPath = EditorGUI.TextField(rect, trigger.GroupPath ?? string.Empty); return;
                case 3:
                    TriggerAuthoringTemplateMatrixCellDrawer.DrawDiagnostics(rect, row.Diagnostics);
                    return;
            }

            var parameterIndex = column - 4;
            if (parameterIndex < 0 || parameterIndex >= _parameters.Count) return;
            var parameter = _parameters[parameterIndex];
            if (parameter == null) return;
            var binding = row.FindBinding(parameter.Name);
            TriggerAuthoringTemplateMatrixCellDrawer.DrawBinding(rect, trigger, parameter, binding);
        }

        private List<TriggerDefinitionData> GetSelectedTriggers()
        {
            var result = new List<TriggerDefinitionData>();
            var selected = GetSelection();
            for (var i = 0; i < selected.Count; i++)
                if (_byId.TryGetValue(selected[i], out var row) && row.Trigger != null) result.Add(row.Trigger);
            return result;
        }

        private void RestoreSelection(IReadOnlyList<TriggerDefinitionData> triggers)
        {
            if (triggers == null || triggers.Count == 0) return;
            var ids = new List<int>();
            for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
                for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
                    if (ReferenceEquals(_rows[rowIndex].Trigger, triggers[triggerIndex]))
                    {
                        ids.Add(_rows[rowIndex].RowId);
                        break;
                    }
            SetSelection(ids);
        }

        private sealed class MatrixItem : TreeViewItem
        {
            public MatrixItem(TriggerAuthoringTemplateMatrixRow row)
                : base(row.RowId, 0, row.Trigger?.Name ?? "<空触发器>") { Row = row; }
            public TriggerAuthoringTemplateMatrixRow Row { get; }
        }
    }
}
#endif
