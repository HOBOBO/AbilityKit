using System;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Variables.Numeric;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class SetNumericVarAction : ITriggerAction
    {
        private readonly NumericVarRef _var;
        private readonly NumericValueSourceRuntime _value;

        public SetNumericVarAction(NumericVarRef varRef, NumericValueSourceRuntime value)
        {
            _var = varRef;
            _value = value;
        }

        public static SetNumericVarAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("Action args is null");

            if (!args.TryGetValue("domain", out var domainObj) || !(domainObj is string domain) || string.IsNullOrEmpty(domain))
            {
                throw new InvalidOperationException("SetNumericVarAction requires args['domain'] as non-empty string");
            }

            if (!args.TryGetValue("key", out var keyObj) || !(keyObj is string key) || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("SetNumericVarAction requires args['key'] as non-empty string");
            }

            var value = NumericValueSourceRuntimeUtil.Parse(args);
            return new SetNumericVarAction(new NumericVarRef(domain, key), value);
        }

        public void Execute(TriggerContext context)
        {
            if (!NumericValueSourceRuntimeUtil.TryResolve(context, _value, out var v))
            {
                v = 0d;
            }

            context.TrySetNumericVar(_var, v);
        }
    }
}
