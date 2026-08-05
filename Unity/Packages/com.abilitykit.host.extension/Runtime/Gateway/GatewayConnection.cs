using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Extensions.Gateway
{
    /// <summary>
    /// <see cref="IGatewayConnection"/> 的默认实现。
    /// 包装 <see cref="IConnection"/> + 请求/响应客户端 + 推送分发。
    /// </summary>
    public sealed class GatewayConnection : IGatewayConnection, IDisposable
    {
        private readonly IConnection _connection;
        private readonly Dictionary<uint, List<Action<byte[]>>> _pushHandlers = new();
        private readonly Dictionary<uint, TaskCompletionSource<byte[]>> _pendingRequests = new();
        private uint _nextSeq;
        private bool _disposed;

        public GatewayConnection(IConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _connection.ServerPushReceived += OnServerPush;
            _connection.PacketReceived += OnPacketReceived;
        }

        public IConnection RawConnection => _connection;
        public bool IsConnected => _connection.IsConnected;

        /// <summary>便捷创建方法。</summary>
        public static GatewayConnection Create(IConnection connection) => new(connection);

        public Task<byte[]> SendRequestAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var seq = ++_nextSeq;
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingRequests)
            {
                _pendingRequests[seq] = tcs;
            }

            cancellationToken.Register(() =>
            {
                lock (_pendingRequests)
                {
                    if (_pendingRequests.Remove(seq))
                        tcs.TrySetCanceled(cancellationToken);
                }
            });

            try
            {
                _connection.Send(opCode, new ArraySegment<byte>(payload ?? Array.Empty<byte>()),
                    flags: (ushort)NetworkPacketFlags.Request, seq: seq);
            }
            catch (Exception ex)
            {
                lock (_pendingRequests) { _pendingRequests.Remove(seq); }
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }

        public Task SendPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
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

        private void OnPacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            TaskCompletionSource<byte[]>? tcs;
            lock (_pendingRequests)
            {
                if (_pendingRequests.Remove(seq, out tcs) && tcs != null)
                {
                    tcs.TrySetResult(payload.ToArray());
                    return;
                }
            }

            Log.Warning($"[GatewayConnection] Unexpected response packet. opCode={opCode}, seq={seq}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _connection.ServerPushReceived -= OnServerPush;
            _connection.PacketReceived -= OnPacketReceived;

            lock (_pendingRequests)
            {
                foreach (var kv in _pendingRequests)
                    kv.Value.TrySetCanceled();
                _pendingRequests.Clear();
            }

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
