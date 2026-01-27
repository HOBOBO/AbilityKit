using System.Collections.Generic;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Node表现元素拓展实用函数
    /// </summary>
    public static class IEditorNodeViewExtension
    {
        /// <summary>
        /// 根据Id获取NodeView
        /// </summary>
        public static IEditorNodeView GetEditorNodeView(this IEditorNodeView nodeView, string id) => nodeView.graphView.graphElementCache.nodeViewById.GetValueOrDefault(id);

        /// <summary>
        /// 根据Id获取EdgeView
        /// </summary>
        public static IEditorEdgeView GetEditorEdgeView(this IEditorNodeView nodeView, string id) => nodeView.graphView.graphElementCache.edgeViewById.GetValueOrDefault(id);

        /// <summary>
        /// 根据Id获取ItemView
        /// </summary>
        public static IEditorItemView GetEditorItemView(this IEditorNodeView nodeView, string id) => nodeView.graphView.graphElementCache.itemViewById.GetValueOrDefault(id);

        /// <summary>
        /// 获取所有Output节点
        /// </summary>
        public static List<IEditorNodeView> GetOutputNodeViews(this IEditorNodeView editorNodeView)
        {
            List<IEditorNodeView> outputNodeViews = new();

            for (var i = 0; i < editorNodeView.graphView.edgeViews.Count; i++)
            {
                IEditorEdgeView edgeView = editorNodeView.graphView.edgeViews[i];

                if (edgeView.GetOutputNodeId() != editorNodeView.asset.id) continue;

                string inputNodeId = edgeView.GetInputNodeId();

                IEditorNodeView outputNodeView = editorNodeView.graphView.graphElementCache.nodeViewById.GetValueOrDefault(inputNodeId);
                if (outputNodeView == null) continue;

                outputNodeViews.Add(outputNodeView);
            }

            return outputNodeViews;
        }

        /// <summary>
        /// 获取所有Input节点
        /// </summary>
        public static List<IEditorNodeView> GetInputNodeViews(this IEditorNodeView editorNodeView)
        {
            List<IEditorNodeView> inputNodeViews = new();

            for (var i = 0; i < editorNodeView.graphView.edgeViews.Count; i++)
            {
                IEditorEdgeView edgeView = editorNodeView.graphView.edgeViews[i];

                if (edgeView.GetInputNodeId() != editorNodeView.asset.id) continue;

                string outputNodeId = edgeView.GetOutputNodeId();

                IEditorNodeView inputNodeView = editorNodeView.GetEditorNodeView(outputNodeId);
                if (inputNodeView == null) continue;

                inputNodeViews.Add(inputNodeView);
            }

            return inputNodeViews;
        }

        /// <summary>
        /// 获取所有Output节点
        /// </summary>
        public static List<IEditorNodeView> GetAllOutputNodeViews(this IEditorNodeView editorNodeView)
        {
            List<IEditorNodeView> outputNodeViews = new();

            for (var i = 0; i < editorNodeView.graphView.edgeViews.Count; i++)
            {
                IEditorEdgeView edgeView = editorNodeView.graphView.edgeViews[i];

                if (edgeView.GetOutputNodeId() != editorNodeView.asset.id) continue;

                string inputNodeId = edgeView.GetInputNodeId();

                IEditorNodeView outputNodeView = editorNodeView.GetEditorNodeView(inputNodeId);
                if (outputNodeView == null) continue;

                outputNodeViews.Add(outputNodeView);
                outputNodeViews.AddRange(outputNodeView.GetAllOutputNodeViews());
            }

            return outputNodeViews;
        }

        /// <summary>
        /// 获取所有Input节点
        /// </summary>
        public static List<IEditorNodeView> GetAllInputNodeViews(this IEditorNodeView editorNodeView)
        {
            List<IEditorNodeView> inputNodeViews = new();

            for (var i = 0; i < editorNodeView.graphView.edgeViews.Count; i++)
            {
                IEditorEdgeView edgeView = editorNodeView.graphView.edgeViews[i];

                if (edgeView.GetInputNodeId() != editorNodeView.asset.id) continue;

                string outputNodeId = edgeView.GetOutputNodeId();

                IEditorNodeView inputNodeView = editorNodeView.GetEditorNodeView(outputNodeId);
                if (inputNodeView == null) continue;

                inputNodeViews.Add(inputNodeView);
                inputNodeViews.AddRange(inputNodeView.GetAllInputNodeViews());
            }

            return inputNodeViews;
        }

        /// <summary>
        /// 获取所有Input EdgeViews
        /// </summary>
        public static List<IEditorEdgeView> GetInputEdgeViews(this IEditorNodeView editorNodeView)
        {
            List<IEditorEdgeView> inputEdgeViews = new();

            for (var i = 0; i < editorNodeView.graphView.edgeViews.Count; i++)
            {
                IEditorEdgeView edgeView = editorNodeView.graphView.edgeViews[i];

                if (edgeView.GetInputNodeId() != editorNodeView.asset.id) continue;

                inputEdgeViews.Add(edgeView);
            }

            return inputEdgeViews;
        }

        /// <summary>
        /// 获取所有Output EdgeViews
        /// </summary>
        public static List<IEditorEdgeView> GetOutputEdgeViews(this IEditorNodeView editorNodeView)
        {
            List<IEditorEdgeView> outputEdgeViews = new();

            for (var i = 0; i < editorNodeView.graphView.edgeViews.Count; i++)
            {
                IEditorEdgeView edgeView = editorNodeView.graphView.edgeViews[i];

                if (edgeView.GetOutputNodeId() != editorNodeView.asset.id) continue;

                outputEdgeViews.Add(edgeView);
            }

            return outputEdgeViews;
        }

        /// <summary>
        /// 获取可连接的IEditorPortView
        /// </summary>
        public static bool GetCanConnectPort(this IEditorNodeView editorNodeView, IEditorEdgeView edgeView, out List<IEditorPortView> canConnectInput, out List<IEditorPortView> canConnectOutput)
        {
            canConnectInput = new List<IEditorPortView>();
            canConnectOutput = new List<IEditorPortView>();

            int portAmount = editorNodeView.portViews.Count;
            for (int i = 0; i < portAmount; i++)
            {
                IEditorPortView portView = editorNodeView.portViews[i];

                if (portView.portDirection == EditorPortDirection.Input || portView.portDirection == EditorPortDirection.Any)
                {
                    bool canConnect = editorNodeView.graphView.connectSystem.CanConnect(portView, edgeView.outputPortView);
                    if (canConnect) canConnectInput.Add(portView);
                }

                if (portView.portDirection == EditorPortDirection.Output || portView.portDirection == EditorPortDirection.Any)
                {
                    bool canConnect = editorNodeView.graphView.connectSystem.CanConnect(edgeView.inputPortView, portView);
                    if (canConnect) canConnectOutput.Add(portView);
                }
            }

            canConnectInput.Sort(SortPortView);
            canConnectOutput.Sort(SortPortView);

            return canConnectInput.Count > 0 && canConnectOutput.Count > 0;
        }

        /// <summary>
        /// 获取可连接的IEditorPortView
        /// </summary>
        public static List<IEditorPortView> GetCanConnectPort(this IEditorNodeView editorNodeView, IEditorPortView portView)
        {
            List<IEditorPortView> canConnectList = new();

            EditorPortDirection direction = portView.portDirection;

            int portAmount = editorNodeView.portViews.Count;
            for (int i = 0; i < portAmount; i++)
            {
                IEditorPortView port = editorNodeView.portViews[i];

                bool canConnect = false;

                switch (direction)
                {
                    case EditorPortDirection.Input:
                        canConnect = editorNodeView.graphView.connectSystem.CanConnect(port, portView);
                        break;
                    case EditorPortDirection.Output:
                        canConnect = editorNodeView.graphView.connectSystem.CanConnect(portView, port);
                        break;
                    case EditorPortDirection.Any:
                        canConnect = editorNodeView.graphView.connectSystem.CanConnect(port, portView)
                                     || editorNodeView.graphView.connectSystem.CanConnect(portView, port);
                        break;
                }

                if (canConnect) canConnectList.Add(port);
            }

            return canConnectList;
        }

        private static int SortPortView(IEditorPortView a, IEditorPortView b)
        {
            if (a == null && b == null) return b.info.order.CompareTo(a.info.order);
            if (a == null) return 1;
            if (b == null) return -1;

            return b.info.priority.CompareTo(a.info.priority);
        }
    }
}