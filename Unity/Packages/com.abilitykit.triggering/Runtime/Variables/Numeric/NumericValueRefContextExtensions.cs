using System;
using AbilityKit.Triggering.Runtime.Abstractions;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Variables.Numeric
{
    /// <summary>
    /// NumericValueRef 针对 ActionContext 的解析扩展
    /// 提供 Resolve(ActionContext) 方法，替代原有的 Resolve(object ctx)
    /// </summary>
    public static class NumericValueRefContextExtensions
    {
        /// <summary>
        /// 在 ActionContext 中解析数值引用
        /// </summary>
        public static double Resolve(this in NumericValueRef valueRef, ActionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            return valueRef.Kind switch
            {
                ENumericValueRefKind.Const => valueRef.ConstValue,
                ENumericValueRefKind.Blackboard => ResolveBlackboard(in valueRef, context),
                ENumericValueRefKind.PayloadField => ResolvePayloadField(in valueRef, context),
                ENumericValueRefKind.Var => ResolveVar(in valueRef, context),
                ENumericValueRefKind.Expr => ResolveExpr(in valueRef, context),
                _ => 0.0
            };
        }

        private static double ResolveBlackboard(in NumericValueRef valueRef, ActionContext context)
        {
            var resolver = context.Blackboard;
            if (resolver == null)
                return 0.0;

            if (resolver.TryResolve(valueRef.BoardId, out var board) && board != null)
            {
                if (board.TryGetDouble(valueRef.KeyId, out var value))
                    return value;
            }

            return 0.0;
        }

        private static double ResolvePayloadField(in NumericValueRef valueRef, ActionContext context)
        {
            var accessor = context.Payloads;
            if (accessor == null)
                return 0.0;

            // 获取 context 的 payload args（通常是 args 对象本身）
            var payloadService = context.GetService<IPayloadAccessor>();
            var payloadArgs = payloadService?.Target;
            if (payloadArgs == null)
                return 0.0;

            // 使用局部变量来满足 in 参数要求
            object args = payloadArgs;
            if (accessor.TryGetPayloadDouble(in args, valueRef.FieldId, out var value))
                return value;

            return 0.0;
        }

        private static double ResolveVar(in NumericValueRef valueRef, ActionContext context)
        {
            var repo = context.Variables;
            if (repo == null)
                return 0.0;

            return repo.GetNumeric(valueRef.DomainId, valueRef.Key);
        }

        private static double ResolveExpr(in NumericValueRef valueRef, ActionContext context)
        {
            // 表达式求值需要额外的表达式解析器
            // 暂时返回 0，后续可扩展
            // 可以从 context 中获取表达式服务
            if (string.IsNullOrEmpty(valueRef.ExprText))
                return 0.0;

            // TODO: 集成 NumericExpressionEvaluator
            // var program = NumericExpressionCompiler.Compile(valueRef.ExprText);
            // NumericExpressionEvaluator.Evaluate(context, program, out var result);
            // return result;

            return 0.0;
        }
    }
}
