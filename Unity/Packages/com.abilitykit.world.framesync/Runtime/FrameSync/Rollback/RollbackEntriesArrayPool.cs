using System;
using AbilityKit.Core.Common.Pool;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    internal static class RollbackEntriesArrayPool
    {
        public static WorldRollbackSnapshotEntry[] Rent(int length)
        {
            if (length <= 0) return Array.Empty<WorldRollbackSnapshotEntry>();

            var key = new PoolKey($"rollback.entries[{length}]");
            var pool = Pools.GetPool(
                key: key,
                createFunc: () => new WorldRollbackSnapshotEntry[length],
                onRelease: arr => Array.Clear(arr, 0, arr.Length),
                defaultCapacity: 8,
                maxSize: 256,
                collectionCheck: false);

            return pool.Get();
        }

        public static void Release(WorldRollbackSnapshotEntry[] entries)
        {
            if (entries == null) return;
            if (entries.Length == 0) return;

            var key = new PoolKey($"rollback.entries[{entries.Length}]");
            Pools.GetPool(
                key: key,
                createFunc: () => new WorldRollbackSnapshotEntry[entries.Length],
                onRelease: arr => Array.Clear(arr, 0, arr.Length),
                defaultCapacity: 8,
                maxSize: 256,
                collectionCheck: false).Release(entries);
        }
    }
}
