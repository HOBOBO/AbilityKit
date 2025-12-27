using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class ArgEqualsCondition : ITriggerCondition
    {
        private readonly string _key;
        private readonly ValueSourceRuntime _expected;

        public ArgEqualsCondition(string key, ValueSourceRuntime expected)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _expected = expected;
        }

        public static ArgEqualsCondition FromDef(ConditionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("Condition args is null");

            if (!args.TryGetValue("key", out var keyObj) || !(keyObj is string key) || string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("ArgEqualsCondition requires args['key'] as non-empty string");
            }

            var expected = ValueSourceRuntimeUtil.Parse(args);
            return new ArgEqualsCondition(key, expected);
        }

        public bool Evaluate(TriggerContext context)
        {
            var eventArgs = context.Event.Args;
            if (eventArgs == null) return false;

            if (!eventArgs.TryGetValue(_key, out var actual)) return false;

            var expected = ValueSourceRuntimeUtil.Resolve(context, _expected);
            return Equals(actual, expected);
        }
    }
}
