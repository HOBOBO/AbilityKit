using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Definitions;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class TriggerRegistry
    {
        private readonly Dictionary<string, IConditionFactory> _conditionFactories = new Dictionary<string, IConditionFactory>(StringComparer.Ordinal);
        private readonly Dictionary<string, IActionFactory> _actionFactories = new Dictionary<string, IActionFactory>(StringComparer.Ordinal);

        public void RegisterCondition(string type, IConditionFactory factory)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _conditionFactories[type] = factory;
        }

        public void RegisterAction(string type, IActionFactory factory)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _actionFactories[type] = factory;
        }

        public ITriggerCondition CreateCondition(ConditionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            if (!_conditionFactories.TryGetValue(def.Type, out var factory))
            {
                throw new InvalidOperationException($"Condition type not registered: {def.Type}");
            }

            return factory.Create(def);
        }

        public ITriggerAction CreateAction(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            if (!_actionFactories.TryGetValue(def.Type, out var factory))
            {
                throw new InvalidOperationException($"Action type not registered: {def.Type}");
            }

            return factory.Create(def);
        }
    }
}
