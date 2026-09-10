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
    internal sealed class TriggerAuthoringTemplateMatrixPanel
    {
        private TriggerAuthoringTemplateMatrixTreeView _tree;
        private List<TriggerAuthoringTemplateMatrixRow> _rows = new List<TriggerAuthoringTemplateMatrixRow>();
        private string _selectedTemplateId;
        private string _search = string.Empty;
        private int _contentSignature = int.MinValue;
        private int _headerSignature = int.MinValue;

        public event Action<IReadOnlyList<int>> SelectionChanged;
        public event Action<int> OpenRequested;
        public event Action<TriggerAuthoringTemplateData, string> PasteRequested;
        public event Action<string> NotificationRequested;

        public void Reset()
        {
            _selectedTemplateId = null;
            _contentSignature = int.MinValue;
        }

        public void Invalidate() { _contentSignature = int.MinValue; }

        public void Draw(
            IReadOnlyList<TriggerDefinitionData> triggers,
            IReadOnlyList<TriggerAuthoringTriggerIndex.Entry> entries,
            TriggerTemplateDescriptorCatalog catalog,
            TriggerDefinitionData selectedTrigger)
        {
            var usedTemplates = TriggerAuthoringTemplateMatrixModel.BuildUsedTemplates(triggers, catalog);
            if (usedTemplates.Count == 0)
            {
                EditorGUILayout.HelpBox("当前模块还没有引用函数库的触发器，暂无可编辑的参数矩阵。", MessageType.Info);
                return;
            }

            EnsureTemplateSelection(usedTemplates, selectedTrigger);
            var selected = FindSelected(usedTemplates);
            if (selected?.Asset?.Template == null) return;
            var template = selected.Asset.Template;
            var parameters = TriggerAuthoringTemplateMatrixModel.BuildParameters(template.Parameters);
            EnsureTree(parameters);

            DrawTemplateToolbar(usedTemplates, selected);
            RebuildRowsIfNeeded(entries, template, parameters);
            DrawOperationToolbar(template);
            if (selectedTrigger != null) _tree.EnsureSelection(selectedTrigger);

            var rect = GUILayoutUtility.GetRect(
                0f,
                10000f,
                360f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            _tree.OnGUI(rect);
        }

        private void DrawTemplateToolbar(
            IReadOnlyList<TriggerAuthoringUsedTemplate> usedTemplates,
            TriggerAuthoringUsedTemplate selected)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("函数库", EditorStyles.miniLabel, GUILayout.Width(48f));
            var labels = new string[usedTemplates.Count];
            var selectedIndex = 0;
            for (var i = 0; i < usedTemplates.Count; i++)
            {
                labels[i] = usedTemplates[i].DisplayName + "  (" + usedTemplates[i].InstanceCount + ")";
                if (string.Equals(usedTemplates[i].Id, _selectedTemplateId, StringComparison.Ordinal)) selectedIndex = i;
            }
            var next = EditorGUILayout.Popup(selectedIndex, labels, EditorStyles.toolbarPopup, GUILayout.MinWidth(180f));
            if (next != selectedIndex)
            {
                _selectedTemplateId = usedTemplates[next].Id;
                _headerSignature = int.MinValue;
                _contentSignature = int.MinValue;
            }
            GUILayout.Space(8f);
            GUILayout.Label("搜索", EditorStyles.miniLabel, GUILayout.Width(34f));
            var nextSearch = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120f));
            if (!string.Equals(nextSearch, _search, StringComparison.Ordinal))
            {
                _search = nextSearch;
                _contentSignature = int.MinValue;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(selected.Id, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOperationToolbar(TriggerAuthoringTemplateData template)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("已选 " + (_tree?.GetSelection().Count ?? 0) + " / 显示 " + _rows.Count,
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_rows.Count == 0))
            {
                if (GUILayout.Button("全选", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    _tree.SelectEveryRow();
                if (GUILayout.Button(new GUIContent("复制全部", "复制 TSV，可直接粘贴到 Excel"),
                        EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    CopyRows(_rows, template);
            }
            var selectedRows = _tree?.GetSelectedRows();
            using (new EditorGUI.DisabledScope(selectedRows == null || selectedRows.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("复制选中", "仅复制明确选中的调用实例"),
                        EditorStyles.toolbarButton, GUILayout.Width(68f)))
                    CopyRows(selectedRows, template);
            }
            if (GUILayout.Button(new GUIContent("粘贴预览", "从 Excel/TSV 解析并预览，不会立即覆盖"),
                    EditorStyles.toolbarButton, GUILayout.Width(72f)))
                PasteRequested?.Invoke(template, EditorGUIUtility.systemCopyBuffer);
            EditorGUILayout.EndHorizontal();
        }

        private void CopyRows(
            IReadOnlyList<TriggerAuthoringTemplateMatrixRow> rows,
            TriggerAuthoringTemplateData template)
        {
            EditorGUIUtility.systemCopyBuffer = TriggerAuthoringTemplateBindingTsvCodec.Build(rows, template);
            NotificationRequested?.Invoke("已复制 " + (rows?.Count ?? 0) + " 个函数调用实例");
        }

        private void EnsureTemplateSelection(
            IReadOnlyList<TriggerAuthoringUsedTemplate> usedTemplates,
            TriggerDefinitionData selectedTrigger)
        {
            var selectedId = selectedTrigger?.Template?.TemplateId;
            if (TriggerAuthoringTemplateMatrixLayout.Contains(usedTemplates, _selectedTemplateId)) return;
            _selectedTemplateId = !string.IsNullOrWhiteSpace(selectedId) &&
                                  TriggerAuthoringTemplateMatrixLayout.Contains(usedTemplates, selectedId)
                ? selectedId
                : usedTemplates[0].Id;
        }

        private TriggerAuthoringUsedTemplate FindSelected(IReadOnlyList<TriggerAuthoringUsedTemplate> usedTemplates)
        {
            for (var i = 0; i < usedTemplates.Count; i++)
                if (string.Equals(usedTemplates[i].Id, _selectedTemplateId, StringComparison.Ordinal))
                    return usedTemplates[i];
            return null;
        }

        private void EnsureTree(IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            var signature = TriggerAuthoringTemplateMatrixLayout.ComputeHeaderSignature(parameters);
            if (_tree != null && signature == _headerSignature) return;
            _headerSignature = signature;
            var header = new MultiColumnHeader(TriggerAuthoringTemplateMatrixLayout.CreateHeaderState(parameters));
            _tree = new TriggerAuthoringTemplateMatrixTreeView(new TreeViewState(), header);
            _tree.SelectionChangedRequested += indices => SelectionChanged?.Invoke(indices);
            _tree.OpenRequested += index => OpenRequested?.Invoke(index);
            header.sortingChanged += _ => _contentSignature = int.MinValue;
            _contentSignature = int.MinValue;
        }

        private void RebuildRowsIfNeeded(
            IReadOnlyList<TriggerAuthoringTriggerIndex.Entry> entries,
            TriggerAuthoringTemplateData template,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            var signature = TriggerAuthoringTemplateMatrixLayout.ComputeContentSignature(
                entries, template.TemplateId, _search);
            if (signature == _contentSignature) return;
            _contentSignature = signature;
            var sortedColumn = _tree.multiColumnHeader.sortedColumnIndex;
            if (sortedColumn < 0) sortedColumn = 0;
            var ascending = _tree.multiColumnHeader.sortedColumnIndex < 0 ||
                            _tree.multiColumnHeader.IsSortedAscending(sortedColumn);
            _rows = TriggerAuthoringTemplateMatrixModel.BuildRows(
                entries,
                template.TemplateId,
                _search,
                sortedColumn,
                ascending,
                parameters);
            _tree.SetRows(_rows, parameters);
        }

    }
}
#endif
