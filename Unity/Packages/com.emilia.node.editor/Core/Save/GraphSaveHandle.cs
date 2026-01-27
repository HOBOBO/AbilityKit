using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 保存自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public class GraphSaveHandle
    {
        /// <summary>
        /// 保存前的处理
        /// </summary>
        public virtual void OnSaveBefore(EditorGraphView graphView) { }

        /// <summary>
        /// 保存后的处理
        /// </summary>
        public virtual void OnSaveAfter(EditorGraphView graphView) { }
    }
}