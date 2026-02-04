using System;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Variables.Numeric
{
    public enum ENumericValueSourceKind : byte
    {
        Const = 0,
        Var = 1,
        Expr = 2,
    }

    public readonly struct NumericValueSourceRuntime
    {
        public readonly ENumericValueSourceKind Kind;
        public readonly double ConstValue;
        public readonly NumericVarRef VarRef;
        public readonly string Expr;

        private NumericValueSourceRuntime(ENumericValueSourceKind kind, double constValue, NumericVarRef varRef, string expr)
        {
            Kind = kind;
            ConstValue = constValue;
            VarRef = varRef;
            Expr = expr;
        }

        public static NumericValueSourceRuntime Const(double value)
        {
            return new NumericValueSourceRuntime(ENumericValueSourceKind.Const, value, default, null);
        }

        public static NumericValueSourceRuntime Var(string domainId, string key)
        {
            return new NumericValueSourceRuntime(ENumericValueSourceKind.Var, 0d, new NumericVarRef(domainId, key), null);
        }

        public static NumericValueSourceRuntime ExprText(string expr)
        {
            return new NumericValueSourceRuntime(ENumericValueSourceKind.Expr, 0d, default, expr);
        }

        public bool TryEvaluate<TCtx>(in AbilityKit.Triggering.Runtime.ExecCtx<TCtx> ctx, out double value)
        {
            value = 0d;
            switch (Kind)
            {
                case ENumericValueSourceKind.Const:
                    value = ConstValue;
                    return true;

                case ENumericValueSourceKind.Var:
                    return ctx.TryGetNumericVar(in VarRef, out value);

                case ENumericValueSourceKind.Expr:
                {
                    if (string.IsNullOrWhiteSpace(Expr)) return false;
                    if (!NumericExpressionCompiler.TryCompileCached(Expr, out var program) || program == null) return false;
                    return NumericExpressionEvaluator.TryEvaluate(in ctx, program, out value);
                }

                default:
                    return false;
            }
        }

        public bool TrySet<TCtx>(in AbilityKit.Triggering.Runtime.ExecCtx<TCtx> ctx, double value)
        {
            if (Kind != ENumericValueSourceKind.Var) return false;
            return ctx.TrySetNumericVar(in VarRef, value);
        }
    }
}
