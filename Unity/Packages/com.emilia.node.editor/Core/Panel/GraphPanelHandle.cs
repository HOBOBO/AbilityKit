using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 面板自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class GraphPanelHandle
    {
        /// <summary>
        /// 初始化时加载面板
        /// </summary>
        public virtual void LoadPanel(EditorGraphView graphView, GraphPanelSystem system) { }
    }
}