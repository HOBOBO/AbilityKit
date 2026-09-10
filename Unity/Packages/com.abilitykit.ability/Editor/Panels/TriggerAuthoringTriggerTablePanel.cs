#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Panels
{
    internal sealed class TriggerAuthoringTriggerTablePanel
    {
        private readonly TriggerAuthoringTriggerTableTreeView _tree;
        private IReadOnlyList<TriggerAuthoringTriggerIndex.Entry> _entries;
        private int _contentSignature = int.MinValue;

        public TriggerAuthoringTriggerTablePanel()
        {
            var header = new MultiColumnHeader(CreateHeaderState());
            header.ResizeToFit();
            _tree = new TriggerAuthoringTriggerTableTreeView(new TreeViewState(), header);
            _tree.SelectionChangedRequested += indices => SelectionChanged?.Invoke(indices);
            _tree.OpenRequested += index => OpenRequested?.Invoke(index);
            _tree.ContextMenuRequested += index => ContextMenuRequested?.Invoke(index);
            header.sortingChanged += _ => RebuildRows(true);
        }

        public event Action<IReadOnlyList<int>> SelectionChanged;
        public event Action<int> OpenRequested;
        public event Action<int> ContextMenuRequested;

        public int SelectedCount => _tree.GetSelection().Count;

        public void SetEntries(IReadOnlyList<TriggerAuthoringTriggerIndex.Entry> entries)
        {
            _entries = entries;
            var signature = ComputeSignature(entries);
            if (signature == _contentSignature) return;
            _contentSignature = signature;
            RebuildRows(false);
        }

        public void Draw(Rect rect)
        {
            _tree.OnGUI(rect);
        }

        public IReadOnlyList<int> GetSelectedIndices()
        {
            return _tree.GetSelectedIndices();
        }

        public void EnsureSelection(TriggerDefinitionData trigger)
        {
            if (trigger == null || _tree.HasSelectedTrigger(trigger)) return;
            _tree.SelectTrigger(trigger);
        }

        public void SelectAll()
        {
            _tree.SelectEveryRow();
        }

        public void ClearSelection()
        {
            _tree.SetSelection(Array.Empty<int>());
        }

        private void RebuildRows(bool sortChanged)
        {
            var column = TriggerAuthoringTriggerTableColumn.Id;
            var ascending = true;
            var header = _tree.multiColumnHeader;
            if (header.sortedColumnIndex >= 0)
            {
                column = (TriggerAuthoringTriggerTableColumn)header.sortedColumnIndex;
                ascending = header.IsSortedAscending(header.sortedColumnIndex);
            }
            _tree.SetRows(TriggerAuthoringTriggerTableModel.BuildRows(_entries, column, ascending));
            if (sortChanged) _contentSignature = int.MinValue;
        }

        private static MultiColumnHeaderState CreateHeaderState()
        {
            return new MultiColumnHeaderState(new[]
            {
                Column("启用", "是否参与运行时编译", 48f, 42f),
                Column("TriggerId", "项目内稳定触发器标识", 78f, 64f),
                Column("名称", "触发器显示名称", 180f, 110f),
                Column("入口", "事件触发或仅供调用", 82f, 70f),
                Column("事件", "订阅的 EventBus 事件", 190f, 110f),
                Column("业务分组", "仅用于编辑器整理", 160f, 100f),
                Column("优先级", "执行优先级", 68f, 58f),
                Column("函数库", "引用的函数库资源", 160f, 100f),
                Column("诊断", "当前规则的错误与警告", 76f, 64f)
            });
        }

        private static MultiColumnHeaderState.Column Column(
            string label,
            string tooltip,
            float width,
            float minimumWidth)
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

        private static int ComputeSignature(IReadOnlyList<TriggerAuthoringTriggerIndex.Entry> entries)
        {
            unchecked
            {
                var hash = 17;
                var count = entries != null ? entries.Count : 0;
                hash = hash * 31 + count;
                for (var i = 0; i < count; i++)
                {
                    var entry = entries[i];
                    var trigger = entry.Trigger;
                    var effective = entry.EffectiveTrigger;
                    hash = hash * 31 + entry.Index;
                    hash = hash * 31 + (trigger != null ? trigger.GetHashCode() : 0);
                    hash = hash * 31 + (trigger?.Id ?? 0);
                    hash = hash * 31 + (trigger?.Enabled == true ? 1 : 0);
                    hash = hash * 31 + (trigger?.Name?.GetHashCode() ?? 0);
                    hash = hash * 31 + (trigger?.GroupPath?.GetHashCode() ?? 0);
                    hash = hash * 31 + (trigger?.Template?.TemplateId?.GetHashCode() ?? 0);
                    hash = hash * 31 + (int)(effective?.EntryMode ?? TriggerEntryMode.Event);
                    hash = hash * 31 + (effective?.Event?.GetHashCode() ?? 0);
                    hash = hash * 31 + (effective?.Priority ?? 0);
                    hash = hash * 31 + entry.Diagnostics.Errors;
                    hash = hash * 31 + entry.Diagnostics.Warnings;
                }
                return hash;
            }
        }
    }
}
#endif
