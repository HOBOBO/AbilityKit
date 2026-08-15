using System;
using System.Collections.Generic;
using AbilityKit.Core.Collections;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Ability.Host
{
    public sealed class RemoteFrameBuffer<TFrame> : IRemoteFrameSource<TFrame>, IRemoteFrameSink<TFrame>
    {
        private readonly Dictionary<int, TFrame> _byFrame;
        private readonly SortedIntSet _frames;
        private int _maxReceivedFrame;

        public RemoteFrameBuffer(int initialCapacity = 256)
        {
            if (initialCapacity <= 0) initialCapacity = 16;
            _byFrame = new Dictionary<int, TFrame>(initialCapacity);
            _frames = new SortedIntSet(initialCapacity);
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

            var removeCount = _frames.LowerBound(minFrameInclusive);
            for (var index = 0; index < removeCount; index++)
            {
                _byFrame.Remove(_frames[index]);
            }

            if (removeCount > 0) _frames.RemoveRange(0, removeCount);
        }

        public void Add(int frame, TFrame frameData)
        {
            if (frame < 0) return;
            _byFrame[frame] = frameData;
            _frames.Add(frame);
            if (frame > _maxReceivedFrame) _maxReceivedFrame = frame;
        }

        public void Dispose()
        {
            _byFrame.Clear();
            _frames.Clear();
            _maxReceivedFrame = -1;
        }
    }
}
