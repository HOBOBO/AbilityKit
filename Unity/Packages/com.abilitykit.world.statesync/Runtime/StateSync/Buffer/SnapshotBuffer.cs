using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Snapshot;
using AbilityKit.Core.Collections;

namespace AbilityKit.Ability.StateSync.Buffer
{
    public sealed class SnapshotBuffer
    {
        private readonly Dictionary<int, WorldStateSnapshot> _snapshots = new Dictionary<int, WorldStateSnapshot>();
        private readonly SortedIntSet _capturedFrames;
        private readonly int _maxBufferSize;
        private readonly object _lock = new object();

        public int Count => _capturedFrames.Count;
        public int MaxBufferSize => _maxBufferSize;

        public SnapshotBuffer(int maxBufferSize = 128)
        {
            if (maxBufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxBufferSize));
            _maxBufferSize = maxBufferSize;
            _capturedFrames = new SortedIntSet(maxBufferSize);
        }

        public void Store(int frame, WorldStateSnapshot snapshot)
        {
            lock (_lock)
            {
                if (_snapshots.ContainsKey(frame))
                {
                    _snapshots[frame] = snapshot.Clone();
                }
                else
                {
                    _snapshots[frame] = snapshot.Clone();
                    _capturedFrames.Add(frame);

                    TrimBuffer();
                }
            }
        }

        public bool TryGet(int frame, out WorldStateSnapshot snapshot)
        {
            lock (_lock)
            {
                if (_snapshots.TryGetValue(frame, out var s))
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
                return _snapshots.ContainsKey(frame);
            }
        }

        public IReadOnlyList<int> GetCapturedFrames()
        {
            lock (_lock)
            {
                var result = new int[_capturedFrames.Count];
                for (var index = 0; index < result.Length; index++)
                {
                    result[index] = _capturedFrames[index];
                }
                return result;
            }
        }

        public int GetLatestFrame()
        {
            lock (_lock)
            {
                return _capturedFrames.Count > 0 ? _capturedFrames[_capturedFrames.Count - 1] : -1;
            }
        }

        public int GetEarliestFrame()
        {
            lock (_lock)
            {
                return _capturedFrames.Count > 0 ? _capturedFrames[0] : -1;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _snapshots.Clear();
                _capturedFrames.Clear();
            }
        }

        public bool Remove(int frame)
        {
            lock (_lock)
            {
                if (_snapshots.Remove(frame))
                {
                    _capturedFrames.Remove(frame);
                    return true;
                }
                return false;
            }
        }

        public void RemoveBefore(int frame)
        {
            lock (_lock)
            {
                var removeCount = _capturedFrames.LowerBound(frame);
                for (var index = 0; index < removeCount; index++)
                {
                    _snapshots.Remove(_capturedFrames[index]);
                }

                if (removeCount > 0) _capturedFrames.RemoveRange(0, removeCount);
            }
        }

        public void RemoveAfter(int frame)
        {
            lock (_lock)
            {
                var firstRemoval = _capturedFrames.UpperBound(frame);
                for (var index = firstRemoval; index < _capturedFrames.Count; index++)
                {
                    _snapshots.Remove(_capturedFrames[index]);
                }

                var removeCount = _capturedFrames.Count - firstRemoval;
                if (removeCount > 0) _capturedFrames.RemoveRange(firstRemoval, removeCount);
            }
        }

        private void TrimBuffer()
        {
            var removeCount = _capturedFrames.Count - _maxBufferSize;
            for (var index = 0; index < removeCount; index++)
            {
                _snapshots.Remove(_capturedFrames[index]);
            }

            if (removeCount > 0) _capturedFrames.RemoveRange(0, removeCount);
        }

        internal void CopyCapturedFrames(List<int> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            lock (_lock)
            {
                destination.Clear();
                for (var index = 0; index < _capturedFrames.Count; index++)
                {
                    destination.Add(_capturedFrames[index]);
                }
            }
        }
    }
}
