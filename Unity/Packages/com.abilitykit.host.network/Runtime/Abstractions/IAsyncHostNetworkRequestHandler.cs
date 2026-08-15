using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Network
{
    public interface IAsyncHostNetworkRequestHandler
    {
        Task HandleAsync(
            HostRuntime runtime,
            ServerClientId clientId,
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken);
    }
}
