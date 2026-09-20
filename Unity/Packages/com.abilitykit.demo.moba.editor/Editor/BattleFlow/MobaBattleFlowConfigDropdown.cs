#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>Searchable config picker. Manual numeric entry remains outside this dropdown.</summary>
    internal sealed class MobaBattleFlowConfigDropdown : AdvancedDropdown
    {
        private readonly string _title;
        private readonly IReadOnlyList<MobaBattleFlowConfigEntry> _entries;
        private readonly Action<MobaBattleFlowConfigEntry> _onSelected;
        private readonly List<MobaBattleFlowConfigEntry> _indexedEntries =
            new List<MobaBattleFlowConfigEntry>();

        public MobaBattleFlowConfigDropdown(
            AdvancedDropdownState state,
            string title,
            IReadOnlyList<MobaBattleFlowConfigEntry> entries,
            Action<MobaBattleFlowConfigEntry> onSelected)
            : base(state)
        {
            _title = title;
            _entries = entries ?? Array.Empty<MobaBattleFlowConfigEntry>();
            _onSelected = onSelected;
            minimumSize = new Vector2(360f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            _indexedEntries.Clear();
            var root = new AdvancedDropdownItem(_title);
            var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                var groupName = string.IsNullOrWhiteSpace(entry.Group) ? "配置" : entry.Group;
                if (!groups.TryGetValue(groupName, out var group))
                {
                    group = new AdvancedDropdownItem(groupName);
                    groups.Add(groupName, group);
                    root.AddChild(group);
                }

                var item = new AdvancedDropdownItem(entry.Label) { id = _indexedEntries.Count };
                _indexedEntries.Add(entry);
                group.AddChild(item);
            }

            if (_indexedEntries.Count == 0)
                root.AddChild(new AdvancedDropdownItem("未找到配置") { enabled = false });
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item.id < 0 || item.id >= _indexedEntries.Count) return;
            _onSelected?.Invoke(_indexedEntries[item.id]);
        }
    }
}
#endif
