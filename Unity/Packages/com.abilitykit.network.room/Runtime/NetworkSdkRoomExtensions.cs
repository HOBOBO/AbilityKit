#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Network.Room
{
    /// <summary>
    /// Adds the shared Room wire capability without creating another request client.
    /// </summary>
    public static class NetworkSdkRoomExtensions
    {
        public static RoomGatewayWireSessionClient CreateRoomClient(
            this NetworkSdkClient client,
            RoomGatewayWireOpCodes? opCodes = null)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            var transport = new NetworkSdkRoomTransport(client);
            return new RoomGatewayWireSessionClient(
                transport,
                transport,
                transport,
                opCodes);
        }

        private sealed class NetworkSdkRoomTransport :
            IRoomGatewayRequestTransport,
            IRoomGatewayPushSource,
            IDisposable
        {
            private readonly NetworkSdkClient _client;
            private bool _disposed;

            public NetworkSdkRoomTransport(NetworkSdkClient client)
            {
                _client = client;
                _client.ServerPushReceived += ForwardServerPush;
            }

            public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

            public Task<ArraySegment<byte>> SendRequestAsync(
                uint opCode,
                ArraySegment<byte> payload,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(NetworkSdkRoomTransport));
                }

                return _client.SendRawRequestAsync(
                    opCode,
                    payload,
                    timeout,
                    cancellationToken);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _client.ServerPushReceived -= ForwardServerPush;
                ServerPushReceived = null;
            }

            private void ForwardServerPush(uint opCode, ArraySegment<byte> payload)
            {
                ServerPushReceived?.Invoke(opCode, payload);
            }
        }
    }
}
