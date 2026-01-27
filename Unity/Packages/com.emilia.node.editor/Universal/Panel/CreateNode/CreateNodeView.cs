using Emilia.Node.Editor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 创建节点面板
    /// </summary>
    public class CreateNodeView : GraphPanel
    {
        protected NodeCollectionSetting collectionSetting;
        protected CreateNodeViewState createNodeViewState;

        protected SearchField searchField;
        protected TreeViewState treeViewState;
        protected CreateNodeTreeView createNodeTreeView;

        public CreateNodeView()
        {
            name = nameof(CreateNodeView);

            IMGUIContainer container = new(OnTreeGUI);
            container.name = $"{nameof(CreateNodeView)}-TreeView";

            Add(container);
        }

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);

            graphView.graphLocalSettingSystem.onTypeSettingChanged += OnReadSetting;

            searchField = new SearchField();
            schedule.Execute(OnInitialize).ExecuteLater(1);
        }

        protected void OnInitialize()
        {
            collectionSetting = NodeCollectionSetting.Get(graphView);
            createNodeViewState = CreateNodeViewState.Get(graphView);

            treeViewState = new TreeViewState();

            treeViewState.expandedIDs.AddRange(createNodeViewState.expandedIDs);

            createNodeTreeView = new CreateNodeTreeView(graphView, treeViewState);

            string saveKey = CreateNodeViewState.CreateNodeViewStateSaveKey;
            if (graphView.graphLocalSettingSystem.HasTypeSetting(saveKey) == false) createNodeTreeView.SetExpandAll();

            createNodeTreeView.ReloadSetting(createNodeViewState, this.collectionSetting);
        }

        protected void OnReadSetting()
        {
            collectionSetting = NodeCollectionSetting.Get(graphView);
            createNodeViewState = CreateNodeViewState.Get(graphView);
            createNodeTreeView.ReloadSetting(createNodeViewState, this.collectionSetting);
        }

        public override void Dispose()
        {
            base.Dispose();
            graphView.graphLocalSettingSystem.onTypeSettingChanged -= OnReadSetting;

            treeViewState = null;
            createNodeTreeView = null;
        }

        protected void OnTreeGUI()
        {
            const float IntervalWidth = 5;
            const float ToolbarHeight = 24;
            const float SearchFieldHeight = 20;

            if (float.IsNaN(layout.width) || float.IsNaN(layout.height)) return;

            Rect rect = new(0.0f, 0.0f, layout.width, layout.height);

            if (createNodeTreeView != null)
            {
                Rect searchRect = rect;
                searchRect.x += IntervalWidth;
                searchRect.y += (ToolbarHeight - SearchFieldHeight) / 2;
                searchRect.height = SearchFieldHeight;
                searchRect.width -= IntervalWidth * 2;

                createNodeTreeView.searchString = searchField.OnToolbarGUI(searchRect, createNodeTreeView.searchString);

                Rect treeRect = rect;
                treeRect.y += SearchFieldHeight;
                treeRect.height -= SearchFieldHeight;

                createNodeTreeView.OnGUI(treeRect);
            }
        }
    }
}