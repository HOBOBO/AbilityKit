#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Trace.Editor.Windows
{
    /// <summary>
    /// 溯源树可视化窗口：左栏根节点列表 + 右栏树图与节点详情。
    /// 原先基于已退役的 PlugableWindow 框架，现为独立 EditorWindow。
    /// </summary>
    public class TraceTreeWindow : EditorWindow
    {
        private const float ListWidth = 220f;

        private TraceTreeViewModel _viewModel;
        private TreeVisualizationPlugin _treePlugin;
        private NodeDetailPlugin _detailPlugin;
        private readonly TraceTreeConfig _config = new TraceTreeConfig();
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private TraceRootViewData _selectedItem;

        [MenuItem("Window/AbilityKit/Trace Tree")]
        public static void ShowWindow()
        {
            var window = GetWindow<TraceTreeWindow>();
            window.titleContent = new GUIContent("Trace Tree");
            window.Show();
        }

        private void OnEnable()
        {
            if (_viewModel == null)
            {
                _viewModel = new TraceTreeViewModel();
                _viewModel.SetRegistryProvider(DefaultTraceRegistryProvider.Instance);
            }

            _treePlugin = new TreeVisualizationPlugin(_viewModel);
            _detailPlugin = new NodeDetailPlugin(_viewModel);
            _config.Validate();
            _viewModel.Refresh();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawMain();
            DrawStatusBar();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(new GUIContent("↻", "刷新 (F5)"), EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _viewModel.Refresh();
                Repaint();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("⚙", EditorStyles.toolbarButton, GUILayout.Width(25)))
                ShowSettingsMenu();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMain()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
            DrawList();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            DrawDetail();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (var item in _viewModel.ActiveRoots)
            {
                var isSelected = _selectedItem != null && _selectedItem.RootId == item.RootId;
                var style = isSelected ? EditorStyles.selectionRect : EditorStyles.label;
                EditorGUILayout.BeginHorizontal(style);
                EditorGUILayout.LabelField(item.KindName + " #" + item.RootId);
                EditorGUILayout.EndHorizontal();

                var rect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 1 && rect.Contains(Event.current.mousePosition))
                {
                    SelectRoot(item);
                    Event.current.Use();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void SelectRoot(TraceRootViewData item)
        {
            _selectedItem = item;
            _viewModel.SelectRoot(item.RootId);
            _treePlugin.ResetSelection();
        }

        private void DrawDetail()
        {
            if (_selectedItem == null)
            {
                EditorGUILayout.HelpBox("选择一项查看详情", MessageType.Info);
                return;
            }

            _detailPlugin.DrawHeader();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            _treePlugin.Draw(_selectedItem);
            _detailPlugin.Draw(_selectedItem);
            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
            GUI.color = Color.gray;
            EditorGUILayout.LabelField($"Active: {_viewModel.ActiveRoots.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Nodes: {_viewModel.TotalNodeCount}", EditorStyles.miniLabel);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ShowSettingsMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Auto Refresh"), _config.AutoRefresh, () =>
            {
                _config.AutoRefresh = !_config.AutoRefresh;
            });

            menu.AddItem(new GUIContent("Show Ended Nodes"), _config.ShowEndedNodes, () =>
            {
                _config.ShowEndedNodes = !_config.ShowEndedNodes;
                _viewModel.Refresh();
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Reset Zoom"), false, () =>
            {
                _config.ZoomLevel = 1.0f;
            });

            menu.ShowAsContext();
        }
    }
}
#endif
