using System;

namespace AbilityKit.Network.Abstractions
{
    public interface IConsumableRemoteFrameSource<TFrame> : IRemoteFrameSource<TFrame>
    {
        bool TryConsume(int frame, out TFrame frameData);
    }
}
