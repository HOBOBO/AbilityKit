using System;
using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class InputHistoryRingBuffer
    {
        private readonly int _capacity;
        private readonly FrameIndex[] _frames;
        private readonly PlayerInputCommand[][] _inputs;
        private readonly bool[] _has;

        public InputHistoryRingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _frames = new FrameIndex[_capacity];
            _inputs = new PlayerInputCommand[_capacity][];
            _has = new bool[_capacity];
        }

        public int Capacity => _capacity;

        public void Store(FrameIndex frame, PlayerInputCommand[] inputs)
        {
            inputs ??= Array.Empty<PlayerInputCommand>();
            var idx = Mod(frame.Value, _capacity);
            _frames[idx] = frame;
            _inputs[idx] = inputs;
            _has[idx] = true;
        }

        public bool TryGet(FrameIndex frame, out PlayerInputCommand[] inputs)
        {
            var idx = Mod(frame.Value, _capacity);
            if (_has[idx] && _frames[idx].Value == frame.Value)
            {
                inputs = _inputs[idx] ?? Array.Empty<PlayerInputCommand>();
                return true;
            }

            inputs = Array.Empty<PlayerInputCommand>();
            return false;
        }

        public void Clear()
        {
            Array.Clear(_has, 0, _has.Length);
            Array.Clear(_inputs, 0, _inputs.Length);
        }

        private static int Mod(int x, int m)
        {
            var r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
