#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.Framework.Examples
{
    // ========== 示例类型 ==========

    public class TriggerDebugInfo
    {
        public int TriggerId;
        public string TriggerName;
        public string Status;
        public long ElapsedMs;
        public List<ConditionDebugInfo> Conditions = new();
        public List<ActionDebugInfo> Actions = new();
        public override string ToString() => $"[{TriggerId}] {TriggerName}";
    }

    public class ConditionDebugInfo { public string Name; public bool Passed; }
    public class ActionDebugInfo { public string Name; public bool Executed; public long ElapsedMs; }

    [Serializable]
    public class TriggerDebuggerConfig : IWindowConfig
    {
        public bool AutoRefresh = true;
        public float RefreshInterval = 0.5f;
        public void Validate() { }
        public string ToJson() => JsonUtility.ToJson(this);
        public void FromJson(string json) => JsonUtility.FromJsonOverwrite(json, this);
    }

    public class AbilityAsset
    {
        public int Id;
        public string Name;
        public string Type;
        public bool IsEnabled;
        public override string ToString() => Name;
    }

    [Serializable]
    public class AbilityListConfig : IWindowConfig
    {
        public int PageSize = 50;
        public bool ShowDisabled = true;
        public void Validate() { }
        public string ToJson() => JsonUtility.ToJson(this);
        public void FromJson(string json) => JsonUtility.FromJsonOverwrite(json, this);
    }

    // ========== Trigger 调试窗口示例 ==========

    public class TriggerDebuggerWindow : PlugableWindow<TriggerDebugInfo, TriggerDebuggerConfig>
    {
        protected override string WindowTitle => "Trigger 调试器";
        protected override int DefaultListWidth => 350;

        protected override IEnumerable<TriggerDebugInfo> LoadData()
        {
            return new List<TriggerDebugInfo>
            {
                new TriggerDebugInfo { TriggerId = 1, TriggerName = "OnSpawn", Status = "Success", ElapsedMs = 2 },
                new TriggerDebugInfo { TriggerId = 2, TriggerName = "OnDeath", Status = "Failed", ElapsedMs = 15 },
                new TriggerDebugInfo { TriggerId = 3, TriggerName = "OnDamaged", Status = "Success", ElapsedMs = 3 },
            };
        }

        protected override void DrawDetail(TriggerDebugInfo item)
        {
            if (item == null) return;

            EditorGUILayout.LabelField("基本信息", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"ID: {item.TriggerId}");
            EditorGUILayout.LabelField($"名称: {item.TriggerName}");
            EditorGUILayout.LabelField($"状态: {item.Status}");
            EditorGUILayout.LabelField($"耗时: {item.ElapsedMs}ms");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("条件评估", EditorStyles.boldLabel);
            foreach (var cond in item.Conditions)
            {
                GUI.color = cond.Passed ? Color.green : Color.red;
                EditorGUILayout.LabelField($"  {(cond.Passed ? "✓" : "✗")} {cond.Name}");
            }
            GUI.color = Color.white;
        }

        protected override bool ContainsSearchText(TriggerDebugInfo item, string text)
        {
            return item.TriggerName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                   item.TriggerId.ToString().Contains(text);
        }

        [MenuItem("Window/AbilityKit/Debug/Trigger")]
        public static void ShowWindow()
        {
            var window = CreateInstance<TriggerDebuggerWindow>();
            window.Initialize(
                dataSource: window.LoadData(),
                plugins: new IWindowPlugin<TriggerDebugInfo>[]
                {
                    new SearchPlugin<TriggerDebugInfo>(),
                    new PagingPlugin<TriggerDebugInfo>(),
                    new ExportPlugin<TriggerDebugInfo>(new JsonExporter<TriggerDebugInfo>())
                });
            window.Show();
        }
    }

    // ========== 能力列表窗口示例 ==========

    public class AbilityListWindow : PlugableWindow<AbilityAsset, AbilityListConfig>
    {
        protected override string WindowTitle => "技能列表";
        protected override int DefaultListWidth => 280;

        protected override IEnumerable<AbilityAsset> LoadData()
        {
            return new List<AbilityAsset>
            {
                new AbilityAsset { Id = 1, Name = "普通攻击", Type = "Attack", IsEnabled = true },
                new AbilityAsset { Id = 2, Name = "冲刺", Type = "Movement", IsEnabled = true },
                new AbilityAsset { Id = 3, Name = "终极技能", Type = "Ultimate", IsEnabled = false },
            };
        }

        protected override void DrawDetail(AbilityAsset item)
        {
            if (item == null) return;

            EditorGUILayout.LabelField("基本信息", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"ID: {item.Id}");
            EditorGUILayout.LabelField($"名称: {item.Name}");
            EditorGUILayout.LabelField($"类型: {item.Type}");
            EditorGUILayout.LabelField($"启用: {item.IsEnabled}");
            EditorGUI.indentLevel--;
        }

        protected override void DrawListItem(AbilityAsset item, Rect rect, bool isSelected)
        {
            var icon = item.IsEnabled ? "●" : "○";
            var color = item.IsEnabled ? Color.green : Color.gray;

            GUI.color = color;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 20, rect.height), icon);
            GUI.color = Color.white;

            EditorGUI.LabelField(
                new Rect(rect.x + 20, rect.y, rect.width - 20, rect.height),
                item.Name);
        }

        [MenuItem("Window/AbilityKit/Ability List")]
        public static void ShowWindow()
        {
            var window = CreateInstance<AbilityListWindow>();
            window.Initialize(window.LoadData());
            window.Show();
        }
    }

    // ========== 插件示例 ==========

    public class SearchPlugin<TData> : BaseWindowPlugin<TData>
    {
        private string _searchText = "";

        public override void OnListHeaderGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5);
            GUILayout.Label("🔍", GUILayout.Width(20));

            var newText = EditorGUILayout.TextField(
                _searchText,
                string.IsNullOrEmpty(_searchText) ? "搜索..." : EditorStyles.toolbarSearchField);

            if (!string.IsNullOrEmpty(_searchText) && GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                _searchText = "";
                newText = "";
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    public class PagingPlugin<TData> : BaseWindowPlugin<TData>
    {
        private int _pageSize = 50;
        private int _currentPage = 1;
        private int _totalPages = 1;

        public override void OnListGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginDisabledGroup(_currentPage <= 1);
            if (GUILayout.Button("⏮", EditorStyles.toolbarButton, GUILayout.Width(30))) _currentPage = 1;
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(_currentPage <= 1);
            if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(25))) _currentPage--;
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);
            EditorGUILayout.LabelField($"第 {_currentPage}/{_totalPages} 页", GUILayout.Width(90));

            EditorGUI.BeginDisabledGroup(_currentPage >= _totalPages);
            if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(25))) _currentPage++;
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(_currentPage >= _totalPages);
            if (GUILayout.Button("⏭", EditorStyles.toolbarButton, GUILayout.Width(30))) _currentPage = _totalPages;
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        public override void OnDataLoaded(IList<TData> data)
        {
            _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)data.Count / _pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 1, _totalPages);
        }
    }

    public class ExportPlugin<TData> : BaseWindowPlugin<TData>
    {
        private readonly List<IExporter<TData>> _exporters = new();

        public ExportPlugin(params IExporter<TData>[] exporters)
        {
            _exporters.AddRange(exporters);
        }

        public override void OnToolbarGUI()
        {
            if (_exporters.Count == 0) return;

            if (GUILayout.Button("导出", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var menu = new GenericMenu();
                foreach (var exporter in _exporters)
                {
                    menu.AddItem(
                        new GUIContent($"{exporter.Name} (.{exporter.Extension})"),
                        false,
                        () => { });
                }
                menu.ShowAsContext();
            }
        }
    }
}
#endif
