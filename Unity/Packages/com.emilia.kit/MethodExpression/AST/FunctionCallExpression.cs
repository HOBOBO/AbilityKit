using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emilia.Expressions
{
    [Serializable]
    public class FunctionCallExpression : Expression
    {
        [SerializeField] public string functionName;
        [SerializeField] public List<Expression> arguments = new();

        public FunctionCallExpression() { }

        public FunctionCallExpression(string functionName, List<Expression> arguments)
        {
            this.functionName = functionName;
            this.arguments = arguments ?? new List<Expression>();
        }

        public override object Evaluate(ExpressionContext context)
        {
            if (context.config == null) throw new ExpressionEvaluateException("未设置表达式配置");
            if (context.config.TryGetFunction(functionName, out var function) == false) throw new ExpressionEvaluateException($"未定义的函数: {functionName}");

            object[] args = new object[arguments.Count];
            for (int i = 0; i < arguments.Count; i++) args[i] = arguments[i].Evaluate(context);

            return function.Invoke(args, context);
        }
    }
}