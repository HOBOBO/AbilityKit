using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class ArgGreaterThanCondition : ITriggerCondition
    {
        private readonly string _key;
        private readonly ValueSourceRuntime _threshold;

        public ArgGreaterThanCondition(string key, ValueSourceRuntime threshold)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _threshold = threshold;
        }

        public static ArgGreaterThanCondition FromDef(ConditionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("Condition args is null");

            if (!args.TryGetValue("key", out var keyObj) || !(keyObj is string key) || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("ArgGreaterThanCondition requires args['key'] as non-empty string");
            }

            var threshold = ValueSourceRuntimeUtil.Parse(args);

            if (threshold.Kind == ValueSourceKind.Const)
            {
                var boxed = threshold.ConstValue;
                if (!ValueSourceRuntimeUtil.TryToDouble(boxed, out var d))
                {
                    d = 0d;
                }
                threshold = new ValueSourceRuntime(ValueSourceKind.Const, d, threshold.FromScope, threshold.FromKey);
            }

            return new ArgGreaterThanCondition(key, threshold);
        }

        public bool Evaluate(TriggerContext context)
        {
            var args = context.Event.Args;
            if (args == null || !args.TryGetValue(_key, out var obj) || obj == null)
            {
                return false;
            }

            if (!ValueSourceRuntimeUtil.TryToDouble(obj, out var v)) return false;

            var tObjResolved = ValueSourceRuntimeUtil.Resolve(context, _threshold);
            if (!ValueSourceRuntimeUtil.TryToDouble(tObjResolved, out var threshold)) return false;

            return v > threshold;
        }
    }
}
