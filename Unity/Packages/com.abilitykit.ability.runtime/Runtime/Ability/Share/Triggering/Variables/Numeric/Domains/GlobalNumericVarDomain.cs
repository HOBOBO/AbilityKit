using System;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Domains
{
    public sealed class GlobalNumericVarDomain : INumericVarDomain
    {
        public string DomainId => NumericVarDomains.Global;

        public bool TryGet(TriggerContext context, string key, out double value)
        {
            value = 0d;
            if (context == null || string.IsNullOrEmpty(key)) return false;

            if (!context.TryGetVar(VarScope.Global, key, out var obj) || obj == null) return false;

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

        public bool TrySet(TriggerContext context, string key, double value)
        {
            if (context == null || string.IsNullOrEmpty(key)) return false;
            context.SetVar(VarScope.Global, key, value);
            return true;
        }
    }
}
