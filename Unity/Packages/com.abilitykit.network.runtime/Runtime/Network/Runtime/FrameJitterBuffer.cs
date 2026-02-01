using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Runtime
{
    public enum MissingFrameMode
    {
        Wait = 0,
        FillDefault = 1,
    }

    public sealed class FrameJitterBuffer<T> : IConsumableRemoteFrameSource<T>, IRemoteFrameSink<T>
    {
        private readonly Dictionary<int, T> _byFrame;
        private int _maxReceivedFrame;

        private int _lastConsumedFrame;
        private long _addedCount;
        private long _duplicateCount;
        private long _lateCount;
        private long _consumedCount;
        private long _filledDefaultCount;

        private int _minBufferedFrame;
        private bool _minDirty;

        private Func<T> _missingFrameFactory;

        public MissingFrameMode MissingMode { get; set; }

        public FrameJitterBuffer(int delayFrames, int initialCapacity = 256)
        {
            if (delayFrames < 0) throw new ArgumentOutOfRangeException(nameof(delayFrames));
            if (initialCapacity <= 0) initialCapacity = 16;

            DelayFrames = delayFrames;
            _byFrame = new Dictionary<int, T>(initialCapacity);
            _maxReceivedFrame = -1;

            _lastConsumedFrame = -1;
            _minBufferedFrame = -1;
            _minDirty = false;

            MissingMode = MissingFrameMode.Wait;
            _missingFrameFactory = () => default;
        }

        public FrameJitterBuffer(int delayFrames, MissingFrameMode missingMode, Func<T> missingFrameFactory, int initialCapacity = 256)
            : this(delayFrames, initialCapacity)
        {
            MissingMode = missingMode;
            if (missingFrameFactory != null) _missingFrameFactory = missingFrameFactory;
        }

        public int DelayFrames { get; set; }

        public int MaxReceivedFrame => _maxReceivedFrame;

        public int TargetFrame => _maxReceivedFrame - DelayFrames;

        public int LastConsumedFrame => _lastConsumedFrame;

        public long AddedCount => _addedCount;

        public long DuplicateCount => _duplicateCount;

        public long LateCount => _lateCount;

        public long ConsumedCount => _consumedCount;

        public long FilledDefaultCount => _filledDefaultCount;

        public int MinBufferedFrame
        {
            get
            {
                if (_byFrame.Count == 0) return -1;
                if (_minDirty) RecomputeMinBuffered();
                return _minBufferedFrame;
            }
        }

        public int MaxBufferedFrame => _maxReceivedFrame;

        public int Count => _byFrame.Count;

        public void Dispose()
        {
            Clear();
        }

        public void Clear()
        {
            _byFrame.Clear();
            _maxReceivedFrame = -1;

            _lastConsumedFrame = -1;
            _addedCount = 0;
            _duplicateCount = 0;
            _lateCount = 0;
            _consumedCount = 0;
            _filledDefaultCount = 0;

            _minBufferedFrame = -1;
            _minDirty = false;
        }

        public void Add(int frame, T value)
        {
            if (frame < 0) return;

            if (frame <= _lastConsumedFrame)
            {
                _lateCount++;
                return;
            }

            _addedCount++;
            if (_byFrame.ContainsKey(frame))
            {
                _duplicateCount++;
            }

            _byFrame[frame] = value;
            if (frame > _maxReceivedFrame) _maxReceivedFrame = frame;

            if (_byFrame.Count == 1)
            {
                _minBufferedFrame = frame;
                _minDirty = false;
            }
            else if (!_minDirty && (_minBufferedFrame < 0 || frame < _minBufferedFrame))
            {
                _minBufferedFrame = frame;
            }
        }

        public bool TryGet(int frame, out T value)
        {
            return _byFrame.TryGetValue(frame, out value);
        }

        public bool TryConsume(int frame, out T value)
        {
            if (_byFrame.TryGetValue(frame, out value))
            {
                _byFrame.Remove(frame);

                _lastConsumedFrame = frame;
                _consumedCount++;

                if (!_minDirty && frame == _minBufferedFrame)
                {
                    _minDirty = true;
                }
                return true;
            }

            if (MissingMode == MissingFrameMode.FillDefault && frame <= TargetFrame)
            {
                value = _missingFrameFactory != null ? _missingFrameFactory.Invoke() : default;

                _lastConsumedFrame = frame;
                _consumedCount++;
                _filledDefaultCount++;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryRemove(int frame, out T value)
        {
            if (_byFrame.TryGetValue(frame, out value))
            {
                _byFrame.Remove(frame);

                if (!_minDirty && frame == _minBufferedFrame)
                {
                    _minDirty = true;
                }
                return true;
            }

            value = default;
            return false;
        }

        public void TrimBefore(int minFrameInclusive)
        {
            if (_byFrame.Count == 0) return;

            var toRemove = (List<int>)null;
            foreach (var k in _byFrame.Keys)
            {
                if (k < minFrameInclusive)
                {
                    toRemove ??= new List<int>(32);
                    toRemove.Add(k);
                }
            }

            if (toRemove == null) return;
            for (int i = 0; i < toRemove.Count; i++)
            {
                _byFrame.Remove(toRemove[i]);
            }

            _minDirty = true;
        }

        private void RecomputeMinBuffered()
        {
            var min = int.MaxValue;
            foreach (var k in _byFrame.Keys)
            {
                if (k < min) min = k;
            }

            _minBufferedFrame = min == int.MaxValue ? -1 : min;
            _minDirty = false;
        }
    }
}
