using System;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackSnapshotRingBuffer
    {
        private readonly object _sync = new object();
        private readonly int _capacity;
        private readonly FrameIndex[] _frames;
        private readonly WorldRollbackSnapshot[] _snapshots;
        private readonly bool[] _has;

        public RollbackSnapshotRingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _frames = new FrameIndex[_capacity];
            _snapshots = new WorldRollbackSnapshot[_capacity];
            _has = new bool[_capacity];
        }

        public int Capacity => _capacity;

        public void Store(in WorldRollbackSnapshot snapshot)
        {
            var ownedEntries = CloneEntries(snapshot.Entries, usePool: true);
            var ownedSnapshot = new WorldRollbackSnapshot(snapshot.Version, snapshot.Frame, ownedEntries);
            var idx = Mod(snapshot.Frame.Value, _capacity);

            lock (_sync)
            {
                if (_has[idx])
                {
                    ReleaseSnapshot(_snapshots[idx]);
                }

                _frames[idx] = snapshot.Frame;
                _snapshots[idx] = ownedSnapshot;
                _has[idx] = true;
            }
        }

        public bool TryGet(FrameIndex frame, out WorldRollbackSnapshot snapshot)
        {
            lock (_sync)
            {
                var idx = Mod(frame.Value, _capacity);
                if (_has[idx] && _frames[idx].Value == frame.Value)
                {
                    var stored = _snapshots[idx];
                    snapshot = new WorldRollbackSnapshot(
                        stored.Version,
                        stored.Frame,
                        CloneEntries(stored.Entries, usePool: false));
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        public void Clear()
        {
            lock (_sync)
            {
                for (int i = 0; i < _has.Length; i++)
                {
                    if (_has[i])
                    {
                        ReleaseSnapshot(_snapshots[i]);
                        _snapshots[i] = default;
                        _frames[i] = default;
                    }
                }

                Array.Clear(_has, 0, _has.Length);
            }
        }

        private static WorldRollbackSnapshotEntry[] CloneEntries(
            WorldRollbackSnapshotEntry[] entries,
            bool usePool)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<WorldRollbackSnapshotEntry>();
            }

            var clone = usePool
                ? RollbackEntriesArrayPool.Rent(entries.Length)
                : new WorldRollbackSnapshotEntry[entries.Length];

            try
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    var payload = entry.Payload == null || entry.Payload.Length == 0
                        ? Array.Empty<byte>()
                        : (byte[])entry.Payload.Clone();
                    clone[i] = new WorldRollbackSnapshotEntry(entry.Key, payload);
                }

                return clone;
            }
            catch
            {
                if (usePool) RollbackEntriesArrayPool.Release(clone);
                throw;
            }
        }

        private static void ReleaseSnapshot(in WorldRollbackSnapshot snapshot)
        {
            RollbackEntriesArrayPool.Release(snapshot.Entries);
        }

        private static int Mod(int x, int m)
        {
            var r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
