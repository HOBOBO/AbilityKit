using System;

namespace AbilityKit.Network.Abstractions
{
    public interface IConsumableRemoteFrameSource<TFrame> : IRemoteFrameSource<TFrame>
    {
        int LastConsumedFrame { get; }

        bool TryConsume(int frame, out TFrame frameData);
    }
}
