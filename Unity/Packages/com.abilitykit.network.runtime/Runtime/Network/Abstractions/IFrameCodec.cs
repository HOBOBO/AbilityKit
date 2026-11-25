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

    /// <summary>
    /// 内置流解码器支持的可选限制配置。
    /// </summary>
    public interface IFrameDecoderLimits
    {
        int MaxFrameLength { get; set; }
    }
}
