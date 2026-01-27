using System;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 用于EditorEdgeView指定EditorEdgeAsset
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EditorEdgeAttribute : Attribute
    {
        /// <summary>
        /// EdgeAsset类型
        /// </summary>
        public Type edgeType;

        public EditorEdgeAttribute(Type edgeType)
        {
            this.edgeType = edgeType;
        }
    }
}