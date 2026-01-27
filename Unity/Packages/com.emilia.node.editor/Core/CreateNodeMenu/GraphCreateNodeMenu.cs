using System.Collections.Generic;
using Emilia.Kit;
using UnityEditor.Experimental.GraphView;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点菜单系统
    /// </summary>
    public class GraphCreateNodeMenu : BasicGraphViewModule
    {
        private CreateNodeMenuHandle handle;

        private List<ICreateNodeHandle> _createNodeHandleCacheList = new();

        /// <summary>
        /// 缓存的创建节点Handle
        /// </summary>
        public IReadOnlyList<ICreateNodeHandle> createNodeHandleCacheList => _createNodeHandleCacheList;

        public override int order => 1300;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            _createNodeHandleCacheList.Clear();

            handle = EditorHandleUtility.CreateHandle<CreateNodeMenuHandle>(graphView.graphAsset.GetType());
            handle?.Initialize(graphView);
        }

        public override void AllModuleInitializeSuccess()
        {
            base.AllModuleInitializeSuccess();
            handle?.InitializeCache(this.graphView, _createNodeHandleCacheList);

            this.graphView.nodeCreationRequest = OnNodeCreationRequest;
        }

        private void OnNodeCreationRequest(NodeCreationContext nodeCreationContext)
        {
            CreateNodeContext createNodeContext = new();
            createNodeContext.screenMousePosition = nodeCreationContext.screenMousePosition;
            ShowCreateNodeMenu(createNodeContext);
        }

        /// <summary>
        /// 显示创建节点菜单
        /// </summary>
        public void ShowCreateNodeMenu(CreateNodeContext context)
        {
            if (this.handle == null) return;
            context.nodeMenu = this;
            if (context.nodeCollector == null) context.nodeCollector = this.handle.GetDefaultFilter(this.graphView);
            handle.ShowCreateNodeMenu(graphView, context);
        }

        public override void Dispose()
        {
            if (this.graphView == null) return;

            _createNodeHandleCacheList.Clear();

            if (handle != null)
            {
                handle.Dispose(this.graphView);
                handle = null;
            }

            base.Dispose();
        }
    }
}