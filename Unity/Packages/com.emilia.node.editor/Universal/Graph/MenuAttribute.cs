using System;
using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 操作菜单特性（在EditorGraphAsset中的函数使用）
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuAttribute : Attribute
    {
        /// <summary>
        /// 菜单名称
        /// </summary>
        public readonly string name;

        /// <summary>
        /// 菜单分组
        /// </summary>
        public readonly string category;

        /// <summary>
        /// 菜单优先级
        /// </summary>
        public int priority;

        /// <summary>
        /// 获取判断是否选中的表达式
        /// </summary>
        public string isOnExpression;

        /// <summary>
        /// 获取有效性函数名称
        /// </summary>
        public string actionValidityMethod;

        public MenuAttribute(string path, int priority)
        {
            this.priority = priority;
            OperateMenuUtility.PathToNameAndCategory(path, out name, out this.category);
        }
    }
}