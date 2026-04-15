using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 布尔表达式（RPN）的流畅 API
    /// 提供更易读的布尔条件构建方式
    /// </summary>
    /// <example>
    /// // 使用流畅 API 构建条件:
    /// var expr = PredicateExprDsl
    ///     .Compare(ECompareOp.Gt, NumericValueRefDsl.Payload("amount"), NumericValueRefDsl.Const(10))
    ///     .And(PredicateExprDsl.Compare(ECompareOp.Lt, NumericValueRefDsl.Var("actor", "hp"), NumericValueRefDsl.Const(50)))
    ///     .Or(PredicateExprDsl.Const(false))
    ///     .Build();
    /// </example>
    public static class PredicateExprDsl
    {
        /// <summary>
        /// 创建一个常量布尔值
        /// </summary>
        public static PredicateExprBuilder Const(bool value)
        {
            return new PredicateExprBuilder().Add(BoolExprNode.Const(value));
        }

        /// <summary>
        /// 创建一个布尔表达式比较
        /// </summary>
        public static PredicateExprBuilder Compare(ECompareOp op, NumericValueRef left, NumericValueRef right)
        {
            return new PredicateExprBuilder().Add(BoolExprNode.Compare(op, left, right));
        }

        /// <summary>
        /// 创建一个相等比较
        /// </summary>
        public static PredicateExprBuilder Eq(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.Equal, left, right);
        }

        /// <summary>
        /// 创建一个不等比较
        /// </summary>
        public static PredicateExprBuilder Ne(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.NotEqual, left, right);
        }

        /// <summary>
        /// 创建一个大于比较
        /// </summary>
        public static PredicateExprBuilder Gt(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.GreaterThan, left, right);
        }

        /// <summary>
        /// 创建一个大于等于比较
        /// </summary>
        public static PredicateExprBuilder Ge(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.GreaterThanOrEqual, left, right);
        }

        /// <summary>
        /// 创建一个小于比较
        /// </summary>
        public static PredicateExprBuilder Lt(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.LessThan, left, right);
        }

        /// <summary>
        /// 创建一个小于等于比较
        /// </summary>
        public static PredicateExprBuilder Le(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.LessThanOrEqual, left, right);
        }

        /// <summary>
        /// 创建一个否定操作
        /// </summary>
        public static PredicateExprBuilder Not()
        {
            return new PredicateExprBuilder().Add(BoolExprNode.Not());
        }
    }

    /// <summary>
    /// 布尔表达式构建器
    /// 支持链式调用追加节点
    /// </summary>
    public sealed class PredicateExprBuilder
    {
        private readonly List<BoolExprNode> _nodes = new List<BoolExprNode>();

        internal PredicateExprBuilder() { }

        /// <summary>
        /// 追加一个节点
        /// </summary>
        public PredicateExprBuilder Add(BoolExprNode node)
        {
            _nodes.Add(node);
            return this;
        }

        /// <summary>
        /// 追加 And 操作
        /// </summary>
        public PredicateExprBuilder And()
        {
            _nodes.Add(BoolExprNode.And());
            return this;
        }

        /// <summary>
        /// 追加 Or 操作
        /// </summary>
        public PredicateExprBuilder Or()
        {
            _nodes.Add(BoolExprNode.Or());
            return this;
        }

        /// <summary>
        /// 追加 Not 操作
        /// </summary>
        public PredicateExprBuilder Not()
        {
            _nodes.Add(BoolExprNode.Not());
            return this;
        }

        /// <summary>
        /// 追加一个比较操作
        /// </summary>
        public PredicateExprBuilder Compare(ECompareOp op, NumericValueRef left, NumericValueRef right)
        {
            _nodes.Add(BoolExprNode.Compare(op, left, right));
            return this;
        }

        /// <summary>
        /// 追加一个相等比较
        /// </summary>
        public PredicateExprBuilder Eq(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.Equal, left, right);
        }

        /// <summary>
        /// 追加一个不等比较
        /// </summary>
        public PredicateExprBuilder Ne(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.NotEqual, left, right);
        }

        /// <summary>
        /// 追加一个大于比较
        /// </summary>
        public PredicateExprBuilder Gt(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.GreaterThan, left, right);
        }

        /// <summary>
        /// 追加一个大于等于比较
        /// </summary>
        public PredicateExprBuilder Ge(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.GreaterThanOrEqual, left, right);
        }

        /// <summary>
        /// 追加一个小于比较
        /// </summary>
        public PredicateExprBuilder Lt(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.LessThan, left, right);
        }

        /// <summary>
        /// 追加一个小于等于比较
        /// </summary>
        public PredicateExprBuilder Le(NumericValueRef left, NumericValueRef right)
        {
            return Compare(ECompareOp.LessThanOrEqual, left, right);
        }

        /// <summary>
        /// 追加一个常量值
        /// </summary>
        public PredicateExprBuilder Value(bool value)
        {
            _nodes.Add(BoolExprNode.Const(value));
            return this;
        }

        /// <summary>
        /// 追加一个值（数值作为布尔：非零为 true）
        /// </summary>
        public PredicateExprBuilder Value(NumericValueRef valueRef)
        {
            _nodes.Add(BoolExprNode.Compare(ECompareOp.NotEqual, valueRef, NumericValueRef.Const(0)));
            return this;
        }

        /// <summary>
        /// 构建为 PredicateExprPlan
        /// </summary>
        public PredicateExprPlan Build()
        {
            return new PredicateExprPlan(_nodes.ToArray());
        }
    }

    /// <summary>
    /// PredicateExprBuilder 的扩展方法
    /// </summary>
    public static class PredicateExprBuilderExtensions
    {
        /// <summary>
        /// 将两个表达式用 And 连接
        /// </summary>
        public static PredicateExprBuilder And(this PredicateExprBuilder builder, Func<PredicateExprBuilder, PredicateExprBuilder> andBuilder)
        {
            if (andBuilder != null)
            {
                andBuilder(builder);
            }
            builder.And();
            return builder;
        }

        /// <summary>
        /// 将两个表达式用 Or 连接
        /// </summary>
        public static PredicateExprBuilder Or(this PredicateExprBuilder builder, Func<PredicateExprBuilder, PredicateExprBuilder> orBuilder)
        {
            if (orBuilder != null)
            {
                orBuilder(builder);
            }
            builder.Or();
            return builder;
        }

        /// <summary>
        /// 对表达式取反
        /// </summary>
        public static PredicateExprBuilder Not(this PredicateExprBuilder builder, Func<PredicateExprBuilder, PredicateExprBuilder> notBuilder)
        {
            if (notBuilder != null)
            {
                notBuilder(builder);
            }
            builder.Not();
            return builder;
        }
    }
}
