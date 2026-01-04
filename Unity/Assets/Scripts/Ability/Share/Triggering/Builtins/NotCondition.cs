using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;

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

            if (args.TryGetValue("item", out var itemObj) && itemObj is ConditionDef item)
            {
                return new NotCondition(compiler.Compile(item));
            }

            if (args.TryGetValue("items", out var itemsObj) && itemsObj is IList<ConditionDef> items && items.Count > 0)
            {
                return new NotCondition(compiler.Compile(items[0]));
            }

            throw new InvalidOperationException("not condition requires args['item'] as ConditionDef or args['items'][0]");
        }

        public bool Evaluate(TriggerContext context)
        {
            return !_child.Evaluate(context);
        }
    }
}
