using System;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Variables.Numeric;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class NumericVarGreaterThanCondition : ITriggerCondition
    {
        private readonly NumericVarRef _var;
        private readonly NumericValueSourceRuntime _threshold;

        public NumericVarGreaterThanCondition(NumericVarRef varRef, NumericValueSourceRuntime threshold)
        {
            _var = varRef;
            _threshold = threshold;
        }

        public static NumericVarGreaterThanCondition FromDef(ConditionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("Condition args is null");

            if (!args.TryGetValue("domain", out var domainObj) || !(domainObj is string domain) || string.IsNullOrEmpty(domain))
            {
                throw new InvalidOperationException("NumericVarGreaterThanCondition requires args['domain'] as non-empty string");
            }

            if (!args.TryGetValue("key", out var keyObj) || !(keyObj is string key) || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("NumericVarGreaterThanCondition requires args['key'] as non-empty string");
            }

            var threshold = NumericValueSourceRuntimeUtil.Parse(args);
            return new NumericVarGreaterThanCondition(new NumericVarRef(domain, key), threshold);
        }

        public bool Evaluate(TriggerContext context)
        {
            if (context == null) return false;

            if (!context.TryGetNumericVar(_var, out var v)) return false;

            if (!NumericValueSourceRuntimeUtil.TryResolve(context, _threshold, out var threshold)) return false;

            return v > threshold;
        }
    }
}
