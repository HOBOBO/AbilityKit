using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Network.Battle
{
    /// <summary>
    /// 通用网络客户端，由 NetworkSdkClient 统一管理连接与请求生命周期。
    /// 可替换为 TcpNetworkClient 或其他实现。
    /// </summary>
    public sealed class GenericNetworkClient : INetworkClient
    {
        private readonly NetworkSdkClient _sdkClient;
        private bool _disposed;

        public bool IsConnected => _sdkClient.IsConnected;

        public event Action OnConnected
        {
            add => _sdkClient.Connected += value;
            remove => _sdkClient.Connected -= value;
        }

        public event Action<string> OnDisconnected;

        public event Action<Exception> OnError
        {
            add => _sdkClient.Error += value;
            remove => _sdkClient.Error -= value;
        }

        private event Action<uint, byte[]> _onServerPush;

        public event Action<uint, byte[]> OnServerPush
        {
            add
            {
                _onServerPush += value;
                _sdkClient.ServerPushReceived += HandleServerPush;
            }
            remove
            {
                _onServerPush -= value;
                if (_onServerPush == null)
                {
                    _sdkClient.ServerPushReceived -= HandleServerPush;
                }
            }
        }

        private void HandleServerPush(uint opCode, ArraySegment<byte> payload)
        {
            byte[] bytes;
            if (payload.Array != null && payload.Count > 0)
            {
                bytes = new byte[payload.Count];
                Buffer.BlockCopy(payload.Array, payload.Offset, bytes, 0, payload.Count);
            }
            else
            {
                bytes = Array.Empty<byte>();
            }
            _onServerPush?.Invoke(opCode, bytes);
        }

        public GenericNetworkClient(Func<ITransport> transportFactory, IFrameCodec frameCodec, IDispatcher dispatcher = null)
        {
            var effectiveDispatcher = dispatcher ?? InlineDispatcher.Instance;
            _sdkClient = new NetworkSdkBuilder()
                .UseTransportFactory(transportFactory)
                .ConfigureConnection(options => options.FrameCodec = frameCodec)
                .UseDispatchers(effectiveDispatcher, effectiveDispatcher)
                .Build();
        }

        public void Connect(string host, int port)
        {
            ThrowIfDisposed();
            _sdkClient.Open(host, port);
        }

        public void Disconnect()
        {
            ThrowIfDisposed();
            _sdkClient.Close();
        }

        public async Task<byte[]> SendRequestAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var segment = payload != null ? new ArraySegment<byte>(payload) : default;
            var response = await _sdkClient.SendRawRequestAsync(opCode, segment, cancellationToken: cancellationToken);

            if (response.Array == null || response.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[response.Count];
            Buffer.BlockCopy(response.Array, response.Offset, result, 0, response.Count);
            return result;
        }

        public Task SendServerPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var segment = payload != null ? new ArraySegment<byte>(payload) : default;
            _sdkClient.SendPacket(opCode, segment, flags: (ushort)NetworkPacketFlags.None);
            return Task.CompletedTask;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GenericNetworkClient));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _sdkClient.Dispose();
        }
    }
}
