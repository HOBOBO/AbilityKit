using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Identifiers
{
    /// <summary>
    /// Maintains a reversible mapping between ordinal strings and deterministic V1 hash identifiers.
    /// The registry is not thread-safe and rejects hash collisions between different strings.
    /// </summary>
    public sealed class StableStringIdRegistry
    {
        private readonly Dictionary<string, int> _nameToId = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _idToName = new Dictionary<int, string>();

        /// <summary>Gets the existing identifier for a name or registers its deterministic identifier.</summary>
        /// <param name="name">The non-null name to resolve.</param>
        /// <returns>The stable V1 identifier associated with <paramref name="name"/>.</returns>
        public int GetOrRegister(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (_nameToId.TryGetValue(name, out var id))
            {
                return id;
            }

            id = StableHashV1.Fnv1a32Utf16(name);

            if (_idToName.TryGetValue(id, out var existingName) && existingName != name)
            {
                throw new InvalidOperationException($"StableStringIdRegistry hash collision: '{existingName}' and '{name}' => {id}");
            }

            _nameToId[name] = id;
            _idToName[id] = name;
            return id;
        }

        /// <summary>Attempts to resolve a previously registered name to its identifier.</summary>
        /// <param name="name">The name to resolve; <see langword="null"/> is treated as not found.</param>
        /// <param name="id">Receives the identifier when found, or zero otherwise.</param>
        /// <returns><see langword="true"/> when the name is registered.</returns>
        public bool TryGetId(string name, out int id)
        {
            if (name == null)
            {
                id = default;
                return false;
            }

            return _nameToId.TryGetValue(name, out id);
        }

        /// <summary>Attempts to resolve a previously registered identifier to its name.</summary>
        /// <param name="id">The identifier to resolve.</param>
        /// <param name="name">Receives the registered name when found, or an empty string otherwise.</param>
        /// <returns><see langword="true"/> when the identifier is registered.</returns>
        public bool TryGetName(int id, out string name)
        {
            if (_idToName.TryGetValue(id, out var registeredName))
            {
                name = registeredName;
                return true;
            }

            name = string.Empty;
            return false;
        }

    }
}
