using System;

namespace Emilia.BehaviorTree.Attributes
{
    /// <summary>
    /// 在编辑器Variable过滤类型
    /// </summary>
    public class VariableTypeFilterAttribute : Attribute
    {
        /// <summary>
        /// 过滤类型
        /// </summary>
        public Type type { get; private set; }

        /// <summary>
        /// 过滤类型表达式
        /// </summary>
        public string getTypeExpression { get; private set; }

        public VariableTypeFilterAttribute(Type type)
        {
            this.type = type;
            getTypeExpression = null;
        }

        public VariableTypeFilterAttribute(string getTypeExpression)
        {
            this.getTypeExpression = getTypeExpression;
            type = null;
        }
    }
}