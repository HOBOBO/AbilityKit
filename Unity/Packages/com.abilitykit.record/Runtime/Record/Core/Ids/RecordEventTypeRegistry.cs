using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Record.Core
{
    public sealed class RecordEventTypeRegistry
    {
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

        public bool TryRegister(string name, out RecordEventType type)
        {
            type = RecordEventType.FromName(name);
            if (_names.TryGetValue(type.Value, out var existing))
            {
                return string.Equals(existing, name, StringComparison.Ordinal);
            }

            _names[type.Value] = name;
            return true;
        }

        public bool TryGetName(RecordEventType type, out string name)
        {
            return _names.TryGetValue(type.Value, out name);
        }
    }
}
