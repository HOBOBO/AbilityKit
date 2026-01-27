using System;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// 下拉菜单过滤类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class VariableKeyTypeFilterAttribute : Attribute
    {
        /// <summary>
        /// 过滤类型
        /// </summary>
        public Type type;

        public VariableKeyTypeFilterAttribute(Type type)
        {
            this.type = type;
        }
    }
}