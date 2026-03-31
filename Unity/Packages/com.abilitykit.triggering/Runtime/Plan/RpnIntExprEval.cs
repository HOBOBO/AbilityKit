using System;
using System.Buffers;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public static class RpnNumericExprEval
    {
        public static double Eval<TArgs, TCtx>(RpnNumericNode[] nodes, in TArgs args, in ExecCtx<TCtx> ctx)
        {
            if (nodes == null || nodes.Length == 0) return 0d;

            if (nodes.Length <= 64)
            {
                Span<double> stack = stackalloc double[64];
                var sp = 0;
                EvalNodes(nodes, in args, in ctx, ref stack, ref sp);
                if (sp != 1) throw new InvalidOperationException("Invalid RPN stack depth: " + sp);
                return stack[0];
            }

            var rented = ArrayPool<double>.Shared.Rent(nodes.Length);
            try
            {
                Span<double> stack = rented;
                var sp = 0;
                EvalNodes(nodes, in args, in ctx, ref stack, ref sp);
                if (sp != 1) throw new InvalidOperationException("Invalid RPN stack depth: " + sp);
                return stack[0];
            }
            finally
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }

        private static void EvalNodes<TArgs, TCtx>(RpnNumericNode[] nodes, in TArgs args, in ExecCtx<TCtx> ctx, ref Span<double> stack, ref int sp)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                switch (n.Kind)
                {
                    case ERpnNumericNodeKind.Push:
                        stack[sp++] = ResolveNumericValueRef(in args, in n.Value, in ctx);
                        break;
                    case ERpnNumericNodeKind.Add:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid RPN: ADD stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        stack[sp++] = a + b;
                        break;
                    }
                    case ERpnNumericNodeKind.Sub:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid RPN: SUB stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        stack[sp++] = a - b;
                        break;
                    }
                    case ERpnNumericNodeKind.Mul:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid RPN: MUL stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        stack[sp++] = a * b;
                        break;
                    }
                    case ERpnNumericNodeKind.Div:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid RPN: DIV stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        if (b == 0d) throw new DivideByZeroException();
                        stack[sp++] = a / b;
                        break;
                    }
                    default:
                        throw new InvalidOperationException("Unsupported RPN node kind: " + n.Kind);
                }
            }
        }

        private static double ResolveNumericValueRef<TArgs, TCtx>(in TArgs args, in NumericValueRef valueRef, in ExecCtx<TCtx> ctx)
        {
            if (valueRef.Kind == ENumericValueRefKind.Const) return valueRef.ConstValue;

            if (valueRef.Kind == ENumericValueRefKind.Blackboard)
            {
                var resolver = ctx.Blackboards;
                if (resolver == null) throw new InvalidOperationException("Blackboard resolver is null");
                if (!resolver.TryResolve(valueRef.BoardId, out var bb) || bb == null) throw new InvalidOperationException("Blackboard not found: " + valueRef.BoardId);
                if (!bb.TryGetDouble(valueRef.KeyId, out var v)) throw new InvalidOperationException("Blackboard numeric key not found: " + valueRef.KeyId);
                return v;
            }

            if (valueRef.Kind == ENumericValueRefKind.PayloadField)
            {
                // 使用 ExecCtx 的强类型访问方法（如果可用）
                // 注意：这里需要 struct 约束，所以在低级别评估中直接使用 legacy accessor
                var payloads = ctx.Payloads;
                if (payloads == null) throw new InvalidOperationException("Payload accessor registry is null");
                if (!payloads.TryGetDouble(in args, valueRef.FieldId, out var v)) throw new InvalidOperationException("Payload numeric field not found: " + valueRef.FieldId);
                return v;
            }

            if (valueRef.Kind == ENumericValueRefKind.Var)
            {
                if (string.IsNullOrEmpty(valueRef.DomainId) || string.IsNullOrEmpty(valueRef.Key))
                    throw new InvalidOperationException("Numeric var ref is empty");
                if (!ctx.TryGetNumericVar(valueRef.DomainId, valueRef.Key, out var v))
                    throw new InvalidOperationException("Numeric var not found: " + valueRef.DomainId + "." + valueRef.Key);
                return v;
            }

            if (valueRef.Kind == ENumericValueRefKind.Expr)
            {
                if (string.IsNullOrEmpty(valueRef.ExprText)) throw new InvalidOperationException("Numeric expr text is empty");
                if (!NumericExpressionCompiler.TryCompileCached(valueRef.ExprText, out var program) || program == null)
                    throw new InvalidOperationException("Numeric expr compile failed: " + valueRef.ExprText);
                if (!NumericExpressionEvaluator.TryEvaluate(in ctx, program, out var v))
                    throw new InvalidOperationException("Numeric expr evaluate failed: " + valueRef.ExprText);
                return v;
            }

            throw new InvalidOperationException("Unsupported NumericValueRef kind: " + valueRef.Kind);
        }
    }
}
