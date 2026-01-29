using System;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Abstractions
{
    public interface INetworkMiddleware
    {
        void OnInbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next);

        void OnOutbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next);
    }
}
