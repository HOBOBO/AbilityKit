using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 工具栏自定义GUI特性（在EditorGraphAsset中使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CustomToolbarAttribute : ToolbarAttribute
    {
        public CustomToolbarAttribute(ToolbarViewControlPosition position) : base(position) { }
    }
}