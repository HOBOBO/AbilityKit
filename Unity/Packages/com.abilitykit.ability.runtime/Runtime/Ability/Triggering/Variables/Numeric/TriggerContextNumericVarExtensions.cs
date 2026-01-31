using System;
using AbilityKit.Ability.Share.Common.Log;

namespace AbilityKit.Ability.Triggering.Variables.Numeric
{
    public static class TriggerContextNumericVarExtensions
    {
        public static bool TryGetNumericVar(this TriggerContext context, in NumericVarRef varRef, out double value)
        {
            value = 0d;
            if (context == null) return false;
            if (string.IsNullOrEmpty(varRef.DomainId) || string.IsNullOrEmpty(varRef.Key)) return false;

            var registry = GetRegistry(context);
            if (!registry.TryGetDomain(varRef.DomainId, out var domain) || domain == null) return false;

            return domain.TryGet(context, varRef.Key, out value);
        }

        public static bool TryGetNumericVar(this TriggerContext context, string domainId, string key, out double value)
        {
            return TryGetNumericVar(context, new NumericVarRef(domainId, key), out value);
        }

        public static bool TrySetNumericVar(this TriggerContext context, in NumericVarRef varRef, double value)
        {
            if (context == null) return false;
            if (string.IsNullOrEmpty(varRef.DomainId) || string.IsNullOrEmpty(varRef.Key)) return false;

            var registry = GetRegistry(context);
            if (!registry.TryGetDomain(varRef.DomainId, out var domain) || domain == null) return false;

            return domain.TrySet(context, varRef.Key, value);
        }

        public static bool TrySetNumericVar(this TriggerContext context, string domainId, string key, double value)
        {
            return TrySetNumericVar(context, new NumericVarRef(domainId, key), value);
        }

        private static INumericVarDomainRegistry GetRegistry(TriggerContext context)
        {
            var sp = context.Services;
            if (sp != null)
            {
                try
                {
                    var obj = sp.GetService(typeof(INumericVarDomainRegistry));
                    if (obj is INumericVarDomainRegistry registry) return registry;
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[TriggerContextNumericVarExtensions] resolve INumericVarDomainRegistry failed");
                }
            }

            return DefaultNumericVarDomainRegistry.Instance;
        }
    }
}
