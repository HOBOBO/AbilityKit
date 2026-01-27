using System;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 指定节点类型为Editor特性（在EditorGraphAsset中使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeToEditorAttribute : Attribute
    {
        /// <summary>
        /// 编辑器资产节点类型（基类）
        /// </summary>
        public Type baseEditorNodeType;

        public NodeToEditorAttribute(Type baseEditorNodeType)
        {
            this.baseEditorNodeType = baseEditorNodeType;
        }
    }
}