using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 工具栏特性基类（在EditorGraphAsset中使用）
    /// </summary>
    public abstract class ToolbarAttribute : Attribute
    {
        /// <summary>
        /// 控件位置
        /// </summary>
        public ToolbarViewControlPosition position { get; private set; }

        public ToolbarAttribute(ToolbarViewControlPosition position)
        {
            this.position = position;
        }
    }
}