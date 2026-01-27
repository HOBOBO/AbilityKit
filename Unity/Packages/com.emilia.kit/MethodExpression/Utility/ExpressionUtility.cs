using System;
using System.Collections.Generic;
using Emilia.Reference;

namespace Emilia.Expressions
{
    /// <summary>
    /// 表达式工具类
    /// </summary>
    public static class ExpressionUtility
    {
        /// <summary>
        /// 解析表达式字符串为AST
        /// </summary>
        /// <param name="expression">表达式字符串</param>
        /// <param name="config">表达式配置</param>
        /// <returns>表达式AST</returns>
        public static Expression Parse(string expression, ExpressionConfig config)
        {
            ExpressionTokenizer tokenizer = ReferencePool.Acquire<ExpressionTokenizer>();
            ExpressionParser parser = ReferencePool.Acquire<ExpressionParser>();

            try
            {
                List<Token> tokens = tokenizer.Tokenize(expression);
                Expression result = parser.Parse(tokens, config);
                return result;
            }
            finally
            {
                ReferencePool.Release(tokenizer);
                ReferencePool.Release(parser);
            }
        }

        /// <summary>
        /// 解析并求值表达式
        /// </summary>
        /// <param name="expression">表达式字符串</param>
        /// <param name="context">求值上下文</param>
        /// <returns>求值结果</returns>
        public static object Evaluate(string expression, ExpressionContext context)
        {
            Expression expr = Parse(expression, context.config);
            return expr.Evaluate(context);
        }

        /// <summary>
        /// 解析并求值表达式，返回指定类型
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="expression">表达式字符串</param>
        /// <param name="context">求值上下文</param>
        /// <returns>求值结果</returns>
        public static T Evaluate<T>(string expression, ExpressionContext context)
        {
            object result = Evaluate(expression, context);
            return ConvertTo<T>(result);
        }

        /// <summary>
        /// 对已解析的表达式求值
        /// </summary>
        /// <param name="expression">表达式AST</param>
        /// <param name="context">求值上下文</param>
        /// <returns>求值结果</returns>
        public static object Evaluate(Expression expression, ExpressionContext context) => expression.Evaluate(context);

        /// <summary>
        /// 对已解析的表达式求值，返回指定类型
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="expression">表达式AST</param>
        /// <param name="context">求值上下文</param>
        /// <returns>求值结果</returns>
        public static T Evaluate<T>(Expression expression, ExpressionContext context)
        {
            object result = expression.Evaluate(context);
            return ConvertTo<T>(result);
        }

        /// <summary>
        /// 创建表达式上下文
        /// </summary>
        /// <param name="config">表达式配置</param>
        /// <returns>表达式上下文</returns>
        public static ExpressionContext CreateContext(ExpressionConfig config)
        {
            ExpressionContext context = ReferencePool.Acquire<ExpressionContext>();
            context.config = config;
            return context;
        }

        /// <summary>
        /// 释放表达式上下文
        /// </summary>
        /// <param name="context">表达式上下文</param>
        public static void ReleaseContext(ExpressionContext context)
        {
            if (context != null) ReferencePool.Release(context);
        }

        /// <summary>
        /// 类型转换
        /// </summary>
        internal static T ConvertTo<T>(object value)
        {
            if (value == null) return default;

            if (value is T t) return t;

            Type targetType = typeof(T);

            if (targetType == typeof(int)) return (T) (object) Convert.ToInt32(value);

            if (targetType == typeof(long)) return (T) (object) Convert.ToInt64(value);

            if (targetType == typeof(float)) return (T) (object) Convert.ToSingle(value);

            if (targetType == typeof(double)) return (T) (object) Convert.ToDouble(value);

            if (targetType == typeof(bool)) return (T) (object) Convert.ToBoolean(value);

            if (targetType == typeof(string)) return (T) (object) value.ToString();

            return (T) Convert.ChangeType(value, targetType);
        }
    }
}