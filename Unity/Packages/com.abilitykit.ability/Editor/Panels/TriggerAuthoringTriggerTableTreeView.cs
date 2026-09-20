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
    internal sealed class TriggerAuthoringTriggerTableTreeView : TreeView
    {
        private static readonly GUIContent[] EntryModeOptions =
        {
            new GUIContent("事件触发"),
            new GUIContent("仅供调用")
        };

        private List<TriggerAuthoringTriggerTableRow> _rows = new List<TriggerAuthoringTriggerTableRow>();
        private readonly Dictionary<int, TriggerAuthoringTriggerTableRow> _byId =
            new Dictionary<int, TriggerAuthoringTriggerTableRow>();

        public TriggerAuthoringTriggerTableTreeView(TreeViewState state, MultiColumnHeader header)
            : base(state, header)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            rowHeight = 23f;
            columnIndexForTreeFoldouts = (int)TriggerAuthoringTriggerTableColumn.Name;
            Reload();
        }

        public event Action<IReadOnlyList<int>> SelectionChangedRequested;
        public event Action<int> OpenRequested;
        public event Action<int> ContextMenuRequested;

        public void SetRows(List<TriggerAuthoringTriggerTableRow> rows)
        {
            var selectedTriggers = GetSelectedTriggers();
            _rows = rows ?? new List<TriggerAuthoringTriggerTableRow>();
            _byId.Clear();
            for (var i = 0; i < _rows.Count; i++) _byId[_rows[i].RowId] = _rows[i];
            Reload();
            RestoreSelection(selectedTriggers);
        }

        public IReadOnlyList<int> GetSelectedIndices()
        {
            return TriggerAuthoringTriggerTableModel.CollectIndices(_rows, GetSelection());
        }

        public bool HasSelectedTrigger(TriggerDefinitionData trigger)
        {
            if (trigger == null) return false;
            var selected = GetSelection();
            for (var i = 0; i < selected.Count; i++)
                if (_byId.TryGetValue(selected[i], out var row) && ReferenceEquals(row.Trigger, trigger))
                    return true;
            return false;
        }

        public void SelectTrigger(TriggerDefinitionData trigger)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (!ReferenceEquals(_rows[i].Trigger, trigger)) continue;
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
            for (var i = 0; i < _rows.Count; i++) items.Add(new TriggerTableItem(_rows[i]));
            SetupParentsAndChildrenFromDepths(root, items);
            return root;
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

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
            if (_byId.TryGetValue(id, out var row)) ContextMenuRequested?.Invoke(row.Index);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (TriggerTableItem)args.item;
            if (Event.current.type == EventType.ContextClick &&
                args.rowRect.Contains(Event.current.mousePosition))
            {
                if (!GetSelection().Contains(item.id))
                    SetSelection(new[] { item.id });
                ContextMenuRequested?.Invoke(item.Row.Index);
                Event.current.Use();
                return;
            }

            for (var visibleColumn = 0; visibleColumn < args.GetNumVisibleColumns(); visibleColumn++)
            {
                var column = args.GetColumn(visibleColumn);
                DrawCell(args.GetCellRect(visibleColumn), item.Row, (TriggerAuthoringTriggerTableColumn)column);
            }
        }

        private void DrawCell(
            Rect rect,
            TriggerAuthoringTriggerTableRow row,
            TriggerAuthoringTriggerTableColumn column)
        {
            CenterRectUsingSingleLineHeight(ref rect);
            var trigger = row.Trigger;
            var effective = row.EffectiveTrigger;
            if (trigger == null || effective == null)
            {
                if (column == TriggerAuthoringTriggerTableColumn.Name)
                    EditorGUI.LabelField(rect, "<空触发器>");
                return;
            }

            switch (column)
            {
                case TriggerAuthoringTriggerTableColumn.Enabled:
                    trigger.Enabled = EditorGUI.Toggle(rect, trigger.Enabled);
                    break;
                case TriggerAuthoringTriggerTableColumn.Id:
                    EditorGUI.LabelField(rect, trigger.Id.ToString());
                    break;
                case TriggerAuthoringTriggerTableColumn.Name:
                    trigger.Name = EditorGUI.TextField(rect, trigger.Name ?? string.Empty);
                    break;
                case TriggerAuthoringTriggerTableColumn.Entry:
                    using (new EditorGUI.DisabledScope(row.UsesTemplate))
                        effective.EntryMode = (TriggerEntryMode)EditorGUI.Popup(
                            rect,
                            (int)effective.EntryMode,
                            EntryModeOptions);
                    break;
                case TriggerAuthoringTriggerTableColumn.Event:
                    DrawEvent(rect, row);
                    break;
                case TriggerAuthoringTriggerTableColumn.Group:
                    trigger.GroupPath = EditorGUI.TextField(rect, trigger.GroupPath ?? string.Empty);
                    break;
                case TriggerAuthoringTriggerTableColumn.Priority:
                    using (new EditorGUI.DisabledScope(row.UsesTemplate))
                        effective.Priority = EditorGUI.IntField(rect, effective.Priority);
                    break;
                case TriggerAuthoringTriggerTableColumn.Template:
                    EditorGUI.LabelField(
                        rect,
                        new GUIContent(trigger.Template?.TemplateId ?? string.Empty),
                        EditorStyles.miniLabel);
                    break;
                case TriggerAuthoringTriggerTableColumn.Diagnostics:
                    DrawDiagnostics(rect, row.Diagnostics);
                    break;
            }
        }

        private static void DrawEvent(Rect rect, TriggerAuthoringTriggerTableRow row)
        {
            var effective = row.EffectiveTrigger;
            if (effective.EntryMode == TriggerEntryMode.Callable)
            {
                if (!row.UsesTemplate) effective.Event = string.Empty;
                EditorGUI.LabelField(rect, "仅供调用", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            using (new EditorGUI.DisabledScope(row.UsesTemplate))
                effective.Event = EditorGUI.TextField(rect, effective.Event ?? string.Empty);
        }

        private static void DrawDiagnostics(
            Rect rect,
            TriggerAuthoringTriggerIndex.DiagnosticSummary diagnostics)
        {
            var previous = GUI.color;
            string text;
            if (diagnostics.Errors > 0)
            {
                GUI.color = new Color(1f, 0.48f, 0.44f);
                text = "E" + diagnostics.Errors;
                if (diagnostics.Warnings > 0) text += " W" + diagnostics.Warnings;
            }
            else if (diagnostics.Warnings > 0)
            {
                GUI.color = new Color(1f, 0.75f, 0.3f);
                text = "W" + diagnostics.Warnings;
            }
            else
            {
                GUI.color = new Color(0.48f, 0.78f, 0.52f);
                text = "正常";
            }
            EditorGUI.LabelField(rect, text, EditorStyles.miniBoldLabel);
            GUI.color = previous;
        }

        private List<TriggerDefinitionData> GetSelectedTriggers()
        {
            var result = new List<TriggerDefinitionData>();
            var selected = GetSelection();
            for (var i = 0; i < selected.Count; i++)
                if (_byId.TryGetValue(selected[i], out var row) && row.Trigger != null)
                    result.Add(row.Trigger);
            return result;
        }

        private void RestoreSelection(IReadOnlyList<TriggerDefinitionData> triggers)
        {
            if (triggers == null || triggers.Count == 0) return;
            var ids = new List<int>();
            for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
                {
                    if (!ReferenceEquals(_rows[rowIndex].Trigger, triggers[triggerIndex])) continue;
                    ids.Add(_rows[rowIndex].RowId);
                    break;
                }
            }
            SetSelection(ids);
        }

        private sealed class TriggerTableItem : TreeViewItem
        {
            public TriggerTableItem(TriggerAuthoringTriggerTableRow row)
                : base(row.RowId, 0, row.Trigger?.Name ?? "<空触发器>")
            {
                Row = row;
            }

            public TriggerAuthoringTriggerTableRow Row { get; }
        }
    }
}
#endif
