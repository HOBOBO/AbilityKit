using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Snapshot;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Buffer
{
    public sealed class SnapshotBuffer : IBufferCapacityControl
    {
        private readonly IFrameIndexedBuffer<WorldStateSnapshot> _storage;
        private readonly object _lock = new object();

        public int Count => _storage.Count;
        public int MaxBufferSize => _storage.Capacity;
        public int Capacity => _storage.Capacity;

        public SnapshotBuffer(int maxBufferSize = 128)
            : this(new SparseFrameIndexedBuffer<WorldStateSnapshot>(maxBufferSize))
        {
        }

        public SnapshotBuffer(IFrameIndexedBuffer<WorldStateSnapshot> storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Store(int frame, WorldStateSnapshot snapshot)
        {
            lock (_lock)
            {
                _storage.Store(frame, snapshot.Clone());
            }
        }

        public bool TryGet(int frame, out WorldStateSnapshot snapshot)
        {
            lock (_lock)
            {
                if (_storage.TryGet(frame, out var s))
                {
                    snapshot = s.Clone();
                    return true;
                }
                snapshot = null;
                return false;
            }
        }

        public WorldStateSnapshot Get(int frame)
        {
            TryGet(frame, out var snapshot);
            return snapshot;
        }

        public bool Contains(int frame)
        {
            lock (_lock)
            {
                return _storage.Contains(frame);
            }
        }

        public IReadOnlyList<int> GetCapturedFrames()
        {
            lock (_lock)
            {
                var result = new int[_storage.Count];
                for (var index = 0; index < result.Length; index++)
                {
                    result[index] = _storage.GetFrameAt(index);
                }
                return result;
            }
        }

        public int GetLatestFrame()
        {
            lock (_lock)
            {
                return _storage.Count > 0 ? _storage.GetFrameAt(_storage.Count - 1) : -1;
            }
        }

        public int GetEarliestFrame()
        {
            lock (_lock)
            {
                return _storage.Count > 0 ? _storage.GetFrameAt(0) : -1;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _storage.Clear();
            }
        }

        public bool Remove(int frame)
        {
            lock (_lock)
            {
                return _storage.Remove(frame);
            }
        }

        public void RemoveBefore(int frame)
        {
            lock (_lock)
            {
                _storage.RemoveBefore(frame);
            }
        }

        public void RemoveAfter(int frame)
        {
            lock (_lock)
            {
                _storage.RemoveAfter(frame);
            }
        }

        public bool TrySetCapacity(int capacity)
        {
            if (capacity <= 0) return false;

            lock (_lock)
            {
                return _storage.TrySetCapacity(capacity);
            }
        }

        internal void CopyCapturedFrames(List<int> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            lock (_lock)
            {
                destination.Clear();
                for (var index = 0; index < _storage.Count; index++)
                {
                    destination.Add(_storage.GetFrameAt(index));
                }
            }
        }
    }
}
