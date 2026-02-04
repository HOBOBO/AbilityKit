using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Blackboard
{
    public sealed class BlackboardKeyRegistry
    {
        private readonly Dictionary<int, BlackboardKeyMeta> _byId = new Dictionary<int, BlackboardKeyMeta>();

        public void Register(in BlackboardKeyMeta meta)
        {
            if (meta.Id == 0) throw new ArgumentOutOfRangeException(nameof(meta.Id), "id must not be 0");
            if (string.IsNullOrEmpty(meta.Name)) throw new ArgumentException("name is null or empty", nameof(meta));

            if (_byId.TryGetValue(meta.Id, out var existing))
            {
                if (!string.Equals(existing.Name, meta.Name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"BlackboardKeyRegistry collision: '{existing.Name}' and '{meta.Name}' => {meta.Id}");
                }
            }

            _byId[meta.Id] = meta;
        }

        public bool TryGet(int keyId, out BlackboardKeyMeta meta)
        {
            return _byId.TryGetValue(keyId, out meta);
        }

        public bool TryValidate(in BlackboardKeyRef keyRef, out BlackboardKeyMeta meta)
        {
            if (!_byId.TryGetValue(keyRef.Id, out meta)) return false;

            if (!string.IsNullOrEmpty(keyRef.Name) && !string.Equals(meta.Name, keyRef.Name, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}
