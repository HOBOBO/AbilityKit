using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime.Builtins;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class TriggerCompiler : IConditionCompiler
    {
        private readonly TriggerRegistry _registry;

        public TriggerCompiler(TriggerRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public TriggerInstance Compile(TriggerDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var conditions = new List<ITriggerCondition>(def.Conditions.Count);
            for (int i = 0; i < def.Conditions.Count; i++)
            {
                conditions.Add(Compile(def.Conditions[i]));
            }

            var actions = new List<ITriggerAction>(def.Actions.Count);
            for (int i = 0; i < def.Actions.Count; i++)
            {
                actions.Add(_registry.CreateAction(def.Actions[i]));
            }

            return new TriggerInstance(def.EventId, conditions, actions);
        }

        public ITriggerCondition Compile(ConditionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            if (string.Equals(def.Type, "all", StringComparison.Ordinal))
            {
                return AllCondition.FromDef(def, this);
            }

            if (string.Equals(def.Type, "any", StringComparison.Ordinal))
            {
                return AnyCondition.FromDef(def, this);
            }

            if (string.Equals(def.Type, "not", StringComparison.Ordinal))
            {
                return NotCondition.FromDef(def, this);
            }

            return _registry.CreateCondition(def);
        }
    }
}
