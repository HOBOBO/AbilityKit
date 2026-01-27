using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 创建节点面板TreeViewItem的创建节点Item实现
    /// </summary>
    public class CreateNodeEntryTreeViewItem : CreateNodeTreeViewItem
    {
        /// <summary>
        /// 创建节点信息
        /// </summary>
        public ICreateNodeHandle createNodeHandle { get; }

        /// <summary>
        /// 是否为收藏节点
        /// </summary>
        public bool isCollection { get; private set; }

        public CreateNodeEntryTreeViewItem(ICreateNodeHandle createNodeHandle, bool isCollection = false)
        {
            this.createNodeHandle = createNodeHandle;
            icon = createNodeHandle.icon;
            this.isCollection = isCollection;
        }
    }
}