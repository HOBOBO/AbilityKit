using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class SequenceAction : ITriggerAction
    {
        private readonly List<ITriggerAction> _items;

        public SequenceAction(IReadOnlyList<ITriggerAction> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items = new List<ITriggerAction>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null) _items.Add(items[i]);
            }
        }

        public static SequenceAction FromDef(ActionDef def, TriggerRegistry registry)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var args = def.Args;
            if (args == null) throw new InvalidOperationException("seq action requires args");
            if (!args.TryGetValue("items", out var itemsObj) || !(itemsObj is IList<ActionDef> items))
            {
                throw new InvalidOperationException("seq action requires args['items'] as IList<ActionDef>");
            }

            var compiled = new List<ITriggerAction>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var childDef = items[i];
                if (childDef == null) continue;
                compiled.Add(registry.CreateAction(childDef));
            }

            return new SequenceAction(compiled);
        }

        public void Execute(TriggerContext context)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Execute(context);
            }
        }
    }
}
