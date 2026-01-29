using System;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class WorldStateHashRingBuffer
    {
        private readonly int _capacity;
        private readonly FrameIndex[] _frames;
        private readonly WorldStateHash[] _hashes;
        private readonly bool[] _has;

        public WorldStateHashRingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _frames = new FrameIndex[_capacity];
            _hashes = new WorldStateHash[_capacity];
            _has = new bool[_capacity];
        }

        public int Capacity => _capacity;

        public void Store(FrameIndex frame, WorldStateHash hash)
        {
            var idx = Mod(frame.Value, _capacity);
            _frames[idx] = frame;
            _hashes[idx] = hash;
            _has[idx] = true;
        }

        public bool TryGet(FrameIndex frame, out WorldStateHash hash)
        {
            var idx = Mod(frame.Value, _capacity);
            if (_has[idx] && _frames[idx].Value == frame.Value)
            {
                hash = _hashes[idx];
                return true;
            }

            hash = default;
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
