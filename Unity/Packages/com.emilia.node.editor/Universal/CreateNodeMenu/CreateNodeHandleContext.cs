using System;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点适配器上下文
    /// </summary>
    public struct CreateNodeHandleContext
    {
        /// <summary>
        /// 节点资产类型
        /// </summary>
        public Type nodeType;
        
        /// <summary>
        /// 编辑器节点资产类型
        /// </summary>
        public Type defaultEditorNodeType;
    }
}