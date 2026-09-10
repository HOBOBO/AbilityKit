#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>最近使用的节点类型记录（按 Condition/Action 分开，EditorPrefs 保存，跨会话持久）。</summary>
    internal static class TriggerNodeRecentUsage
    {
        private const int Capacity = 8;

        public static List<string> GetRecent(TriggerNodeKind kind)
        {
            var result = new List<string>();
            var raw = EditorPrefs.GetString(BuildKey(kind), string.Empty);
            if (string.IsNullOrEmpty(raw)) return result;
            var parts = raw.Split('|');
            for (var i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i])) result.Add(parts[i]);
            }
            return result;
        }

        public static void RecordUse(TriggerNodeKind kind, string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return;
            var current = GetRecent(kind);
            current.Remove(type);
            current.Insert(0, type);
            if (current.Count > Capacity) current.RemoveRange(Capacity, current.Count - Capacity);
            EditorPrefs.SetString(BuildKey(kind), string.Join("|", current.ToArray()));
        }

        private static string BuildKey(TriggerNodeKind kind)
        {
            return "AbilityKit.TriggerAuthoring.RecentNodes." + kind;
        }
    }

    /// <summary>
    /// 可搜索的节点类型浏览器（Y3 式）：内置搜索框、分类树、最近使用。
    /// id 空间：0..N-1 为类型描述符，1_000_000 起为组引用。
    /// 描述符目录取自 TriggerTypeDescriptorCatalog.CreateProjectDefaults()，与校验器一致。
    /// </summary>
    internal sealed class TriggerNodeTypeBrowser : AdvancedDropdown
    {
        private const int GroupIdOffset = 1000000;

        private readonly TriggerNodeKind _kind;
        private readonly Action<TriggerTypeDescriptor> _onSelectType;
        private readonly List<TriggerNodeGroupData> _groups;
        private readonly Action<string> _onSelectGroup;
        private readonly TriggerTypeDescriptorCatalog _catalog;
        private readonly List<TriggerTypeDescriptor> _descriptors = new List<TriggerTypeDescriptor>();
        private readonly List<TriggerNodeGroupData> _resolvedGroups = new List<TriggerNodeGroupData>();

        public TriggerNodeTypeBrowser(
            AdvancedDropdownState state,
            TriggerNodeKind kind,
            Action<TriggerTypeDescriptor> onSelectType,
            List<TriggerNodeGroupData> groups = null,
            Action<string> onSelectGroup = null,
            TriggerTypeDescriptorCatalog catalog = null)
            : base(state)
        {
            _kind = kind;
            _onSelectType = onSelectType;
            _groups = groups;
            _onSelectGroup = onSelectGroup;
            _catalog = catalog;
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            _descriptors.Clear();
            _resolvedGroups.Clear();
            var root = new AdvancedDropdownItem(_kind == TriggerNodeKind.Condition ? "选择条件" : "选择行为");
            var catalog = _catalog ?? TriggerTypeDescriptorCatalog.CreateForProject(null);
            var categories = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);

            var recent = TriggerNodeRecentUsage.GetRecent(_kind);
            if (recent.Count > 0)
            {
                var recentItem = new AdvancedDropdownItem("最近使用");
                var added = 0;
                for (var i = 0; i < recent.Count; i++)
                {
                    if (!catalog.TryGet(_kind, recent[i], out var descriptor)) continue;
                    recentItem.AddChild(CreateTypeItem(descriptor));
                    added++;
                }
                if (added > 0) root.AddChild(recentItem);
            }

            var descriptors = catalog.GetAll(_kind);
            for (var i = 0; i < descriptors.Count; i++)
            {
                var parent = GetOrCreateCategory(root, categories, descriptors[i].Category);
                parent.AddChild(CreateTypeItem(descriptors[i]));
            }

            if (_groups != null && _onSelectGroup != null)
            {
                var groupParent = GetOrCreateCategory(root, categories, "可复用组");
                for (var i = 0; i < _groups.Count; i++)
                {
                    var group = _groups[i];
                    if (group == null || string.IsNullOrWhiteSpace(group.Id)) continue;
                    var label = string.IsNullOrWhiteSpace(group.DisplayName) ? group.Id : group.DisplayName;
                    var item = new AdvancedDropdownItem(label + "  [" + group.Id + "]")
                    {
                        id = GroupIdOffset + _resolvedGroups.Count
                    };
                    _resolvedGroups.Add(group);
                    groupParent.AddChild(item);
                }
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item.id >= GroupIdOffset)
            {
                var groupIndex = item.id - GroupIdOffset;
                if (groupIndex >= 0 && groupIndex < _resolvedGroups.Count)
                    _onSelectGroup?.Invoke(_resolvedGroups[groupIndex].Id);
                return;
            }

            if (item.id >= 0 && item.id < _descriptors.Count)
            {
                var descriptor = _descriptors[item.id];
                TriggerNodeRecentUsage.RecordUse(descriptor.Kind, descriptor.Type);
                _onSelectType?.Invoke(descriptor);
            }
        }

        private AdvancedDropdownItem CreateTypeItem(TriggerTypeDescriptor descriptor)
        {
            var capability = descriptor.Kind == TriggerNodeKind.Action && !descriptor.RuntimeSupported
                ? "  [仅编辑器]"
                : string.Empty;
            var item = new AdvancedDropdownItem(
                TriggerAuthoringEditorLabels.Node(descriptor.Type, descriptor.DisplayName) +
                "  [" + descriptor.Type + "]" + capability)
            {
                id = _descriptors.Count
            };
            _descriptors.Add(descriptor);
            return item;
        }

        private static AdvancedDropdownItem GetOrCreateCategory(
            AdvancedDropdownItem root,
            Dictionary<string, AdvancedDropdownItem> cache,
            string category)
        {
            var path = string.IsNullOrEmpty(category) ? "其他" : category;
            if (cache.TryGetValue(path, out var cached)) return cached;

            var parts = path.Split('/');
            var parent = root;
            var prefix = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                prefix = prefix.Length == 0 ? parts[i] : prefix + "/" + parts[i];
                if (!cache.TryGetValue(prefix, out var node))
                {
                    node = new AdvancedDropdownItem(TranslateCategory(parts[i]));
                    parent.AddChild(node);
                    cache.Add(prefix, node);
                }
                parent = node;
            }
            return parent;
        }

        private static string TranslateCategory(string category)
        {
            switch (category)
            {
                case "Condition": return "条件";
                case "Action": return "行为";
                case "Composite": return "组合";
                case "Constant": return "常量";
                case "Compare": return "比较";
                case "Combat": return "战斗";
                case "Context": return "上下文";
                case "Blackboard": return "黑板";
                case "Flow": return "流程";
                case "Debug": return "调试";
                case "Variable": return "变量";
                case "Attribute": return "属性";
                case "Buff": return "增益效果";
                case "Shield": return "护盾";
                case "Resource": return "资源";
                case "Projectile": return "投射物";
                case "Summon": return "召唤物";
                case "Area": return "区域";
                case "Skill": return "技能";
                case "Motion": return "位移";
                case "Presentation": return "表现";
                case "Gameplay": return "玩法";
                default: return category;
            }
        }
    }
}
#endif
