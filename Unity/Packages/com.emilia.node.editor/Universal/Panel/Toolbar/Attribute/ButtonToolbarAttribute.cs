using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 工具栏按钮特性（在EditorGraphAsset中使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ButtonToolbarAttribute : ToolbarAttribute
    {
        /// <summary>
        /// 按钮名称
        /// </summary>
        public string displayName;

        public ButtonToolbarAttribute(string displayName, ToolbarViewControlPosition position) : base(position)
        {
            this.displayName = displayName;
        }
    }
}