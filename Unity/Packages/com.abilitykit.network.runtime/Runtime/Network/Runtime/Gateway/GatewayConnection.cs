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
        private readonly NetworkPacketRouter? _packetRouter;
        private readonly Dictionary<uint, List<Action<byte[]>>> _pushHandlers = new();
        private readonly Dictionary<uint, Dictionary<Action<byte[]>, NetworkPacketRouteHandler>> _routeHandlers = new();
        private bool _disposed;

        public GatewayConnection(IConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _requestClient = new RequestClient(_connection);
            _packetRouter = (connection as IProtocolRoutedConnection)?.PacketRouter;
            _connection.ServerPushReceived += OnServerPush;
        }

        public IConnection RawConnection => _connection;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;
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

            if (_packetRouter != null)
            {
                lock (_routeHandlers)
                {
                    if (!_routeHandlers.TryGetValue(opCode, out var handlers))
                    {
                        handlers = new Dictionary<Action<byte[]>, NetworkPacketRouteHandler>();
                        _routeHandlers[opCode] = handlers;
                    }

                    if (handlers.ContainsKey(handler)) return;
                    NetworkPacketRouteHandler routeHandler = dispatch =>
                    {
                        var bytes = dispatch.Payload.ToArray();
                        try { handler(bytes); }
                        catch (Exception ex)
                        {
                            Log.Exception(ex, $"[GatewayConnection] Push handler error. opCode={opCode}");
                            throw;
                        }
                    };
                    handlers.Add(handler, routeHandler);
                    _packetRouter.Register(opCode, NetworkPacketDispatchKind.ServerPush, routeHandler);
                }
                return;
            }

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

            if (_packetRouter != null)
            {
                lock (_routeHandlers)
                {
                    if (_routeHandlers.TryGetValue(opCode, out var handlers) &&
                        handlers.Remove(handler, out var routeHandler))
                    {
                        _packetRouter.Unregister(opCode, NetworkPacketDispatchKind.ServerPush, routeHandler);
                        if (handlers.Count == 0) _routeHandlers.Remove(opCode);
                    }
                }
                return;
            }

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
            var subscribers = ServerPushReceived?.GetInvocationList();
            if (subscribers != null)
            {
                for (var i = 0; i < subscribers.Length; i++)
                {
                    try
                    {
                        ((Action<uint, ArraySegment<byte>>)subscribers[i])(
                            opCode,
                            payload);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(
                            ex,
                            $"[GatewayConnection] Server push subscriber error. opCode={opCode}");
                    }
                }
            }

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

            if (_packetRouter != null)
            {
                lock (_routeHandlers)
                {
                    foreach (var pair in _routeHandlers)
                    {
                        foreach (var routeHandler in pair.Value.Values)
                        {
                            _packetRouter.Unregister(
                                pair.Key,
                                NetworkPacketDispatchKind.ServerPush,
                                routeHandler);
                        }
                    }
                    _routeHandlers.Clear();
                }
            }

            ServerPushReceived = null;
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
