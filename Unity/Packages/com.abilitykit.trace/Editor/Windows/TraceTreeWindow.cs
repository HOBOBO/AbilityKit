#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AbilityKit.Editor.Framework;

namespace AbilityKit.Trace.Editor.Windows
{
    /// <summary>
    /// 溯源树可视化窗口 - 使用 PlugableWindow 框架
    /// </summary>
    public class TraceTreeWindow : PlugableWindow<TraceRootViewData, TraceTreeConfig>
    {
        private TraceTreeViewModel _viewModel;

        protected override string WindowTitle => "Trace Tree";
        protected override int DefaultListWidth => 220;

        [MenuItem("Window/AbilityKit/Trace Tree")]
        public static void ShowWindow()
        {
            var window = CreateInstance<TraceTreeWindow>();
            window.InitializeWindow();
            window.Show();
        }

        private void InitializeWindow()
        {
            _viewModel = new TraceTreeViewModel();
            _viewModel.SetRegistryProvider(DefaultTraceRegistryProvider.Instance);

            titleContent = new GUIContent(WindowTitle);

            var plugins = new List<IWindowPlugin<TraceRootViewData>>
            {
                new TreeVisualizationPlugin(_viewModel),
                new NodeDetailPlugin(_viewModel),
                new StatisticsPlugin(_viewModel)
            };

            Initialize(_viewModel.ActiveRoots, plugins);
        }

        private void OnEnable()
        {
            if (_viewModel == null)
            {
                _viewModel = new TraceTreeViewModel();
                _viewModel.SetRegistryProvider(DefaultTraceRegistryProvider.Instance);
            }
        }

        protected override IEnumerable<TraceRootViewData> LoadData()
        {
            _viewModel.Refresh();
            return _viewModel.ActiveRoots;
        }

        protected override void DrawDetail(TraceRootViewData item)
        {
            // 详情区域由插件绘制
        }

        protected override void DrawListItem(TraceRootViewData item, Rect rect, bool isSelected)
        {
            // 列表项由列表插件绘制
        }

        protected override bool ContainsSearchText(TraceRootViewData item, string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            return item.RootId.ToString().Contains(text) ||
                   item.KindName.Contains(text, System.StringComparison.OrdinalIgnoreCase);
        }

        protected override void OnSettingsClicked()
        {
            ShowSettingsMenu();
        }

        private void ShowSettingsMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Auto Refresh"), Config.AutoRefresh, () =>
            {
                Config.AutoRefresh = !Config.AutoRefresh;
            });

            menu.AddItem(new GUIContent("Show Ended Nodes"), Config.ShowEndedNodes, () =>
            {
                Config.ShowEndedNodes = !Config.ShowEndedNodes;
                _viewModel.Refresh();
                RefreshData();
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Reset Zoom"), false, () =>
            {
                Config.ZoomLevel = 1.0f;
            });

            menu.ShowAsContext();
        }
    }

    /// <summary>
    /// 统计信息插件 - 显示节点统计
    /// </summary>
    public class StatisticsPlugin : BaseWindowPlugin<TraceRootViewData>
    {
        private TraceTreeViewModel _viewModel;

        public StatisticsPlugin(TraceTreeViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override int Priority => 100;

        public override void OnStatusBarGUI()
        {
            GUI.color = Color.gray;
            EditorGUILayout.LabelField($"Active: {_viewModel.ActiveRoots.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Nodes: {_viewModel.TotalNodeCount}", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }
    }
}
#endif
