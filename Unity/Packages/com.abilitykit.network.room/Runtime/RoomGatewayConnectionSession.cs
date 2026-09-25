#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Network.Room
{
    /// <summary>Owns a connected SDK client and its Room capability without choosing a login or room flow.</summary>
    public sealed class RoomGatewayConnectionSession : IDisposable
    {
        private readonly NetworkSdkClient _sdkClient;
        private readonly RoomGatewayWireSessionClient _roomClient;
        private readonly RoomGatewayConnectionRecovery _recovery;
        private bool _disposed;

        private RoomGatewayConnectionSession(NetworkSdkClient sdkClient, RoomGatewayWireSessionClient roomClient)
        {
            _sdkClient = sdkClient;
            _roomClient = roomClient;
            _recovery = new RoomGatewayConnectionRecovery(sdkClient);
        }

        public NetworkSdkClient SdkClient => _sdkClient;
        public RoomGatewayWireSessionClient RoomClient => _roomClient;
        public RoomGatewayConnectionRecovery Recovery => _recovery;

        public static async Task<RoomGatewayConnectionSession> ConnectAsync(
            string host, int port, Func<ITransport>? transportFactory = null,
            IDispatcher? dispatcher = null, TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
            if (effectiveTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            cancellationToken.ThrowIfCancellationRequested();

            var builder = new NetworkSdkBuilder().UseTransportFactory(transportFactory ?? (() => new TcpTransport()));
            if (dispatcher != null) builder.UseDispatchers(dispatcher);
            var sdkClient = builder.Build();
            RoomGatewayWireSessionClient? roomClient = null;
            try
            {
                sdkClient.Open(host, port);
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    deadline.CancelAfter(effectiveTimeout);
                    try
                    {
                        while (!sdkClient.IsConnected)
                        {
                            deadline.Token.ThrowIfCancellationRequested();
                            await Task.Delay(25, deadline.Token).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Gateway connection timed out: {host}:{port}");
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                roomClient = sdkClient.CreateRoomClient();
                return new RoomGatewayConnectionSession(sdkClient, roomClient);
            }
            catch
            {
                roomClient?.Dispose();
                sdkClient.Dispose();
                throw;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RoomGatewayConnectionSession));
            _sdkClient.Tick(deltaTime);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _recovery.Dispose(); }
            finally
            {
                try { _roomClient.Dispose(); }
                finally { _sdkClient.Dispose(); }
            }
        }
    }
}
