using System.Collections.Generic;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点收集器
    /// </summary>
    public interface ICreateNodeCollector
    {
        /// <summary>
        /// 收集节点信息
        /// </summary>
        /// <param name="allNodeInfos">所有节点</param>
        /// <returns>可创建的节点</returns>
        List<CreateNodeInfo> Collect(List<MenuNodeInfo> allNodeInfos);
    }
}