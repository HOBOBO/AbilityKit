using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    /// <summary>
    /// Immutable index over one rollback snapshot's provider payloads. Payload memory is exposed
    /// as read-only so cross-provider validation cannot mutate the snapshot before import.
    /// </summary>
    public sealed class RollbackSnapshotPreflightContext
    {
        private readonly IReadOnlyDictionary<int, ReadOnlyMemory<byte>> _payloads;

        public RollbackSnapshotPreflightContext(
            FrameIndex frame,
            IReadOnlyList<WorldRollbackSnapshotEntry> entries,
            IReadOnlyDictionary<int, IRollbackStateProvider> providers)
        {
            Frame = frame;
            var payloads = new Dictionary<int, ReadOnlyMemory<byte>>();
            var providerCopy = new Dictionary<int, IRollbackStateProvider>();
            if (providers != null)
                foreach (var provider in providers)
                    providerCopy.Add(provider.Key, provider.Value);
            Providers = new ReadOnlyDictionary<int, IRollbackStateProvider>(providerCopy);
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (!payloads.TryAdd(entry.Key, entry.Payload ?? Array.Empty<byte>()))
                        throw new InvalidOperationException($"Rollback snapshot contains duplicate provider key: {entry.Key}");
                }
            }
            _payloads = new ReadOnlyDictionary<int, ReadOnlyMemory<byte>>(payloads);
        }

        public FrameIndex Frame { get; }

        public IReadOnlyDictionary<int, IRollbackStateProvider> Providers { get; }

        public bool TryGetPayload(int key, out ReadOnlyMemory<byte> payload)
        {
            return _payloads.TryGetValue(key, out payload);
        }

        public ReadOnlyMemory<byte> RequirePayload(int key)
        {
            if (!_payloads.TryGetValue(key, out var payload))
                throw new InvalidOperationException($"Rollback snapshot is missing provider payload: {key}");
            return payload;
        }
    }
}
