using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Buffer
{
    public sealed class InputBuffer
    {
        private readonly Dictionary<int, PlayerInputCommand> _inputs = new Dictionary<int, PlayerInputCommand>();
        private readonly List<int> _frames = new List<int>();
        private readonly int _maxBufferSize;
        private readonly int _localPlayerId;
        private readonly object _lock = new object();

        public int LocalPlayerId => _localPlayerId;
        public int Count => _frames.Count;

        public InputBuffer(int localPlayerId, int maxBufferSize = 128)
        {
            _localPlayerId = localPlayerId;
            _maxBufferSize = maxBufferSize;
        }

        public void Store(int frame, PlayerInputCommand input)
        {
            lock (_lock)
            {
                _inputs[frame] = input;
                if (!_frames.Contains(frame))
                {
                    _frames.Add(frame);
                    _frames.Sort();
                }
                TrimBuffer();
            }
        }

        public bool TryGet(int frame, out PlayerInputCommand input)
        {
            lock (_lock)
            {
                return _inputs.TryGetValue(frame, out input);
            }
        }

        public bool TryGetLocalInput(int frame, out PlayerInputCommand input)
        {
            if (TryGet(frame, out input))
            {
                return input.PlayerId == _localPlayerId;
            }
            input = default;
            return false;
        }

        public bool PeekLocalInput(int frame, out PlayerInputCommand input)
        {
            lock (_lock)
            {
                for (int i = _frames.Count - 1; i >= 0; i--)
                {
                    int f = _frames[i];
                    if (f <= frame && _inputs.TryGetValue(f, out var cmd) && cmd.PlayerId == _localPlayerId)
                    {
                        input = cmd;
                        return true;
                    }
                }
                input = default;
                return false;
            }
        }

        public bool Contains(int frame)
        {
            lock (_lock)
            {
                return _inputs.ContainsKey(frame);
            }
        }

        public IReadOnlyList<PlayerInputCommand> GetInputsInRange(int startFrame, int endFrame)
        {
            lock (_lock)
            {
                var result = new List<PlayerInputCommand>();
                foreach (var frame in _frames)
                {
                    if (frame >= startFrame && frame <= endFrame && _inputs.TryGetValue(frame, out var input))
                    {
                        result.Add(input);
                    }
                }
                return result;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _inputs.Clear();
                _frames.Clear();
            }
        }

        public void RemoveBefore(int frame)
        {
            lock (_lock)
            {
                var framesToRemove = new List<int>();
                foreach (var f in _frames)
                {
                    if (f < frame) framesToRemove.Add(f);
                }

                foreach (var f in framesToRemove)
                {
                    _inputs.Remove(f);
                    _frames.Remove(f);
                }
            }
        }

        private void TrimBuffer()
        {
            while (_frames.Count > _maxBufferSize)
            {
                int earliestFrame = _frames[0];
                _inputs.Remove(earliestFrame);
                _frames.RemoveAt(0);
            }
        }

        public int GetInputCount()
        {
            lock (_lock)
            {
                return _frames.Count;
            }
        }

        public int GetLatestFrame()
        {
            lock (_lock)
            {
                return _frames.Count > 0 ? _frames[_frames.Count - 1] : -1;
            }
        }
    }
}
