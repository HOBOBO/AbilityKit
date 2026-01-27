using System;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 指定节点类型为Runtime特性（在EditorGraphAsset中使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NodeToRuntimeAttribute : Attribute
    {
        /// <summary>
        /// 运行时资产节点类型（基类）
        /// </summary>
        public Type baseRuntimeNodeType;

        /// <summary>
        /// 编辑器资产节点类型（基类）
        /// </summary>
        public Type baseEditorNodeType;

        public NodeToRuntimeAttribute(Type baseRuntimeNodeType, Type baseEditorNodeType)
        {
            this.baseRuntimeNodeType = baseRuntimeNodeType;
            this.baseEditorNodeType = baseEditorNodeType;
        }
    }
}