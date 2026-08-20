using System.Collections.Generic;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.Host.Extensions.Server.BattleHost
{
    public readonly struct BattleInputDrainResult<TInput>
    {
        public readonly int Frame;
        public readonly IReadOnlyList<TInput> Inputs;

        public BattleInputDrainResult(int frame, IReadOnlyList<TInput> inputs)
        {
            Frame = frame;
            Inputs = inputs;
        }

        public int Count => Inputs?.Count ?? 0;
    }

    public interface IBattleInputBuffer<TInput>
    {
        int PendingFrameCount { get; }

        bool Enqueue(int frame, TInput input);

        BattleInputDrainResult<TInput> Drain(int frame);

        void ClearFrame(int frame);

        void ClearBefore(int frame);

        void Clear();
    }

    public sealed class BattleInputBuffer<TInput> : IBattleInputBuffer<TInput>, IBufferCapacityControl
    {
        private readonly Dictionary<int, List<TInput>> _inputsByFrame = new Dictionary<int, List<TInput>>();
        private readonly int _initialFrameCapacity;
        private int _maxPendingFrames;

        public BattleInputBuffer(int initialFrameCapacity = 8, int maxPendingFrames = 0)
        {
            if (maxPendingFrames < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(maxPendingFrames));
            }

            _initialFrameCapacity = initialFrameCapacity > 0 ? initialFrameCapacity : 8;
            _maxPendingFrames = maxPendingFrames == 0 ? int.MaxValue : maxPendingFrames;
        }

        public int PendingFrameCount => _inputsByFrame.Count;

        public int Capacity => _maxPendingFrames;

        public bool IsCapacityBounded => _maxPendingFrames != int.MaxValue;

        public bool Enqueue(int frame, TInput input)
        {
            if (frame < 0)
            {
                return false;
            }

            if (!_inputsByFrame.TryGetValue(frame, out var list))
            {
                if (_inputsByFrame.Count >= _maxPendingFrames)
                {
                    return false;
                }

                list = new List<TInput>(_initialFrameCapacity);
                _inputsByFrame[frame] = list;
            }

            list.Add(input);
            return true;
        }

        public BattleInputDrainResult<TInput> Drain(int frame)
        {
            if (!_inputsByFrame.TryGetValue(frame, out var list) || list == null || list.Count == 0)
            {
                _inputsByFrame.Remove(frame);
                return new BattleInputDrainResult<TInput>(frame, System.Array.Empty<TInput>());
            }

            _inputsByFrame.Remove(frame);
            return new BattleInputDrainResult<TInput>(frame, list);
        }

        public void ClearFrame(int frame)
        {
            _inputsByFrame.Remove(frame);
        }

        public void ClearBefore(int frame)
        {
            if (_inputsByFrame.Count == 0)
            {
                return;
            }

            var keysToRemove = new List<int>();
            foreach (var pair in _inputsByFrame)
            {
                if (pair.Key < frame)
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _inputsByFrame.Remove(keysToRemove[i]);
            }
        }

        public void Clear()
        {
            _inputsByFrame.Clear();
        }

        public bool TrySetCapacity(int capacity)
        {
            if (capacity <= 0 || capacity < _inputsByFrame.Count)
            {
                return false;
            }

            _maxPendingFrames = capacity;
            return true;
        }

        public void RemoveCapacityLimit()
        {
            _maxPendingFrames = int.MaxValue;
        }
    }
}
