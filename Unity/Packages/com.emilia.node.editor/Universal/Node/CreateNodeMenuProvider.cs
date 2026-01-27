using System.Collections.Generic;
using Emilia.Node.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 创建节点菜单（ISearchWindowProvider）
    /// </summary>
    public class CreateNodeMenuProvider : ScriptableObject, ISearchWindowProvider
    {
        protected EditorGraphView editorGraphView;
        protected CreateNodeContext createNodeContext;
        protected IUniversalCreateNodeMenuInfoProvider infoProvider;

        public void Initialize(EditorGraphView graphView, CreateNodeContext createNodeContext, IUniversalCreateNodeMenuInfoProvider createNodeMenuInfoProvider)
        {
            this.editorGraphView = graphView;
            this.createNodeContext = createNodeContext;
            this.infoProvider = createNodeMenuInfoProvider;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new();
            tree.Add(new SearchTreeGroupEntry(new GUIContent(infoProvider.GetTitle())));
            infoProvider.CreateNodeTree(createNodeContext, (info) => CreateGroup(tree, info), (menuInfo) => CreateItem(tree, menuInfo));

            return tree;
        }

        protected void CreateGroup(List<SearchTreeEntry> tree, CreateNodeMenuItem menuInfo)
        {
            tree.Add(new SearchTreeGroupEntry(new GUIContent(menuInfo.title), menuInfo.level));
        }

        protected void CreateItem(List<SearchTreeEntry> tree, CreateNodeMenuItem menuInfo)
        {
            tree.Add(new SearchTreeEntry(new GUIContent(menuInfo.title, menuInfo.info.menuInfo.icon)) {
                level = menuInfo.level,
                userData = menuInfo.info
            });
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            CreateNodeInfo createNodeInfo = (CreateNodeInfo) SearchTreeEntry.userData;
            return infoProvider.CreateNode(createNodeInfo, createNodeContext);
        }
    }
}