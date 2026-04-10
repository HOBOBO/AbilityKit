using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Generic
{
    public sealed class StableStringIdRegistry
    {
        private readonly Dictionary<string, int> _nameToId = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _idToName = new Dictionary<int, string>();

        public int GetOrRegister(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (_nameToId.TryGetValue(name, out var id))
            {
                return id;
            }

            id = StableHash32(name);

            if (_idToName.TryGetValue(id, out var existingName) && existingName != name)
            {
                throw new InvalidOperationException($"StableStringIdRegistry hash collision: '{existingName}' and '{name}' => {id}");
            }

            _nameToId[name] = id;
            _idToName[id] = name;
            return id;
        }

        public bool TryGetId(string name, out int id)
        {
            if (name == null)
            {
                id = default;
                return false;
            }

            return _nameToId.TryGetValue(name, out id);
        }

        public bool TryGetName(int id, out string name) => _idToName.TryGetValue(id, out name);

        private static int StableHash32(string s)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261u;
                const uint prime = 16777619u;

                uint hash = offsetBasis;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= prime;
                }

                return (int)hash;
            }
        }
    }
}
