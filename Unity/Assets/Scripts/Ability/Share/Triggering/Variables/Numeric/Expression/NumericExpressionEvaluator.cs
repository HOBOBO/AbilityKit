using System;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Expression
{
    public static class NumericExpressionEvaluator
    {
        public static bool TryEvaluate(TriggerContext context, NumericRpnProgram program, out double value)
        {
            value = 0d;
            if (context == null || program == null) return false;

            var tokens = program.Tokens;
            if (tokens == null || tokens.Length == 0) return false;

            var stack = new double[tokens.Length];
            var top = 0;

            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                switch (t.Kind)
                {
                    case NumericRpnTokenKind.Number:
                        stack[top++] = t.Number;
                        break;

                    case NumericRpnTokenKind.Var:
                        if (string.IsNullOrEmpty(t.DomainId) || string.IsNullOrEmpty(t.Key)) return false;
                        if (!context.TryGetNumericVar(t.DomainId, t.Key, out var v)) return false;
                        stack[top++] = v;
                        break;

                    case NumericRpnTokenKind.Add:
                    case NumericRpnTokenKind.Sub:
                    case NumericRpnTokenKind.Mul:
                    case NumericRpnTokenKind.Div:
                        if (top < 2) return false;
                        var b = stack[--top];
                        var a = stack[--top];
                        double r;
                        if (t.Kind == NumericRpnTokenKind.Add) r = a + b;
                        else if (t.Kind == NumericRpnTokenKind.Sub) r = a - b;
                        else if (t.Kind == NumericRpnTokenKind.Mul) r = a * b;
                        else
                        {
                            if (b == 0d) return false;
                            r = a / b;
                        }
                        stack[top++] = r;
                        break;

                    case NumericRpnTokenKind.Func:
                        if (top < t.FuncArgCount) return false;
                        var registry = GetFunctionRegistry(context);
                        if (registry == null) return false;
                        if (!registry.TryGet(t.FuncName, out var fn) || fn == null) return false;
                        if (fn.ArgCount != t.FuncArgCount) return false;

                        var args = new double[t.FuncArgCount];
                        for (int ai = t.FuncArgCount - 1; ai >= 0; ai--)
                        {
                            args[ai] = stack[--top];
                        }

                        if (!fn.TryInvoke(args, out var fr)) return false;
                        stack[top++] = fr;
                        break;

                    default:
                        return false;
                }
            }

            if (top != 1) return false;
            value = stack[0];
            return true;
        }

        private static INumericRpnFunctionRegistry GetFunctionRegistry(TriggerContext context)
        {
            var sp = context.Services;
            if (sp != null)
            {
                try
                {
                    var obj = sp.GetService(typeof(INumericRpnFunctionRegistry));
                    if (obj is INumericRpnFunctionRegistry registry) return registry;
                }
                catch
                {
                }
            }

            return DefaultNumericRpnFunctionRegistry.Instance;
        }
    }
}
