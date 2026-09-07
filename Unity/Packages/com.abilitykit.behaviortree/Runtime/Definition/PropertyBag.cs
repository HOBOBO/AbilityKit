using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Definition
{
    public sealed class PropertyBag
    {
        private readonly SortedDictionary<string, PropertyValue> _values = new();

        public IReadOnlyDictionary<string, PropertyValue> Values => _values;
        public IEnumerable<string> Keys => _values.Keys;

        public bool TryGet(string name, out PropertyValue value) => _values.TryGetValue(name, out value!);
        public bool ContainsKey(string name) => _values.ContainsKey(name);
        public void Set(string name, PropertyValue value) => _values[name] = value ?? throw new ArgumentNullException(nameof(value));




    }
}
