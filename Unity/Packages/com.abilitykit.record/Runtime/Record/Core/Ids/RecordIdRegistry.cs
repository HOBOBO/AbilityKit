using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Record.Core
{
    public sealed class RecordIdRegistry
    {
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

        public bool TryRegister(string name, out RecordTrackId id)
        {
            id = RecordTrackId.FromName(name);
            if (_names.TryGetValue(id.Value, out var existing))
            {
                // If hash collides with another name, we report failure.
                return string.Equals(existing, name, StringComparison.Ordinal);
            }

            _names[id.Value] = name;
            return true;
        }

        public bool TryGetName(RecordTrackId id, out string name)
        {
            return _names.TryGetValue(id.Value, out name);
        }
    }
}
