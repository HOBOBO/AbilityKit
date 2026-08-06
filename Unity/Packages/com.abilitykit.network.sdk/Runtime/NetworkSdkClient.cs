#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Network.Sdk
{
    /// <summary>
    /// Owns one connection and its single request client for the complete SDK lifetime.
    /// </summary>
    public sealed class NetworkSdkClient : IReconnectableConnection, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IReconnectableConnection? _reconnectableConnection;
        private readonly RequestClient _requestClient;
        private Action<uint, uint, ArraySegment<byte>>? _packetReceived;
        private bool _disposed;

        internal NetworkSdkClient(IConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _reconnectableConnection = connection as IReconnectableConnection;
            _requestClient = new RequestClient(_connection);

            _connection.Connected += HandleConnected;
            _connection.Disconnected += HandleDisconnected;
            _connection.Error += HandleError;
            _connection.ServerPushReceived += HandleServerPushReceived;
            _connection.Kicked += HandleKicked;

            if (_reconnectableConnection != null)
            {
                _reconnectableConnection.ReconnectScheduled += HandleReconnectScheduled;
                _reconnectableConnection.ReconnectAttemptStarted += HandleReconnectAttemptStarted;
                _reconnectableConnection.ReconnectExhausted += HandleReconnectExhausted;
            }
        }

        public ConnectionState State => _connection.State;

        public bool IsConnected => _connection.IsConnected;

        public bool SupportsReconnect => _reconnectableConnection != null;

        public bool IsReconnectExhausted =>
            _reconnectableConnection?.IsReconnectExhausted == true;

        public event Action? Connected;

        public event Action? Disconnected;

        public event Action<Exception>? Error;

        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived
        {
            add
            {
                ThrowIfDisposed();
                if (_packetReceived == null)
                {
                    _connection.PacketReceived += HandlePacketReceived;
                }

                _packetReceived += value;
            }
            remove
            {
                if (_disposed)
                {
                    return;
                }

                _packetReceived -= value;
                if (_packetReceived == null)
                {
                    _connection.PacketReceived -= HandlePacketReceived;
                }
            }
        }

        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

        public event Action<string, string>? Kicked;

        public event Action<int, float>? ReconnectScheduled;

        public event Action<int>? ReconnectAttemptStarted;

        public event Action<int>? ReconnectExhausted;

        public void Open(string host, int port)
        {
            ThrowIfDisposed();
            _connection.Open(host, port);
        }

        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            _connection.Close();
        }

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();
            _connection.Tick(deltaTime);
        }

        public void ResetReconnect()
        {
            ThrowIfDisposed();
            if (_reconnectableConnection == null)
            {
                throw new NotSupportedException(
                    "The configured network connection does not support reconnect control.");
            }

            _reconnectableConnection.ResetReconnect();
        }

        public void SendPacket(
            uint opCode,
            ArraySegment<byte> payload,
            ushort flags = 0,
            uint seq = 0)
        {
            ThrowIfDisposed();
            _connection.Send(opCode, payload, flags, seq);
        }

        public Task<ArraySegment<byte>> SendRawRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _requestClient.SendRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _connection.Connected -= HandleConnected;
            _connection.Disconnected -= HandleDisconnected;
            _connection.Error -= HandleError;
            if (_packetReceived != null)
            {
                _connection.PacketReceived -= HandlePacketReceived;
            }
            _connection.ServerPushReceived -= HandleServerPushReceived;
            _connection.Kicked -= HandleKicked;

            if (_reconnectableConnection != null)
            {
                _reconnectableConnection.ReconnectScheduled -= HandleReconnectScheduled;
                _reconnectableConnection.ReconnectAttemptStarted -= HandleReconnectAttemptStarted;
                _reconnectableConnection.ReconnectExhausted -= HandleReconnectExhausted;
            }

            _requestClient.Dispose();
            try
            {
                _connection.Close();
            }
            finally
            {
                _connection.Dispose();
            }

            Connected = null;
            Disconnected = null;
            Error = null;
            _packetReceived = null;
            ServerPushReceived = null;
            Kicked = null;
            ReconnectScheduled = null;
            ReconnectAttemptStarted = null;
            ReconnectExhausted = null;
        }

        private void HandleConnected()
        {
            Connected?.Invoke();
        }

        private void HandleDisconnected()
        {
            Disconnected?.Invoke();
        }

        private void HandleError(Exception exception)
        {
            Error?.Invoke(exception);
        }

        private void HandlePacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            _packetReceived?.Invoke(opCode, seq, payload);
        }

        private void HandleServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            ServerPushReceived?.Invoke(opCode, payload);
        }

        private void HandleKicked(string code, string reason)
        {
            Kicked?.Invoke(code, reason);
        }

        private void HandleReconnectScheduled(int attemptNumber, float delaySeconds)
        {
            ReconnectScheduled?.Invoke(attemptNumber, delaySeconds);
        }

        private void HandleReconnectAttemptStarted(int attemptNumber)
        {
            ReconnectAttemptStarted?.Invoke(attemptNumber);
        }

        private void HandleReconnectExhausted(int attempts)
        {
            ReconnectExhausted?.Invoke(attempts);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetworkSdkClient));
            }
        }
    }
}
