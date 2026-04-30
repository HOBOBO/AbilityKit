#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.Framework
{
    // ========== 导出器 ==========

    public interface IExporter
    {
        string Name { get; }
        string Extension { get; }
        void Export(IList<object> data, string path);
    }

    public interface IExporter<T> : IExporter
    {
        void Export(IList<T> data, string path);
    }

    public class JsonExporter<T> : IExporter<T>
    {
        public string Name => "JSON";
        public string Extension => "json";

        public void Export(IList<T> data, string path)
        {
            var json = JsonUtility.ToJson(data, true);
            System.IO.File.WriteAllText(path, json);
        }

        public void Export(IList<object> data, string path)
        {
            Export(data.Cast<T>().ToList(), path);
        }
    }

    public class CsvExporter<T> : IExporter<T>
    {
        public string Name => "CSV";
        public string Extension => "csv";

        public void Export(IList<T> data, string path)
        {
            var sb = new System.Text.StringBuilder();
            var props = typeof(T).GetProperties();
            sb.AppendLine(string.Join(",", props.Select(p => p.Name)));

            foreach (var item in data)
            {
                if (item == null) continue;
                var values = props.Select(p => $"\"{p.GetValue(item)}\"");
                sb.AppendLine(string.Join(",", values));
            }

            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
        }

        public void Export(IList<object> data, string path)
        {
            Export(data.Cast<T>().ToList(), path);
        }
    }

    // ========== 配置接口 ==========

    public interface IWindowConfig
    {
        void Validate();
        string ToJson();
        void FromJson(string json);
    }

    // ========== 窗口基类 ==========

    public class PlugableWindow<TData, TConfig> : EditorWindow
        where TConfig : class, IWindowConfig, new()
    {
        private List<TData> _allData = new();
        private List<TData> _filteredData = new();
        private TData _selectedItem;
        private TConfig _config;
        private readonly List<IWindowPlugin<TData>> _plugins = new();
        private string _searchText = "";
        private Vector2 _scrollPosition;
        private Vector2 _detailScrollPosition;

        public TConfig Config => _config;
        public TData SelectedItem => _selectedItem;
        public string SearchText => _searchText;
        public IReadOnlyList<TData> AllData => _allData;
        public IReadOnlyList<TData> FilteredData => _filteredData;

        protected virtual string WindowTitle => "Window";
        protected virtual string WindowMenuPath => "Window/Generic";
        protected virtual int DefaultListWidth => 300;
        protected virtual IEnumerable<TData> LoadData() => Enumerable.Empty<TData>();
        protected virtual void DrawDetail(TData item) { }
        protected virtual void DrawListItem(TData item, Rect rect, bool isSelected)
        {
            EditorGUI.LabelField(rect, item?.ToString() ?? "");
        }
        protected virtual bool ContainsSearchText(TData item, string text) 
            => item?.ToString().Contains(text) ?? false;

        public void Initialize(
            IEnumerable<TData> dataSource,
            IEnumerable<IWindowPlugin<TData>> plugins = null)
        {
            titleContent = new GUIContent(WindowTitle);
            _config = new TConfig();
            _config.Validate();

            if (plugins != null)
            {
                foreach (var plugin in plugins)
                {
                    _plugins.Add(plugin);
                }
            }

            _plugins.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            RefreshData();
        }

        private void OnEnable()
        {
            _config ??= new TConfig();
            _config.Validate();
        }

        private void OnGUI()
        {
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scrollScope.scrollPosition;
                DrawToolbar();
                DrawMainContent();
                DrawStatusBar();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("↻", "刷新 (F5)"), EditorStyles.toolbarButton, GUILayout.Width(30)))
                    RefreshData();

                foreach (var plugin in _plugins)
                    plugin.OnToolbarGUI();

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("⚙", EditorStyles.toolbarButton, GUILayout.Width(25)))
                    OnSettingsClicked();
            }
        }

        protected virtual void OnSettingsClicked() { }

        private void DrawMainContent()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(DefaultListWidth));
            foreach (var plugin in _plugins)
                plugin.OnListHeaderGUI();

            foreach (var plugin in _plugins)
                plugin.OnListGUI();

            DrawDefaultList();
            EditorGUILayout.EndVertical();

            DrawResizeDivider();

            EditorGUILayout.BeginVertical();
            if (_selectedItem != null)
            {
                foreach (var plugin in _plugins)
                    plugin.OnDetailHeaderGUI();

                _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);
                foreach (var plugin in _plugins)
                    plugin.OnDetailGUI(_selectedItem);

                DrawDetail(_selectedItem);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("选择一项查看详情", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDefaultList()
        {
            if (_filteredData.Count == 0)
            {
                EditorGUILayout.HelpBox("无数据", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var item in _filteredData)
            {
                var isSelected = item != null && item.Equals(_selectedItem);
                var style = isSelected ? EditorStyles.selectionRect : EditorStyles.label;

                EditorGUILayout.BeginHorizontal(style);
                DrawListItem(item, Rect.zero, isSelected);
                EditorGUILayout.EndHorizontal();

                if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 1)
                {
                    var previous = _selectedItem;
                    _selectedItem = item;
                    OnItemSelected(previous, item);
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawResizeDivider()
        {
            var rect = GUILayoutUtility.GetRect(5, 5, GUILayout.ExpandHeight(true), GUILayout.Width(5));
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
        }

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(20)))
            {
                GUI.color = Color.gray;
                EditorGUILayout.LabelField($"总计: {_allData.Count}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"过滤: {_filteredData.Count}", EditorStyles.miniLabel);
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();

                foreach (var plugin in _plugins)
                    plugin.OnStatusBarGUI();
            }
        }

        public void RefreshData()
        {
            try
            {
                _allData = LoadData().ToList();
                ApplyFilters();

                foreach (var plugin in _plugins)
                    plugin.OnDataLoaded(_allData);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            Repaint();
        }

        private void ApplyFilters()
        {
            _filteredData = _allData.Where(item => PassesFilter(item)).ToList();
            Repaint();
        }

        private bool PassesFilter(TData item)
        {
            if (!string.IsNullOrEmpty(_searchText) && !ContainsSearchText(item, _searchText))
                return false;
            return true;
        }

        public void SetSearchText(string text)
        {
            _searchText = text;
            ApplyFilters();
        }

        private void OnItemSelected(TData previous, TData current)
        {
            foreach (var plugin in _plugins)
                plugin.OnSelectionChanged(previous, current);
            Repaint();
        }

        protected virtual void OnDestroy()
        {
            foreach (var plugin in _plugins)
                plugin.OnDestroy();
        }
    }

    // ========== 窗口插件接口 ==========

    public interface IWindowPlugin
    {
        int Priority { get; }
    }

    public interface IWindowPlugin<TData> : IWindowPlugin
    {
        void OnToolbarGUI();
        void OnListHeaderGUI();
        void OnListGUI();
        void OnDetailHeaderGUI();
        void OnDetailGUI(TData item);
        void OnStatusBarGUI();
        void OnDataLoaded(IList<TData> data);
        void OnSelectionChanged(TData previous, TData current);
        void OnDestroy();
    }

    public abstract class BaseWindowPlugin<TData> : IWindowPlugin<TData>
    {
        public virtual int Priority => 0;
        public virtual void OnToolbarGUI() { }
        public virtual void OnListHeaderGUI() { }
        public virtual void OnListGUI() { }
        public virtual void OnDetailHeaderGUI() { }
        public virtual void OnDetailGUI(TData item) { }
        public virtual void OnStatusBarGUI() { }
        public virtual void OnDataLoaded(IList<TData> data) { }
        public virtual void OnSelectionChanged(TData previous, TData current) { }
        public virtual void OnDestroy() { }
    }
}
#endif
