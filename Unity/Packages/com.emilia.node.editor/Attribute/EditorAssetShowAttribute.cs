using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 以编辑方式展示资源
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class EditorAssetShowAttribute : Attribute
    {
        /// <summary>
        /// 高度
        /// </summary>
        public float height = 300;

        /// <summary>
        /// 宽度(-1为自适应)
        /// </summary>
        public float width = -1f;
    }
}