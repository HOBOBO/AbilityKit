using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点上下文
    /// </summary>
    public class CreateNodeContext
    {
        /// <summary>
        /// 鼠标位置
        /// </summary>
        public Vector2 screenMousePosition;

        /// <summary>
        /// 节点菜单
        /// </summary>
        public GraphCreateNodeMenu nodeMenu;

        /// <summary>
        /// 收集创建的节点
        /// </summary>
        public ICreateNodeCollector nodeCollector;
    }
}