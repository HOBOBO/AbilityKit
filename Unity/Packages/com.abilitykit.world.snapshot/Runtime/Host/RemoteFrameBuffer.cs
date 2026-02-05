using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Ability.Host
{
    public sealed class RemoteFrameBuffer<TFrame> : IRemoteFrameSource<TFrame>, IRemoteFrameSink<TFrame>
    {
        private readonly Dictionary<int, TFrame> _byFrame;
        private int _maxReceivedFrame;

        public RemoteFrameBuffer(int initialCapacity = 256)
        {
            if (initialCapacity <= 0) initialCapacity = 16;
            _byFrame = new Dictionary<int, TFrame>(initialCapacity);
            _maxReceivedFrame = -1;
        }

        public int DelayFrames { get; set; }

        public int MaxReceivedFrame => _maxReceivedFrame;

        public int TargetFrame => _maxReceivedFrame - DelayFrames;

        public bool TryGet(int frame, out TFrame frameData)
        {
            return _byFrame.TryGetValue(frame, out frameData);
        }

        public void TrimBefore(int minFrameInclusive)
        {
            if (_byFrame.Count == 0) return;

            var keys = new List<int>(_byFrame.Count);
            foreach (var kv in _byFrame)
            {
                if (kv.Key < minFrameInclusive) keys.Add(kv.Key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                _byFrame.Remove(keys[i]);
            }
        }

        public void Add(int frame, TFrame frameData)
        {
            if (frame < 0) return;
            _byFrame[frame] = frameData;
            if (frame > _maxReceivedFrame) _maxReceivedFrame = frame;
        }

        public void Dispose()
        {
            _byFrame.Clear();
            _maxReceivedFrame = -1;
        }
    }
}
