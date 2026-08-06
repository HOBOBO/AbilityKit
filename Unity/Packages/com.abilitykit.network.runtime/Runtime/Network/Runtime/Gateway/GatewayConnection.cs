using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Network.Runtime.Gateway
{
    /// <summary>
    /// <see cref="IGatewayConnection"/> 的默认实现。
    /// 包装 <see cref="IConnection"/> + 请求/响应客户端 + 推送分发。
    /// </summary>
    public sealed class GatewayConnection : IGatewayConnection, IDisposable
    {
        private readonly IConnection _connection;
        private readonly RequestClient _requestClient;
        private readonly Dictionary<uint, List<Action<byte[]>>> _pushHandlers = new();
        private bool _disposed;

        public GatewayConnection(IConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _requestClient = new RequestClient(_connection);
            _connection.ServerPushReceived += OnServerPush;
        }

        public IConnection RawConnection => _connection;
        public bool IsConnected => _connection.IsConnected;

        /// <summary>便捷创建方法。</summary>
        public static GatewayConnection Create(IConnection connection) => new(connection);

        public Task<byte[]> SendRequestAsync(
            uint opCode,
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            return SendRequestAsync(
                opCode,
                payload,
                timeout: null,
                cancellationToken: cancellationToken);
        }

        public async Task<byte[]> SendRequestAsync(
            uint opCode,
            byte[] payload,
            TimeSpan? timeout,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var response = await _requestClient.SendRequestAsync(
                opCode,
                new ArraySegment<byte>(payload ?? Array.Empty<byte>()),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return response.ToArray();
        }

        public Task SendPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            _connection.Send(opCode, new ArraySegment<byte>(payload ?? Array.Empty<byte>()),
                flags: (ushort)NetworkPacketFlags.ServerPush, seq: 0);
            return Task.CompletedTask;
        }

        public void RegisterPushHandler(uint opCode, Action<byte[]> handler)
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_pushHandlers)
            {
                if (!_pushHandlers.TryGetValue(opCode, out var list))
                {
                    list = new List<Action<byte[]>>(2);
                    _pushHandlers[opCode] = list;
                }

                list.Add(handler);
            }
        }

        public void UnregisterPushHandler(uint opCode, Action<byte[]> handler)
        {
            ThrowIfDisposed();
            if (handler == null) return;

            lock (_pushHandlers)
            {
                if (_pushHandlers.TryGetValue(opCode, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                        _pushHandlers.Remove(opCode);
                }
            }
        }

        private void OnServerPush(uint opCode, ArraySegment<byte> payload)
        {
            var bytes = payload.ToArray();
            List<Action<byte[]>>? handlers;

            lock (_pushHandlers)
            {
                if (!_pushHandlers.TryGetValue(opCode, out handlers) || handlers == null || handlers.Count == 0)
                    return;

                handlers = new List<Action<byte[]>>(handlers); // 快照副本，避免 handler 内修改集合
            }

            for (var i = 0; i < handlers.Count; i++)
            {
                try { handlers[i](bytes); }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[GatewayConnection] Push handler error. opCode={opCode}");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _connection.ServerPushReceived -= OnServerPush;
            _requestClient.Dispose();

            lock (_pushHandlers)
            {
                _pushHandlers.Clear();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GatewayConnection));
        }
    }
}
