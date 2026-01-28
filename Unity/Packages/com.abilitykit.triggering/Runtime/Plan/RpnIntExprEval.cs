using System;
using System.Buffers;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public static class RpnIntExprEval
    {
        public static int Eval<TArgs, TCtx>(RpnIntNode[] nodes, in TArgs args, in ExecCtx<TCtx> ctx)
        {
            if (nodes == null || nodes.Length == 0) return 0;

            if (nodes.Length <= 64)
            {
                Span<int> stack = stackalloc int[64];
                var sp = 0;
                EvalNodes(nodes, in args, in ctx, ref stack, ref sp);
                if (sp != 1) throw new InvalidOperationException("Invalid RPN stack depth: " + sp);
                return stack[0];
            }

            var rented = ArrayPool<int>.Shared.Rent(nodes.Length);
            try
            {
                Span<int> stack = rented;
                var sp = 0;
                EvalNodes(nodes, in args, in ctx, ref stack, ref sp);
                if (sp != 1) throw new InvalidOperationException("Invalid RPN stack depth: " + sp);
                return stack[0];
            }
            finally
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }

        private static void EvalNodes<TArgs, TCtx>(RpnIntNode[] nodes, in TArgs args, in ExecCtx<TCtx> ctx, ref Span<int> stack, ref int sp)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                switch (n.Kind)
                {
                    case ERpnIntNodeKind.Push:
                        stack[sp++] = ResolveIntValueRef(in args, in n.Value, in ctx);
                        break;
                    case ERpnIntNodeKind.Add:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid RPN: ADD stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        stack[sp++] = a + b;
                        break;
                    }
                    case ERpnIntNodeKind.Sub:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid RPN: SUB stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        stack[sp++] = a - b;
                        break;
                    }
                    case ERpnIntNodeKind.Mul:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid RPN: MUL stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        stack[sp++] = a * b;
                        break;
                    }
                    case ERpnIntNodeKind.Div:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid RPN: DIV stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        if (b == 0) throw new DivideByZeroException();
                        stack[sp++] = a / b;
                        break;
                    }
                    default:
                        throw new InvalidOperationException("Unsupported RPN node kind: " + n.Kind);
                }
            }
        }

        private static int ResolveIntValueRef<TArgs, TCtx>(in TArgs args, in IntValueRef valueRef, in ExecCtx<TCtx> ctx)
        {
            if (valueRef.Kind == EIntValueRefKind.Const) return valueRef.ConstValue;

            if (valueRef.Kind == EIntValueRefKind.Blackboard)
            {
                var resolver = ctx.Blackboards;
                if (resolver == null) throw new InvalidOperationException("Blackboard resolver is null");
                if (!resolver.TryResolve(valueRef.BoardId, out var bb) || bb == null) throw new InvalidOperationException("Blackboard not found: " + valueRef.BoardId);
                if (!bb.TryGetInt(valueRef.KeyId, out var v)) throw new InvalidOperationException("Blackboard int key not found: " + valueRef.KeyId);
                return v;
            }

            if (valueRef.Kind == EIntValueRefKind.PayloadField)
            {
                var payloads = ctx.Payloads;
                if (payloads == null) throw new InvalidOperationException("Payload accessor registry is null");
                if (!payloads.TryGetInt(in args, valueRef.FieldId, out var v)) throw new InvalidOperationException("Payload int field not found: " + valueRef.FieldId);
                return v;
            }

            throw new InvalidOperationException("Unsupported IntValueRef kind: " + valueRef.Kind);
        }
    }
}
