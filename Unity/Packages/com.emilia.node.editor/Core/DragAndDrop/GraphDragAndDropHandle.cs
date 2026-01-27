using Emilia.Kit;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 自定义拖拽处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class GraphDragAndDropHandle
    {
        /// <summary>
        /// 拖拽更新回调
        /// </summary>
        public virtual void DragUpdatedCallback(EditorGraphView graphView, DragUpdatedEvent evt) { }

        /// <summary>
        /// 拖拽执行回调
        /// </summary>
        public virtual void DragPerformedCallback(EditorGraphView graphView, DragPerformEvent evt) { }
    }
}