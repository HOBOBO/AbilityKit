using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Definitions
{
    [Serializable]
    public sealed class ConditionDef
    {
        public ConditionDef(string type, Dictionary<string, object> args = null)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Args = args;
        }

        public string Type { get; }
        public IReadOnlyDictionary<string, object> Args { get; }
    }
}
