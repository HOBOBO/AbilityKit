using System;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Abstractions
{
    public interface IFrameCodec
    {
        IFrameDecoder CreateDecoder();

        ArraySegment<byte> Encode(NetworkPacketHeader header, ArraySegment<byte> payload);
    }

    public interface IFrameDecoder
    {
        void Reset();

        void Append(ArraySegment<byte> bytes);

        bool TryRead(out NetworkPacketHeader header, out ArraySegment<byte> payload);
    }
}
