using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 用于EditorNodeView指定EditorNodeAsset
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EditorNodeAttribute : Attribute
    {
        /// <summary>
        /// 节点资产类型
        /// </summary>
        public Type nodeType;

        public EditorNodeAttribute(Type nodeType)
        {
            this.nodeType = nodeType;
        }
    }
}