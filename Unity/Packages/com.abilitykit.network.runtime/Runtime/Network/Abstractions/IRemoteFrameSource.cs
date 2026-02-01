using System;

namespace AbilityKit.Network.Abstractions
{
    public interface IRemoteFrameSource<TFrame> : IDisposable
    {
        int DelayFrames { get; set; }

        int MaxReceivedFrame { get; }

        int TargetFrame { get; }

        bool TryGet(int frame, out TFrame frameData);

        void TrimBefore(int minFrameInclusive);
    }
}
