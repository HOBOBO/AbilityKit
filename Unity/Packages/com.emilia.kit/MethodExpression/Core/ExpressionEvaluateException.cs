using System;

namespace Emilia.Expressions
{
    /// <summary>
    /// 表达式求值异常
    /// </summary>
    public class ExpressionEvaluateException : Exception
    {
        public ExpressionEvaluateException(string message) : base(message) { }
        public ExpressionEvaluateException(string message, Exception innerException) : base(message, innerException) { }
    }
}