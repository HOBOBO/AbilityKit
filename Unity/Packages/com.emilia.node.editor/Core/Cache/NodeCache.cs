namespace Emilia.Node.Editor
{
    /// <summary>
    /// 节点缓存数据结构
    /// </summary>
    public class NodeCache
    {
        /// <summary>
        /// 节点数据
        /// </summary>
        public object nodeData;

        /// <summary>
        /// 节点视图
        /// </summary>
        public IEditorNodeView nodeView;

        public NodeCache(object nodeData, IEditorNodeView nodeView)
        {
            this.nodeData = nodeData;
            this.nodeView = nodeView;
        }
    }
}