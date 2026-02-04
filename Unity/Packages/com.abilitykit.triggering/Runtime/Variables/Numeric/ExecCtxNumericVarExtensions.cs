using System;

namespace AbilityKit.Triggering.Variables.Numeric
{
    public static class ExecCtxNumericVarExtensions
    {
        public static bool TryGetNumericVar<TCtx>(this in AbilityKit.Triggering.Runtime.ExecCtx<TCtx> ctx, in NumericVarRef varRef, out double value)
        {
            value = 0d;
            if (string.IsNullOrEmpty(varRef.DomainId) || string.IsNullOrEmpty(varRef.Key)) return false;

            var registry = ctx.NumericDomains ?? DefaultNumericVarDomainRegistry.Instance;
            if (!registry.TryGetDomain(varRef.DomainId, out var domain) || domain == null) return false;

            return domain.TryGet(in ctx, varRef.Key, out value);
        }

        public static bool TryGetNumericVar<TCtx>(this in AbilityKit.Triggering.Runtime.ExecCtx<TCtx> ctx, string domainId, string key, out double value)
        {
            return TryGetNumericVar(in ctx, new NumericVarRef(domainId, key), out value);
        }

        public static bool TrySetNumericVar<TCtx>(this in AbilityKit.Triggering.Runtime.ExecCtx<TCtx> ctx, in NumericVarRef varRef, double value)
        {
            if (string.IsNullOrEmpty(varRef.DomainId) || string.IsNullOrEmpty(varRef.Key)) return false;

            var registry = ctx.NumericDomains ?? DefaultNumericVarDomainRegistry.Instance;
            if (!registry.TryGetDomain(varRef.DomainId, out var domain) || domain == null) return false;

            return domain.TrySet(in ctx, varRef.Key, value);
        }

        public static bool TrySetNumericVar<TCtx>(this in AbilityKit.Triggering.Runtime.ExecCtx<TCtx> ctx, string domainId, string key, double value)
        {
            return TrySetNumericVar(in ctx, new NumericVarRef(domainId, key), value);
        }
    }
}
