using System;
using UnityEngine;

namespace Emilia.Expressions
{
    [Serializable]
    public class VariableExpression : Expression
    {
        [SerializeField] public string variableName;

        public VariableExpression() { }

        public VariableExpression(string variableName)
        {
            this.variableName = variableName;
        }

        public override object Evaluate(ExpressionContext context)
        {
            if (context.TryGetVariable(variableName, out object value)) return value;
            throw new ExpressionEvaluateException($"未定义的变量: {variableName}");
        }
    }
}