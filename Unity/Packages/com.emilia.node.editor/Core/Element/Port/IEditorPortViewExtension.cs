using System.Collections.Generic;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Port表现元素拓展实用函数
    /// </summary>
    public static class IEditorPortViewExtension
    {
        /// <summary>
        /// 根据Id获取NodeView
        /// </summary>
        public static IEditorNodeView GetEditorNodeView(this IEditorPortView protView, string id) => protView.master.graphView.graphElementCache.nodeViewById.GetValueOrDefault(id);

        /// <summary>
        /// 根据Id获取EdgeView
        /// </summary>
        public static IEditorEdgeView GetEditorEdgeView(this IEditorPortView protView, string id) => protView.master.graphView.graphElementCache.edgeViewById.GetValueOrDefault(id);

        /// <summary>
        /// 根据Id获取ItemView
        /// </summary>
        public static IEditorItemView GetEditorItemView(this IEditorPortView protView, string id) => protView.master.graphView.graphElementCache.itemViewById.GetValueOrDefault(id);

        /// <summary>
        /// 获取端口的所有连接
        /// </summary>
        public static List<IEditorEdgeView> GetEdges(this IEditorPortView port)
        {
            List<IEditorEdgeView> edges = new();

            EditorGraphView graphView = port.master.graphView;

            int edgeAmount = graphView.edgeViews.Count;
            for (int i = 0; i < edgeAmount; i++)
            {
                IEditorEdgeView edge = graphView.edgeViews[i];

                bool hsaValidInput = edge.inputPortView?.master?.asset;
                bool hsaValidOutput = edge.outputPortView?.master?.asset;

                if (hsaValidInput == false || hsaValidOutput == false) continue;

                bool hasInputNode = edge.inputPortView.master.asset.id == port.master.asset.id;
                bool hasOutputNode = edge.outputPortView.master.asset.id == port.master.asset.id;

                bool hasInputPort = edge.inputPortView.info.id == port.info.id;
                bool hasOutputPort = edge.outputPortView.info.id == port.info.id;

                bool hasInput = hasInputNode && hasInputPort;
                bool hasOutput = hasOutputNode && hasOutputPort;

                if (hasInput || hasOutput) edges.Add(edge);
            }

            return edges;
        }

        /// <summary>
        /// 获取端口的所有连接的Asset
        /// </summary>
        public static List<EditorEdgeAsset> GetEdgeAssets(this IEditorPortView editorPortView)
        {
            List<EditorEdgeAsset> edgeAssets = new();

            int amount = editorPortView.master.graphView.edgeViews.Count;
            for (int i = 0; i < amount; i++)
            {
                IEditorEdgeView edgeView = editorPortView.master.graphView.edgeViews[i];

                if (edgeView.asset.inputNodeId == editorPortView.master.asset.id && edgeView.asset.inputPortId == editorPortView.info.id)
                {
                    edgeAssets.Add(edgeView.asset);
                    continue;
                }

                if (edgeView.asset.outputNodeId == editorPortView.master.asset.id && edgeView.asset.outputPortId == editorPortView.info.id)
                {
                    edgeAssets.Add(edgeView.asset);
                    continue;
                }
            }

            return edgeAssets;
        }
    }
}