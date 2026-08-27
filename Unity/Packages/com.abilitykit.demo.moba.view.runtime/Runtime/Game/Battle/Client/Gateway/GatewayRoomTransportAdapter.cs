using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Gateway;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Game.Battle.Agent
{
    internal sealed class GatewayRoomTransportAdapter :
        IRoomGatewayRequestTransport,
        IRoomGatewayPushSource,
        IDisposable
    {
        private readonly Func<
            uint,
            ArraySegment<byte>,
            TimeSpan?,
            CancellationToken,
            Task<ArraySegment<byte>>> _sendRequestAsync;
        private readonly Action<Action<uint, ArraySegment<byte>>>
            _subscribeServerPush;
        private readonly Action<Action<uint, ArraySegment<byte>>>
            _unsubscribeServerPush;
        private readonly IDisposable _ownedRequestClient;
        private bool _disposed;

        public GatewayRoomTransportAdapter(IConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            var gateway = GatewayConnection.Create(connection);
            _sendRequestAsync = async (opCode, payload, timeout, cancellationToken) =>
            {
                var response = await gateway.SendRequestAsync(
                    opCode,
                    payload.Array == null ? Array.Empty<byte>() : payload.ToArray(),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new ArraySegment<byte>(response);
            };
            _subscribeServerPush =
                handler => gateway.ServerPushReceived += handler;
            _unsubscribeServerPush =
                handler => gateway.ServerPushReceived -= handler;
            _ownedRequestClient = gateway;
        }

        public GatewayRoomTransportAdapter(NetworkSdkClient sdkClient)
        {
            if (sdkClient == null)
            {
                throw new ArgumentNullException(nameof(sdkClient));
            }

            _sendRequestAsync = sdkClient.SendRawRequestAsync;
            _subscribeServerPush =
                handler => sdkClient.ServerPushReceived += handler;
            _unsubscribeServerPush =
                handler => sdkClient.ServerPushReceived -= handler;
        }

        internal GatewayRoomTransportAdapter(
            Func<
                uint,
                ArraySegment<byte>,
                TimeSpan?,
                CancellationToken,
                Task<ArraySegment<byte>>> sendRequestAsync,
            Action<Action<uint, ArraySegment<byte>>> subscribeServerPush,
            Action<Action<uint, ArraySegment<byte>>> unsubscribeServerPush,
            IDisposable ownedRequestClient = null)
        {
            _sendRequestAsync = sendRequestAsync ??
                throw new ArgumentNullException(nameof(sendRequestAsync));
            _subscribeServerPush = subscribeServerPush ??
                throw new ArgumentNullException(nameof(subscribeServerPush));
            _unsubscribeServerPush = unsubscribeServerPush ??
                throw new ArgumentNullException(nameof(unsubscribeServerPush));
            _ownedRequestClient = ownedRequestClient;
        }

        public event Action<uint, ArraySegment<byte>> ServerPushReceived
        {
            add
            {
                ThrowIfDisposed();
                _subscribeServerPush(value);
            }
            remove
            {
                if (!_disposed)
                {
                    _unsubscribeServerPush(value);
                }
            }
        }

        public Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _sendRequestAsync(
                opCode,
                payload,
                timeout,
                cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ownedRequestClient?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(GatewayRoomTransportAdapter));
            }
        }
    }
}
