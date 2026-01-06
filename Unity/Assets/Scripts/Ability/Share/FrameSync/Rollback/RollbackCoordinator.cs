using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Pool;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackCoordinator
    {
        private static readonly ObjectPool<List<WorldRollbackSnapshotEntry>> s_entriesListPool = Pools.GetPool(
            createFunc: () => new List<WorldRollbackSnapshotEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 16,
            maxSize: 256,
            collectionCheck: false);

        private readonly RollbackRegistry _registry;
        private readonly RollbackSnapshotRingBuffer _buffer;

        public RollbackCoordinator(RollbackRegistry registry, RollbackSnapshotRingBuffer buffer)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public bool CaptureAndStore(FrameIndex frame)
        {
            var snapshot = Capture(frame);
            _buffer.Store(snapshot);
            return true;
        }

        public void StoreSnapshot(in WorldRollbackSnapshot snapshot)
        {
            _buffer.Store(snapshot);
        }

        public WorldRollbackSnapshot Capture(FrameIndex frame)
        {
            var providers = _registry.Providers;
            var entries = s_entriesListPool.Get();
            if (entries.Capacity < providers.Count) entries.Capacity = providers.Count;

            try
            {
                for (int i = 0; i < providers.Count; i++)
                {
                    var p = providers[i];
                    if (p == null) continue;
                    var payload = p.Export(frame) ?? Array.Empty<byte>();
                    entries.Add(new WorldRollbackSnapshotEntry(p.Key, payload));
                }

                var arr = RollbackEntriesArrayPool.Rent(entries.Count);
                entries.CopyTo(arr, 0);
                return new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion, frame, arr);
            }
            finally
            {
                s_entriesListPool.Release(entries);
            }
        }

        public bool TryRestore(FrameIndex frame)
        {
            if (!_buffer.TryGet(frame, out var snapshot))
            {
                return false;
            }

            Restore(snapshot);
            return true;
        }

        public void Restore(in WorldRollbackSnapshot snapshot)
        {
            if (snapshot.Version != WorldRollbackSnapshotCodec.CurrentVersion)
            {
                throw new InvalidOperationException($"Unsupported rollback snapshot version: {snapshot.Version}");
            }

            var entries = snapshot.Entries;
            if (entries == null || entries.Length == 0) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (_registry.TryGet(e.Key, out var provider) && provider != null)
                {
                    provider.Import(snapshot.Frame, e.Payload);
                }
            }
        }

        public void ClearHistory()
        {
            _buffer.Clear();
        }
    }
}
