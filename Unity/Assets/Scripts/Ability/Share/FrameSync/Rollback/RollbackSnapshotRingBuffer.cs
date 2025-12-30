using System;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackSnapshotRingBuffer
    {
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
            var idx = Mod(snapshot.Frame.Value, _capacity);
            _frames[idx] = snapshot.Frame;
            _snapshots[idx] = snapshot;
            _has[idx] = true;
        }

        public bool TryGet(FrameIndex frame, out WorldRollbackSnapshot snapshot)
        {
            var idx = Mod(frame.Value, _capacity);
            if (_has[idx] && _frames[idx].Value == frame.Value)
            {
                snapshot = _snapshots[idx];
                return true;
            }

            snapshot = default;
            return false;
        }

        public void Clear()
        {
            Array.Clear(_has, 0, _has.Length);
        }

        private static int Mod(int x, int m)
        {
            var r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
