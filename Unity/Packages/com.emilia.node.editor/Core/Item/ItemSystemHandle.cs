using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Item自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class ItemSystemHandle
    {
        /// <summary>
        /// 创建Item视图处理
        /// </summary>
        public virtual void OnCreateItem(EditorGraphView graphView, IEditorItemView itemView) { }
    }
}