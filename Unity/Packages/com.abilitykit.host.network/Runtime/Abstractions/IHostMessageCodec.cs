using System;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>Maps host-domain messages to the shared network envelope.</summary>
    public interface IHostMessageCodec
    {
        bool TryEncode(
            ServerMessage message,
            out NetworkPacketHeader header,
            out ArraySegment<byte> payload);
    }
}
