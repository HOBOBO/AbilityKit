using System;

namespace AbilityKit.Protocol
{
    /// <summary>Serializes protocol payload DTOs independently of transport framing and routing.</summary>
    public interface INetworkProtocolCodec
    {
        ArraySegment<byte> Serialize<T>(in T value);
        T Deserialize<T>(ArraySegment<byte> payload);
    }
}
