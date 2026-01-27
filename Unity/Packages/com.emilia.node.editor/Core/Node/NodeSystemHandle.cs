using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 节点系统自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class NodeSystemHandle
    {
        /// <summary>
        /// 创建节点时的处理
        /// </summary>
        public virtual void OnCreateNode(EditorGraphView graphView, IEditorNodeView editorNodeView) { }
    }
}