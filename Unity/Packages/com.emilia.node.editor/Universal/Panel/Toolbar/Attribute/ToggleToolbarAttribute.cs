using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 工具栏Toggle特性（在EditorGraphAsset中使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ToggleToolbarAttribute : ToolbarAttribute
    {
        /// <summary>
        /// Toggle名称
        /// </summary>
        public string displayName { get; private set; }

        public ToggleToolbarAttribute(string displayName, ToolbarViewControlPosition position) : base(position)
        {
            this.displayName = displayName;
        }
    }
}