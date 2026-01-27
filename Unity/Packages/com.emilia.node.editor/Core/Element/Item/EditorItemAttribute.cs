using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 用于EditorItemView指定EditorItemAsset
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EditorItemAttribute : Attribute
    {
        /// <summary>
        /// Item资产类型
        /// </summary>
        public Type itemType;

        public EditorItemAttribute(Type itemType)
        {
            this.itemType = itemType;
        }
    }
}