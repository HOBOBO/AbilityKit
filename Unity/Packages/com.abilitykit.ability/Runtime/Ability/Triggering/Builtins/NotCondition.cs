using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class NotCondition : ITriggerCondition
    {
        private readonly ITriggerCondition _child;

        public NotCondition(ITriggerCondition child)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
        }

        public static NotCondition FromDef(ConditionDef def, IConditionCompiler compiler)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (compiler == null) throw new ArgumentNullException(nameof(compiler));

            var args = def.Args;
            if (args == null) throw new InvalidOperationException("not condition requires args");

            if (args.TryGetValue(TriggerDefArgKeys.Item, out var itemObj) && itemObj is ConditionDef item)
            {
                return new NotCondition(compiler.Compile(item));
            }

            throw new InvalidOperationException("not condition requires args['item'] as ConditionDef");
        }

        public bool Evaluate(TriggerContext context)
        {
            return !_child.Evaluate(context);
        }
    }
}
