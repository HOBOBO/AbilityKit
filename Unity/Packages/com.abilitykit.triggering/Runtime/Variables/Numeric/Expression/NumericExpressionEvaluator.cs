using System;
using System.Buffers;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Variables.Numeric.Expression
{
    public static class NumericExpressionEvaluator
    {
        public static bool TryEvaluate<TCtx>(in AbilityKit.Triggering.Runtime.ExecCtx<TCtx> ctx, NumericRpnProgram program, out double value)
        {
            value = 0d;
            if (program == null) return false;

            var tokens = program.Tokens;
            if (tokens == null || tokens.Length == 0) return false;

            if (tokens.Length <= 64)
            {
                Span<double> stack = stackalloc double[64];
                var sp = 0;
                return EvalTokens(tokens, in ctx, ref stack, ref sp, out value);
            }

            var rented = ArrayPool<double>.Shared.Rent(tokens.Length);
            try
            {
                Span<double> stack = rented;
                var sp = 0;
                return EvalTokens(tokens, in ctx, ref stack, ref sp, out value);
            }
            finally
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }

        private static bool EvalTokens<TCtx>(NumericRpnToken[] tokens, in AbilityKit.Triggering.Runtime.ExecCtx<TCtx> ctx, ref Span<double> stack, ref int sp, out double value)
        {
            value = 0d;

            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                switch (t.Kind)
                {
                    case NumericRpnTokenKind.Number:
                        stack[sp++] = t.Number;
                        break;

                    case NumericRpnTokenKind.Var:
                        if (string.IsNullOrEmpty(t.DomainId) || string.IsNullOrEmpty(t.Key)) return false;
                        if (!ctx.TryGetNumericVar(t.DomainId, t.Key, out var v)) return false;
                        stack[sp++] = v;
                        break;

                    case NumericRpnTokenKind.Add:
                    case NumericRpnTokenKind.Sub:
                    case NumericRpnTokenKind.Mul:
                    case NumericRpnTokenKind.Div:
                        if (sp < 2) return false;
                        var b = stack[--sp];
                        var a = stack[--sp];
                        if (t.Kind == NumericRpnTokenKind.Add) stack[sp++] = a + b;
                        else if (t.Kind == NumericRpnTokenKind.Sub) stack[sp++] = a - b;
                        else if (t.Kind == NumericRpnTokenKind.Mul) stack[sp++] = a * b;
                        else
                        {
                            if (b == 0d) return false;
                            stack[sp++] = a / b;
                        }
                        break;

                    case NumericRpnTokenKind.Func:
                    {
                        if (sp < t.FuncArgCount) return false;

                        var registry = ctx.NumericFunctions ?? DefaultNumericRpnFunctionRegistry.Instance;
                        if (!registry.TryGet(t.FuncName, out var fn) || fn == null) return false;
                        if (fn.ArgCount != t.FuncArgCount) return false;

                        var args = new double[t.FuncArgCount];
                        for (int ai = t.FuncArgCount - 1; ai >= 0; ai--)
                        {
                            args[ai] = stack[--sp];
                        }

                        if (!fn.TryInvoke(args, out var fr)) return false;
                        stack[sp++] = fr;
                        break;
                    }

                    default:
                        return false;
                }
            }

            if (sp != 1) return false;
            value = stack[0];
            return true;
        }
    }
}
