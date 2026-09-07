using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Definition
{
    public sealed class BlackboardSchema
    {
        public List<BlackboardKeyDefinition> Keys { get; set; } = new();

        public bool TryGetType(string name, out ValueType type)
        {
            foreach (var key in Keys)
            {
                if (string.Equals(key.Name, name, StringComparison.Ordinal))
                {
                    type = key.Type;
                    return true;
                }
            }
            type = default;
            return false;
        }

        internal AbilityKit.BehaviorTree.BtBlackboardSchema ToLegacy()
        {
            var schema = new AbilityKit.BehaviorTree.BtBlackboardSchema();
            foreach (var key in Keys)
            {
                schema.Keys.Add(key.ToLegacy());
            }
            return schema;
        }

        internal static BlackboardSchema FromLegacy(AbilityKit.BehaviorTree.BtBlackboardSchema source)
        {
            var schema = new BlackboardSchema();
            foreach (var key in source.Keys)
            {
                schema.Keys.Add(BlackboardKeyDefinition.FromLegacy(key));
            }
            return schema;
        }
    }
}
