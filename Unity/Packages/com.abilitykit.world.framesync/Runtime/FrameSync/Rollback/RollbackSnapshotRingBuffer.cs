using System;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackSnapshotRingBuffer
    {
        private readonly object _sync = new object();
        private readonly int _capacity;
        private readonly FrameIndex[] _frames;
        private readonly int[] _versions;
        private readonly PooledBufferOwner<WorldRollbackSnapshotEntry>[] _entryOwners;
        private readonly bool[] _has;

        public RollbackSnapshotRingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _frames = new FrameIndex[_capacity];
            _versions = new int[_capacity];
            _entryOwners = new PooledBufferOwner<WorldRollbackSnapshotEntry>[_capacity];
            _has = new bool[_capacity];
        }

        public int Capacity => _capacity;

        public void Store(in WorldRollbackSnapshot snapshot)
        {
            Store(snapshot.Frame, snapshot.Entries, snapshot.Version);
        }

        /// <summary>Stores a deep-copy of <paramref name="entries"/>. Lets the internal capture→store path
        /// pass the pooled capture list directly (as a span) without an intermediate ToArray allocation.</summary>
        public void Store(FrameIndex frame, ReadOnlySpan<WorldRollbackSnapshotEntry> entries, int version)
        {
            var ownedEntries = CloneEntriesToOwner(entries);
            var idx = Mod(frame.Value, _capacity);
            PooledBufferOwner<WorldRollbackSnapshotEntry> previousOwner = null;

            try
            {
                lock (_sync)
                {
                    previousOwner = _entryOwners[idx];
                    _frames[idx] = frame;
                    _versions[idx] = version;
                    _entryOwners[idx] = ownedEntries;
                    _has[idx] = true;
                    ownedEntries = null;
                }
            }
            finally
            {
                ownedEntries?.Dispose();
            }

            previousOwner?.Dispose();
        }

        public bool TryGet(FrameIndex frame, out WorldRollbackSnapshot snapshot)
        {
            lock (_sync)
            {
                var idx = Mod(frame.Value, _capacity);
                if (_has[idx] && _frames[idx].Value == frame.Value)
                {
                    snapshot = new WorldRollbackSnapshot(
                        _versions[idx],
                        _frames[idx],
                        CloneEntriesToArray(_entryOwners[idx].Span));
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
                        _entryOwners[i]?.Dispose();
                        _entryOwners[i] = null;
                        _frames[i] = default;
                        _versions[i] = default;
                    }
                }

                Array.Clear(_has, 0, _has.Length);
            }
        }

        private static PooledBufferOwner<WorldRollbackSnapshotEntry> CloneEntriesToOwner(
            ReadOnlySpan<WorldRollbackSnapshotEntry> entries)
        {
            var owner = PooledBufferOwner<WorldRollbackSnapshotEntry>.Rent(
                entries.Length,
                PooledBufferClearMode.OnReturn);

            try
            {
                var clone = owner.Span;
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    var payload = entry.Payload == null || entry.Payload.Length == 0
                        ? Array.Empty<byte>()
                        : (byte[])entry.Payload.Clone();
                    clone[i] = new WorldRollbackSnapshotEntry(entry.Key, payload);
                }

                return owner;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }

        private static WorldRollbackSnapshotEntry[] CloneEntriesToArray(
            ReadOnlySpan<WorldRollbackSnapshotEntry> entries)
        {
            if (entries.IsEmpty)
            {
                return Array.Empty<WorldRollbackSnapshotEntry>();
            }

            var clone = new WorldRollbackSnapshotEntry[entries.Length];
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

        private static int Mod(int x, int m)
        {
            var r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
