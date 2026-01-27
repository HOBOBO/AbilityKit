using System;
using UnityEngine;

namespace Emilia.Expressions
{
    [Serializable]
    public class ConstantExpression : Expression
    {
        [SerializeField] public string constantName;

        public ConstantExpression() { }

        public ConstantExpression(string constantName)
        {
            this.constantName = constantName;
        }

        public override object Evaluate(ExpressionContext context)
        {
            if (context.config == null) throw new ExpressionEvaluateException("未设置表达式配置");
            
            if (context.config.TryGetConstant(constantName, out object value)) return value;
            
            throw new ExpressionEvaluateException($"未定义的常量: {constantName}");
        }
    }
}