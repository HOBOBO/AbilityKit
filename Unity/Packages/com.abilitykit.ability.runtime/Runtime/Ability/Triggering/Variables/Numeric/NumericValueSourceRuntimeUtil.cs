using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Ability.Triggering.Variables.Numeric
{
    public enum NumericValueSourceKind
    {
        Const = 0,
        Var = 1,
        Expr = 2
    }

    public readonly struct NumericValueSourceRuntime
    {
        public NumericValueSourceRuntime(NumericValueSourceKind kind, double constValue, string fromDomainId, string fromKey, NumericRpnProgram exprProgram)
        {
            Kind = kind;
            ConstValue = constValue;
            FromDomainId = fromDomainId;
            FromKey = fromKey;
            ExprProgram = exprProgram;
        }

        public NumericValueSourceKind Kind { get; }
        public double ConstValue { get; }
        public string FromDomainId { get; }
        public string FromKey { get; }
        public NumericRpnProgram ExprProgram { get; }
    }

    public static class NumericValueSourceRuntimeUtil
    {
        public static NumericValueSourceRuntime Parse(IReadOnlyDictionary<string, object> args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            var kind = NumericValueSourceKind.Const;
            if (args.TryGetValue("value_source", out var valueSourceObj) && valueSourceObj is string valueSourceStr && !string.IsNullOrEmpty(valueSourceStr))
            {
                if (string.Equals(valueSourceStr, "var", StringComparison.OrdinalIgnoreCase)) kind = NumericValueSourceKind.Var;
                else if (string.Equals(valueSourceStr, "const", StringComparison.OrdinalIgnoreCase)) kind = NumericValueSourceKind.Const;
                else if (string.Equals(valueSourceStr, "expr", StringComparison.OrdinalIgnoreCase)) kind = NumericValueSourceKind.Expr;
            }

            double constValue = 0d;
            if (args.TryGetValue("value", out var valueObj) && valueObj != null)
            {
                TryToDouble(valueObj, out constValue);
            }

            string fromDomainId = null;
            if (args.TryGetValue("value_domain", out var domainObj) && domainObj is string domainStr && !string.IsNullOrEmpty(domainStr))
            {
                fromDomainId = domainStr;
            }

            string fromKey = null;
            if (args.TryGetValue("value_key", out var keyObj) && keyObj is string keyStr && !string.IsNullOrEmpty(keyStr))
            {
                fromKey = keyStr;
            }

            NumericRpnProgram program = null;
            if (kind == NumericValueSourceKind.Expr)
            {
                if (args.TryGetValue("expr", out var exprObj) && exprObj is string exprStr && !string.IsNullOrEmpty(exprStr))
                {
                    NumericExpressionCompiler.TryCompileCached(exprStr, out program);
                }
            }

            return new NumericValueSourceRuntime(kind, constValue, fromDomainId, fromKey, program);
        }

        public static bool TryResolve(TriggerContext context, in NumericValueSourceRuntime source, out double value)
        {
            value = 0d;
            if (context == null) return false;

            if (source.Kind == NumericValueSourceKind.Var)
            {
                if (string.IsNullOrEmpty(source.FromDomainId) || string.IsNullOrEmpty(source.FromKey)) return false;
                return context.TryGetNumericVar(source.FromDomainId, source.FromKey, out value);
            }

            if (source.Kind == NumericValueSourceKind.Expr)
            {
                var program = source.ExprProgram;
                if (program == null) return false;
                return NumericExpressionEvaluator.TryEvaluate(context, program, out value);
            }

            value = source.ConstValue;
            return true;
        }

        public static bool TryToDouble(object obj, out double value)
        {
            value = 0d;
            if (obj == null) return false;
            try
            {
                value = Convert.ToDouble(obj);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
