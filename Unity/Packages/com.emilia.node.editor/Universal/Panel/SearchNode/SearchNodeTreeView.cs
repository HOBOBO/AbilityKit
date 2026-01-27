using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Node.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.IMGUI.Controls;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 搜索节点面板TreeView实现
    /// </summary>
    public class SearchNodeTreeView : TreeView
    {
        protected EditorGraphView graphView;
        protected Dictionary<int, IEditorNodeView> nodeViews = new();

        public SearchNodeTreeView(EditorGraphView graphView, TreeViewState state) : base(state)
        {
            baseIndent = 10;
            this.graphView = graphView;
        }

        protected override TreeViewItem BuildRoot() => new() {id = 0, depth = -1, displayName = "Root"};

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            List<TreeViewItem> treeViewItems = new();

            nodeViews.Clear();

            if (hasSearch == false) AddNormalItem(treeViewItems, root);
            else AddSearchItem(treeViewItems, root);

            return treeViewItems;
        }

        protected void AddNormalItem(List<TreeViewItem> treeViewItems, TreeViewItem root)
        {
            int count = this.graphView.nodeViews.Count;
            for (int i = 0; i < count; i++)
            {
                IEditorNodeView nodeView = this.graphView.nodeViews[i];
                int id = nodeView.asset.id.GetHashCode();
                nodeViews.Add(id, nodeView);

                string displayName = ObjectDescriptionUtility.GetDescription(nodeView.asset);
                if (string.IsNullOrEmpty(displayName)) displayName = nodeView.asset.name;
                
                displayName = RemoveRichTextLabel(displayName);

                TreeViewItem item = new(id, 0, displayName);

                root.AddChild(item);
                treeViewItems.Add(item);
            }
        }

        protected void AddSearchItem(List<TreeViewItem> treeViewItems, TreeViewItem root)
        {
            List<(TreeViewItem, int)> collects = new();

            int count = this.graphView.nodeViews.Count;
            for (int i = 0; i < count; i++)
            {
                IEditorNodeView nodeView = this.graphView.nodeViews[i];

                int id = nodeView.asset.id.GetHashCode();
                nodeViews.Add(id, nodeView);

                string displayName = ObjectDescriptionUtility.GetDescription(nodeView.asset);
                if (string.IsNullOrEmpty(displayName)) displayName = nodeView.asset.name;
                
                displayName = RemoveRichTextLabel(displayName);

                int score = SearchUtility.SmartSearch(displayName, searchString);
                if (score == 0) continue;

                TreeViewItem item = new(id, 0, displayName);
                collects.Add((item, score));
            }

            collects.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            foreach (var collect in collects)
            {
                root.AddChild(collect.Item1);
                treeViewItems.Add(collect.Item1);
            }
        }

        protected string RemoveRichTextLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            int startIndex = 0;
            while ((startIndex = text.IndexOf('<', startIndex)) != -1)
            {
                int endIndex = text.IndexOf('>', startIndex);
                if (endIndex == -1) break;

                text = text.Remove(startIndex, endIndex - startIndex + 1);
            }

            return text;
        }

        protected override void SingleClickedItem(int id)
        {
            base.SingleClickedItem(id);
            IEditorNodeView nodeView = nodeViews.GetValueOrDefault(id);
            if (nodeView == null) return;

            this.graphView.SetSelection(new List<ISelectable> {nodeView.element});
            this.graphView.FrameSelection();
        }
    }
}