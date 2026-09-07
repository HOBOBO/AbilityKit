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




    }
}
