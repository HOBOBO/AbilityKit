using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class AllCondition : ITriggerCondition
    {
        private readonly List<ITriggerCondition> _children;

        public AllCondition(IReadOnlyList<ITriggerCondition> children)
        {
            if (children == null) throw new ArgumentNullException(nameof(children));
            _children = new List<ITriggerCondition>(children);
        }

        private AllCondition(List<ITriggerCondition> children)
        {
            if (children == null) throw new ArgumentNullException(nameof(children));
            _children = children;
        }

        public static AllCondition FromDef(ConditionDef def, IConditionCompiler compiler)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (compiler == null) throw new ArgumentNullException(nameof(compiler));

            var args = def.Args;
            if (args == null) throw new InvalidOperationException("all condition requires args");
            if (!args.TryGetValue(TriggerDefArgKeys.Items, out var itemsObj) || !(itemsObj is IList<ConditionDef> items))
            {
                throw new InvalidOperationException("all condition requires args['items'] as IList<ConditionDef>");
            }

            var compiled = new List<ITriggerCondition>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                compiled.Add(compiler.Compile(items[i]));
            }

            return new AllCondition(compiled);
        }

        public bool Evaluate(TriggerContext context)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (!_children[i].Evaluate(context)) return false;
            }
            return true;
        }
    }
}
