using System;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Variables.Numeric
{
    /// <summary>
    /// NumericValueRef 解析扩展方法
    /// </summary>
    public static class NumericValueRefExtensions
    {
        /// <summary>
        /// 解析 NumericValueRef 为 double 值
        /// </summary>
        public static double Resolve(this in NumericValueRef ref_, object ctx)
        {
            return ref_.Kind switch
            {
                ENumericValueRefKind.Const => ref_.ConstValue,
                ENumericValueRefKind.Blackboard => ResolveBlackboard(ref_, ctx),
                ENumericValueRefKind.PayloadField => ResolvePayloadField(ref_, ctx),
                ENumericValueRefKind.Var => ResolveVar(ref_, ctx),
                ENumericValueRefKind.Expr => ResolveExpr(ref_, ctx),
                _ => 0.0
            };
        }

        private static double ResolveBlackboard(in NumericValueRef ref_, object ctx)
        {
            // 通过黑板解析器获取值
            if (ctx is IBlackboardResolvable resolvable)
            {
                if (resolvable.TryResolveBlackboardValue(ref_.BoardId, ref_.KeyId, out var value))
                    return value;
            }
            return 0.0;
        }

        private static double ResolvePayloadField(in NumericValueRef ref_, object ctx)
        {
            // 通过 Payload 访问器获取值
            if (ctx is Runtime.Executable.IHasPayload payload)
            {
                if (payload.TryGetPayloadDouble(ref_.FieldId, out var value))
                    return value;
            }
            return 0.0;
        }

        private static double ResolveVar(in NumericValueRef ref_, object ctx)
        {
            // 通过变量域解析器获取值
            if (ctx is IVarResolvable varResolvable)
            {
                if (varResolvable.TryResolveVarValue(ref_.DomainId, ref_.Key, out var value))
                    return value;
            }
            return 0.0;
        }

        private static double ResolveExpr(in NumericValueRef ref_, object ctx)
        {
            // 表达式求值（需要表达式解析器）
            // 这里暂时返回常量值，实际应该通过表达式引擎计算
            return 0.0;
        }
    }

    /// <summary>
    /// 支持黑板值解析的上下文接口
    /// </summary>
    public interface IBlackboardResolvable
    {
        bool TryResolveBlackboardValue(int boardId, int keyId, out double value);
    }

    /// <summary>
    /// 支持变量值解析的上下文接口
    /// </summary>
    public interface IVarResolvable
    {
        bool TryResolveVarValue(string domainId, string key, out double value);
    }
}
