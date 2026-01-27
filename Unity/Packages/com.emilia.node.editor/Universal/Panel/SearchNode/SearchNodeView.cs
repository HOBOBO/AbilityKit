using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 搜索节点目标
    /// </summary>
    public class SearchNodeView : GraphPanel
    {
        protected SearchField searchField;
        protected TreeViewState treeViewState;
        protected SearchNodeTreeView searchNodeTreeView;

        protected List<UniversalEditorNodeView> dimNodeViews = new();

        public SearchNodeView()
        {
            name = nameof(SearchNodeView);

            IMGUIContainer container = new(OnTreeGUI);
            container.name = $"{nameof(SearchNodeView)}-TreeView";

            Add(container);
        }

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);

            searchField = new SearchField();

            schedule.Execute(() => {
                treeViewState = new TreeViewState();
                searchNodeTreeView = new SearchNodeTreeView(graphView, treeViewState);
                searchNodeTreeView.Reload();
            }).ExecuteLater(1);
        }

        public override void Dispose()
        {
            base.Dispose();
            treeViewState = null;
            searchNodeTreeView = null;
            ClearDim();
        }

        protected void OnTreeGUI()
        {
            const float IntervalWidth = 5;
            const float ToolbarHeight = 24;
            const float SearchFieldHeight = 20;

            if (float.IsNaN(layout.width) || float.IsNaN(layout.height)) return;

            Rect rect = new(0.0f, 0.0f, layout.width, layout.height);

            if (searchNodeTreeView != null)
            {
                Rect searchRect = rect;
                searchRect.x += IntervalWidth;
                searchRect.y += (ToolbarHeight - SearchFieldHeight) / 2;
                searchRect.height = SearchFieldHeight;
                searchRect.width -= IntervalWidth * 2;

                EditorGUI.BeginChangeCheck();
                searchNodeTreeView.searchString = searchField.OnToolbarGUI(searchRect, searchNodeTreeView.searchString);
                if (EditorGUI.EndChangeCheck()) RefreshDim();

                Rect treeRect = rect;
                treeRect.y += 20;

                searchNodeTreeView.OnGUI(treeRect);
            }
        }

        protected void RefreshDim()
        {
            ClearDim();

            int count = this.graphView.nodeViews.Count;
            for (int i = 0; i < count; i++)
            {
                IEditorNodeView nodeView = this.graphView.nodeViews[i];
                UniversalEditorNodeView universalNodeView = nodeView as UniversalEditorNodeView;
                if (universalNodeView == null) continue;

                string displayName = ObjectDescriptionUtility.GetDescription(nodeView.asset);
                if (string.IsNullOrEmpty(displayName)) displayName = nodeView.asset.name;

                if (SearchUtility.Matching(displayName, searchNodeTreeView.searchString)) continue;

                universalNodeView.SetDisabled();
                dimNodeViews.Add(universalNodeView);
            }
        }

        protected void ClearDim()
        {
            int count = dimNodeViews.Count;
            for (int i = 0; i < count; i++)
            {
                UniversalEditorNodeView nodeView = dimNodeViews[i];
                if (nodeView == null) continue;
                nodeView.ClearDisabled();
            }

            dimNodeViews.Clear();
        }
    }
}