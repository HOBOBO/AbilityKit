using System.Collections.Generic;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// EdgeView扩展实用方法
    /// </summary>
    public static class IEditorEdgeViewExtension
    {
        /// <summary>
        /// 根据Id获取NodeView
        /// </summary>
        public static IEditorNodeView GetEditorNodeView(this IEditorEdgeView edgeView, string id) => edgeView.graphView.graphElementCache.nodeViewById.GetValueOrDefault(id);

        /// <summary>
        /// 根据Id获取EdgeView
        /// </summary>
        public static IEditorEdgeView GetEditorEdgeView(this IEditorEdgeView edgeView, string id) => edgeView.graphView.graphElementCache.edgeViewById.GetValueOrDefault(id);

        /// <summary>
        /// 根据Id获取ItemView
        /// </summary>
        public static IEditorItemView GetEditorItemView(this IEditorEdgeView edgeView, string id) => edgeView.graphView.graphElementCache.itemViewById.GetValueOrDefault(id);

        /// <summary>
        /// 获取Output节点Id
        /// </summary>
        public static string GetOutputNodeId(this IEditorEdgeView edgeView) => edgeView.outputPortView.master.asset.id;

        /// <summary>
        /// 获取Input节点Id
        /// </summary>
        public static string GetInputNodeId(this IEditorEdgeView edgeView) => edgeView.inputPortView.master.asset.id;
    }
}