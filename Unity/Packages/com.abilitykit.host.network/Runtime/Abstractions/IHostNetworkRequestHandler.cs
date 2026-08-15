using System;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>Application-owned bridge from a wire request into host/world commands.</summary>
    public interface IHostNetworkRequestHandler
    {
        void Handle(
            HostRuntime runtime,
            ServerClientId clientId,
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload);
    }
}
