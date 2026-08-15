using System;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Host
{
    public interface IServerRequestHandler
    {
        void Handle(
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload);
    }
}
