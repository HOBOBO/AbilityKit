using System.Collections.Generic;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Item表现元素拓展实用函数
    /// </summary>
    public static class IEditorItemViewExtension
    {
        /// <summary>
        /// 根据Id获取NodeView
        /// </summary>
        public static IEditorNodeView GetEditorNodeView(this IEditorItemView itemView, string id) => itemView.graphView.graphElementCache.nodeViewById.GetValueOrDefault(id);

        /// <summary>
        /// 根据Id获取EdgeView
        /// </summary>
        public static IEditorEdgeView GetEditorEdgeView(this IEditorItemView itemView, string id) => itemView.graphView.graphElementCache.edgeViewById.GetValueOrDefault(id);

        /// <summary>
        /// 根据Id获取ItemView
        /// </summary>
        public static IEditorItemView GetEditorItemView(this IEditorItemView itemView, string id) => itemView.graphView.graphElementCache.itemViewById.GetValueOrDefault(id);
    }
}