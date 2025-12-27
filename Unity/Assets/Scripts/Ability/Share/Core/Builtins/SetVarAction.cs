using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class SetVarAction : ITriggerAction
    {
        private readonly VarScope _scope;
        private readonly string _key;
        private readonly ValueSourceRuntime _value;

        public SetVarAction(VarScope scope, string key, ValueSourceRuntime value)
        {
            _scope = scope;
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _value = value;
        }

        public static SetVarAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("Action args is null");

            if (!args.TryGetValue("key", out var keyObj) || !(keyObj is string key) || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("SetVarAction requires args['key'] as non-empty string");
            }

            var scope = VarScope.Local;
            if (args.TryGetValue("scope", out var scopeObj) && scopeObj is string scopeStr && !string.IsNullOrEmpty(scopeStr))
            {
                if (string.Equals(scopeStr, "global", StringComparison.OrdinalIgnoreCase)) scope = VarScope.Global;
                else if (string.Equals(scopeStr, "local", StringComparison.OrdinalIgnoreCase)) scope = VarScope.Local;
            }

            var value = ValueSourceRuntimeUtil.Parse(args);
            return new SetVarAction(scope, key, value);
        }

        public void Execute(TriggerContext context)
        {
            var v = ValueSourceRuntimeUtil.Resolve(context, _value);
            context.SetVar(_scope, _key, v);
        }
    }
}
