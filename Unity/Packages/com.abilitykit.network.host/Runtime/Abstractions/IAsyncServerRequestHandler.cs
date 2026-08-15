using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Host
{
    public interface IAsyncServerRequestHandler
    {
        Task HandleAsync(
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken);
    }
}
