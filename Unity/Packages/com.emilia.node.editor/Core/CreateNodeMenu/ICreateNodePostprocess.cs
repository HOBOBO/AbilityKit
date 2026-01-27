namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点后处理接口
    /// </summary>
    public interface ICreateNodePostprocess
    {
        /// <summary>
        /// 后处理
        /// </summary>
        void Postprocess(EditorGraphView graphView, IEditorNodeView nodeView, CreateNodeContext createNodeContext);
    }
}