using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 借用一个连接实现请求、响应和推送；会话拥有自身订阅，不拥有连接。
    /// </summary>
    public sealed class NetworkProtocolSession : INetworkProtocolSession, IDisposable
    {
        private readonly IConnection _connection;
        private readonly RequestClient _requestClient;
        private readonly NetworkPacketRouter? _packetRouter;
        private readonly object _lifecycleGate = new();
        private readonly Dictionary<uint, List<Action<byte[]>>> _pushHandlers = new();
        private readonly Dictionary<uint, Dictionary<Action<byte[]>, NetworkPacketRouteHandler>> _routeHandlers = new();
        private Action<uint, ArraySegment<byte>>? _serverPushReceived;
        private bool _disposed;

        /// <summary>
        /// 在调用方持有的连接上建立会话；未提供编解码器时返回原始响应载荷。
        /// </summary>
        public NetworkProtocolSession(
            IConnection connection,
            IRequestResponseEnvelopeCodec? responseCodec = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _requestClient = new RequestClient(
                _connection,
                responseCodec);
            _packetRouter = (connection as IProtocolRoutedConnection)?.PacketRouter;
            _connection.ServerPushReceived += OnServerPush;
        }

        /// <inheritdoc />
        public IConnection RawConnection => _connection;

        /// <summary>
        /// 在连接的推送分发路径上通知订阅者。载荷仅在回调期间有效；释放后不能订阅，但允许解绑。
        /// </summary>
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived
        {
            add
            {
                lock (_lifecycleGate)
                {
                    ThrowIfDisposed();
                    _serverPushReceived += value;
                }
            }
            remove
            {
                lock (_lifecycleGate)
                    _serverPushReceived -= value;
            }
        }

        /// <inheritdoc />
        public bool IsConnected => !Volatile.Read(ref _disposed) && _connection.IsConnected;

        /// <summary>
        /// 在不接管连接所有权的前提下创建会话。
        /// </summary>
        public static NetworkProtocolSession Create(
            IConnection connection,
            IRequestResponseEnvelopeCodec? responseCodec = null) =>
            new(connection, responseCodec);

        /// <inheritdoc />
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

        /// <summary>
        /// 发送请求并等待响应；正数超时启用独立计时，返回会话拥有的响应副本。
        /// </summary>
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

        /// <inheritdoc />
        public Task SendPushAsync(uint opCode, byte[] payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            _connection.Send(opCode, new ArraySegment<byte>(payload ?? Array.Empty<byte>()),
                flags: (ushort)NetworkPacketFlags.ServerPush, seq: 0);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void RegisterPushHandler(uint opCode, Action<byte[]> handler)
        {
            lock (_lifecycleGate)
            {
                ThrowIfDisposed();
                if (handler == null) throw new ArgumentNullException(nameof(handler));

                if (_packetRouter != null)
                {
                    if (!_routeHandlers.TryGetValue(opCode, out var handlers))
                    {
                        handlers = new Dictionary<Action<byte[]>, NetworkPacketRouteHandler>();
                        _routeHandlers[opCode] = handlers;
                    }

                    if (handlers.ContainsKey(handler)) return;
                    NetworkPacketRouteHandler routeHandler = dispatch =>
                    {
                        if (Volatile.Read(ref _disposed)) return;
                        var bytes = dispatch.Payload.ToArray();
                        try { handler(bytes); }
                        catch (Exception ex)
                        {
                            Log.Exception(ex, $"[NetworkProtocolSession] Push handler error. opCode={opCode}");
                            throw;
                        }
                    };
                    handlers.Add(handler, routeHandler);
                    _packetRouter.Register(opCode, NetworkPacketDispatchKind.ServerPush, routeHandler);
                    return;
                }

                if (!_pushHandlers.TryGetValue(opCode, out var list))
                {
                    list = new List<Action<byte[]>>(2);
                    _pushHandlers[opCode] = list;
                }

                list.Add(handler);
            }
        }

        /// <inheritdoc />
        public void UnregisterPushHandler(uint opCode, Action<byte[]> handler)
        {
            lock (_lifecycleGate)
            {
                ThrowIfDisposed();
                if (handler == null) return;

                if (_packetRouter != null)
                {
                    if (_routeHandlers.TryGetValue(opCode, out var handlers) &&
                        handlers.Remove(handler, out var routeHandler))
                    {
                        _packetRouter.Unregister(opCode, NetworkPacketDispatchKind.ServerPush, routeHandler);
                        if (handlers.Count == 0) _routeHandlers.Remove(opCode);
                    }
                    return;
                }

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
            Delegate[]? subscribers;
            lock (_lifecycleGate)
            {
                if (_disposed) return;
                subscribers = _serverPushReceived?.GetInvocationList();
            }

            if (subscribers != null)
            {
                for (var i = 0; i < subscribers.Length; i++)
                {
                    if (Volatile.Read(ref _disposed)) return;
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
                            $"[NetworkProtocolSession] Server push subscriber error. opCode={opCode}");
                    }
                }
            }

            Action<byte[]>[] handlers;

            lock (_lifecycleGate)
            {
                if (_disposed || !_pushHandlers.TryGetValue(opCode, out var registered))
                    return;

                handlers = registered.ToArray();
            }

            var bytes = payload.ToArray();
            for (var i = 0; i < handlers.Length; i++)
            {
                if (Volatile.Read(ref _disposed)) return;
                try { handlers[i](bytes); }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[NetworkProtocolSession] Push handler error. opCode={opCode}");
                }
            }
        }

        /// <summary>
        /// 撤销本会话订阅和请求；可重复调用，不关闭连接，也不等待已经开始的回调退出。
        /// </summary>
        public void Dispose()
        {
            lock (_lifecycleGate)
            {
                if (_disposed) return;
                Volatile.Write(ref _disposed, true);

                if (_packetRouter != null)
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
                _serverPushReceived = null;
                _pushHandlers.Clear();
            }

            _requestClient.Dispose();
            _connection.ServerPushReceived -= OnServerPush;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed)) throw new ObjectDisposedException(nameof(NetworkProtocolSession));
        }
    }
}
