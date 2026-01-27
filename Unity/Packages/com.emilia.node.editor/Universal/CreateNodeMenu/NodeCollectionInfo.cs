namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 节点收藏信息
    /// </summary>
    public class NodeCollectionInfo
    {
        /// <summary>
        /// 节点名称
        /// </summary>
        public string nodeName { get; private set; }

        /// <summary>
        /// 节点路径
        /// </summary>
        public string nodePath { get; private set; }

        public NodeCollectionInfo(string nodePath)
        {
            this.nodePath = nodePath;
            nodeName = nodePath.Split('/').Length > 0 ? nodePath.Split('/')[nodePath.Split('/').Length - 1] : "";
        }
    }
}