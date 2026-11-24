using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Builder.Components;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>
    /// 把 NetworkHost 会话映射为权威 HostRuntime 的连接。
    /// 宿主负责串行调用生命周期入口；完成排空前不得重启、解绑或释放。
    /// </summary>
    public sealed class HostNetworkConnectionManager :
        IConnectionManager,
        IConnectionManagerLifecycle,
        IEndpointConnectionManager,
        IDisposable
    {
        private readonly object _gate = new object();
        private readonly Func<IChannelListener> _listenerFactory;
        private readonly Func<string, int, IChannelListener> _endpointListenerFactory;
        private readonly NetworkHostOptions _networkOptions;
        private readonly IHostMessageCodec _messageCodec;
        private readonly IHostNetworkRequestHandler _requestHandler;
        private readonly IAsyncHostNetworkRequestHandler _asyncRequestHandler;
        private readonly IHostClientIdResolver _clientIdResolver;
        private readonly Dictionary<string, HostNetworkServerConnection> _bySession =
            new Dictionary<string, HostNetworkServerConnection>();
        private readonly Dictionary<string, IServerConnection> _connections =
            new Dictionary<string, IServerConnection>();
        private HostRuntime _runtime;
        private global::AbilityKit.Network.Host.NetworkHost _networkHost;
        private NetworkHostDiagnostics _lastDiagnostics;
        private volatile bool _stopping;
        private bool _disposed;

        /// <summary>
        /// 创建连接桥；监听器工厂直到启动预检通过才会调用。
        /// </summary>
        public HostNetworkConnectionManager(
            Func<IChannelListener> listenerFactory,
            IHostMessageCodec messageCodec,
            IHostNetworkRequestHandler requestHandler = null,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
            : this(listenerFactory, null, messageCodec, requestHandler, null, clientIdResolver, networkOptions)
        {
        }

        /// <summary>
        /// 创建连接桥；监听器工厂直到启动预检通过才会调用。
        /// </summary>
        public HostNetworkConnectionManager(
            Func<string, int, IChannelListener> endpointListenerFactory,
            IHostMessageCodec messageCodec,
            IHostNetworkRequestHandler requestHandler = null,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
            : this(null, endpointListenerFactory, messageCodec, requestHandler, null, clientIdResolver, networkOptions)
        {
        }

        /// <summary>
        /// 创建使用异步请求处理器的连接桥；不会立即启动监听。
        /// </summary>
        public static HostNetworkConnectionManager CreateAsync(
            Func<IChannelListener> listenerFactory,
            IHostMessageCodec messageCodec,
            IAsyncHostNetworkRequestHandler requestHandler,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
        {
            return new HostNetworkConnectionManager(
                listenerFactory,
                null,
                messageCodec,
                null,
                requestHandler ?? throw new ArgumentNullException(nameof(requestHandler)),
                clientIdResolver,
                networkOptions);
        }

        /// <summary>
        /// 创建使用异步处理器和显式端点工厂的连接桥。
        /// </summary>
        public static HostNetworkConnectionManager CreateEndpointAsync(
            Func<string, int, IChannelListener> endpointListenerFactory,
            IHostMessageCodec messageCodec,
            IAsyncHostNetworkRequestHandler requestHandler,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
        {
            return new HostNetworkConnectionManager(
                null,
                endpointListenerFactory,
                messageCodec,
                null,
                requestHandler ?? throw new ArgumentNullException(nameof(requestHandler)),
                clientIdResolver,
                networkOptions);
        }

        private HostNetworkConnectionManager(
            Func<IChannelListener> listenerFactory,
            Func<string, int, IChannelListener> endpointListenerFactory,
            IHostMessageCodec messageCodec,
            IHostNetworkRequestHandler requestHandler,
            IAsyncHostNetworkRequestHandler asyncRequestHandler,
            IHostClientIdResolver clientIdResolver,
            NetworkHostOptions networkOptions)
        {
            _listenerFactory = listenerFactory;
            _endpointListenerFactory = endpointListenerFactory;
            if (_listenerFactory == null && _endpointListenerFactory == null)
                throw new ArgumentNullException(nameof(listenerFactory));
            _messageCodec = messageCodec ?? throw new ArgumentNullException(nameof(messageCodec));
            _requestHandler = requestHandler;
            _asyncRequestHandler = asyncRequestHandler;
            _clientIdResolver = clientIdResolver ?? ChannelHostClientIdResolver.Instance;
            _networkOptions = networkOptions ?? new NetworkHostOptions();
        }

        /// <summary>
        /// 获取当前 Host 连接的独立集合快照；其中的连接对象仍是活动引用。
        /// </summary>
        public IReadOnlyCollection<IServerConnection> Connections
        {
            get
            {
                lock (_gate)
                {
                    var snapshot = new IServerConnection[_connections.Count];
                    _connections.Values.CopyTo(snapshot, 0);
                    return snapshot;
                }
            }
        }

        /// <summary>
        /// 表示当前运行允许接入；排空期间为假。
        /// </summary>
        public bool IsListening => !_stopping && _networkHost?.IsListening == true;
        /// <summary>
        /// 获取当前监听端点；未运行或排空期间返回空字符串。
        /// </summary>
        public string Endpoint => _stopping ? string.Empty : _networkHost?.Endpoint ?? string.Empty;

        /// <summary>
        /// 在会话已登记到 Host 后通知。
        /// </summary>
        public event Action<IServerConnection> OnClientConnected;
        /// <summary>
        /// 在会话从 Host 移除后通知。
        /// </summary>
        public event Action<ServerClientId> OnClientDisconnected;
        /// <summary>
        /// 在绑定切换成功后通知旧标识和新标识。
        /// </summary>
        public event Action<ServerClientId, ServerClientId> OnClientRebound;
        /// <summary>
        /// 转发运行期间收到的监听器和会话错误。
        /// </summary>
        public event Action<Exception> Error;

        /// <summary>
        /// 绑定目标宿主；运行期间只能重复绑定同一实例。
        /// </summary>
        public void Attach(HostRuntime runtime)
        {
            ThrowIfDisposed();
            ThrowIfStopping();
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (_networkHost != null && !ReferenceEquals(_runtime, runtime))
                throw new InvalidOperationException("Stop the network host before changing the attached runtime.");
            _runtime = runtime;
        }

        /// <summary>
        /// 停止并解绑宿主；如正在排空，调用方须先等待原停止任务。
        /// </summary>
        public void Detach()
        {
            ThrowIfStopping();
            try { Stop(); }
            finally { _runtime = null; }
        }

        /// <summary>
        /// 启动由工厂配置端点的监听器；启动条件不满足时不调用工厂。
        /// </summary>
        public void Start()
        {
            if (_listenerFactory == null)
                throw new InvalidOperationException("This manager requires StartListen(address, port).");
            StartCore(_listenerFactory);
        }

        /// <summary>
        /// 通过端点工厂创建并启动监听；仅用于支持显式地址与端口的装配。
        /// </summary>
        public void StartListen(string address, int port)
        {
            if (_endpointListenerFactory == null)
                throw new NotSupportedException(
                    "This listener does not use address/port endpoints. Call Start() instead.");
            StartCore(() => _endpointListenerFactory(address, port));
        }

        /// <summary>
        /// 立即停止监听并关闭会话；已有排空过程时不重复停止。
        /// </summary>
        public void Stop()
        {
            var host = BeginStop();
            if (host == null) return;
            FinishStop(host);
        }

        /// <summary>
        /// 停止接入并在给定时限内排空已经排队的请求；调用方须等待当前任务完成后再切换生命周期。
        /// </summary>
        public async Task StopAsync(
            TimeSpan drainTimeout,
            CancellationToken cancellationToken = default)
        {
            if (drainTimeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(drainTimeout));
            var host = BeginStop();
            if (host == null) return;
            try
            {
                await host.StopAsync(drainTimeout, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                FinishStop(host);
            }
        }

        private global::AbilityKit.Network.Host.NetworkHost BeginStop()
        {
            var host = _networkHost;
            if (host == null || _stopping) return null;
            _stopping = true;
            Unsubscribe(host);
            return host;
        }

        private void FinishStop(global::AbilityKit.Network.Host.NetworkHost host)
        {
            try { host.Dispose(); }
            finally
            {
                try
                {
                    _lastDiagnostics = host.GetDiagnostics();
                    ClearConnections();
                }
                finally
                {
                    _networkHost = null;
                    _stopping = false;
                }
            }
        }

        private void ClearConnections()
        {
            HostNetworkServerConnection[] connections;
            lock (_gate)
            {
                connections = new HostNetworkServerConnection[_bySession.Count];
                _bySession.Values.CopyTo(connections, 0);
                _bySession.Clear();
                _connections.Clear();
            }
            foreach (var connection in connections)
                _runtime?.Disconnect(connection.ClientId);
            foreach (var connection in connections)
                OnClientDisconnected?.Invoke(connection.ClientId);
        }

        /// <summary>
        /// 推进运行期超时检查；排空期间不处理超时。
        /// </summary>
        public void Tick()
        {
            if (!_stopping) _networkHost?.Tick();
        }

        /// <summary>
        /// 获取当前或最近一次停止的网络诊断汇总。
        /// </summary>
        public NetworkHostDiagnostics GetDiagnostics()
        {
            return _networkHost?.GetDiagnostics() ?? _lastDiagnostics;
        }

        /// <summary>
        /// 获取当前网络会话明细，停止完成后为空。
        /// </summary>
        public IReadOnlyList<NetworkHostSessionSnapshot> GetSessionSnapshots()
        {
            return _networkHost?.GetSessionSnapshots() ?? Array.Empty<NetworkHostSessionSnapshot>();
        }

        /// <summary>
        /// 用已验证的身份替换临时连接标识；目标标识被占用时保持原绑定。
        /// </summary>
        public bool TryBindClient(string sessionId, ServerClientId authenticatedClientId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session id is required.", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(authenticatedClientId.Value))
                throw new ArgumentException("Authenticated client id is required.", nameof(authenticatedClientId));
            if (_runtime == null) return false;

            HostNetworkServerConnection connection;
            ServerClientId previousId;
            lock (_gate)
            {
                if (!_bySession.TryGetValue(sessionId, out connection)) return false;
                previousId = connection.ClientId;
                if (previousId.Value == authenticatedClientId.Value)
                {
                    connection.Session.Context.MarkEstablished();
                    return true;
                }
                if (_connections.ContainsKey(authenticatedClientId.Value)) return false;

                _connections.Remove(previousId.Value);
                connection.Rebind(authenticatedClientId);
                connection.Session.Context.MarkEstablished();
                _connections.Add(authenticatedClientId.Value, connection);
                _runtime.Disconnect(previousId);
                _runtime.Connect(connection);
            }

            OnClientRebound?.Invoke(previousId, authenticatedClientId);
            return true;
        }

        /// <summary>
        /// 停止并释放桥接资源；不能替代正在执行的异步排空。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            ThrowIfStopping();
            try
            {
                Detach();
            }
            finally
            {
                _disposed = true;
            }
        }

        private void StartCore(Func<IChannelListener> listenerFactory)
        {
            ThrowIfDisposed();
            ThrowIfStopping();
            if (_runtime == null) throw new InvalidOperationException("Attach a HostRuntime before starting the network host.");
            if (_networkHost != null) throw new InvalidOperationException("Network host is already running.");

            var options = CreateEffectiveNetworkOptions();
            var listener = listenerFactory() ?? throw new InvalidOperationException("Listener factory returned null.");
            global::AbilityKit.Network.Host.NetworkHost host;
            try { host = new global::AbilityKit.Network.Host.NetworkHost(listener, options); }
            catch
            {
                listener.Dispose();
                throw;
            }
            _lastDiagnostics = default;
            Subscribe(host);
            _networkHost = host;
            try
            {
                host.Start();
            }
            catch
            {
                var failedHost = BeginStop();
                if (failedHost != null) FinishStop(failedHost);
                throw;
            }
        }

        private void OnSessionOpened(IServerNetworkSession session)
        {
            var clientId = _clientIdResolver.Resolve(session);
            if (string.IsNullOrWhiteSpace(clientId.Value))
            {
                session.Channel.Close();
                return;
            }
            var connection = new HostNetworkServerConnection(clientId, session, _messageCodec);
            lock (_gate)
            {
                if (_connections.ContainsKey(clientId.Value))
                {
                    session.Channel.Close();
                    return;
                }
                _bySession.Add(session.Id, connection);
                _connections.Add(clientId.Value, connection);
            }
            _runtime.Connect(connection);
            OnClientConnected?.Invoke(connection);
        }

        private void OnSessionClosed(IServerNetworkSession session)
        {
            HostNetworkServerConnection connection;
            lock (_gate)
            {
                if (!_bySession.TryGetValue(session.Id, out connection)) return;
                _bySession.Remove(session.Id);
                _connections.Remove(connection.ClientId.Value);
            }
            _runtime?.Disconnect(connection.ClientId);
            OnClientDisconnected?.Invoke(connection.ClientId);
        }

        private Task HandleRequestAsync(
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken)
        {
            if (_runtime == null) return Task.CompletedTask;
            HostNetworkServerConnection connection;
            lock (_gate)
            {
                if (!_bySession.TryGetValue(session.Id, out connection)) return Task.CompletedTask;
            }
            if (_asyncRequestHandler != null)
            {
                return _asyncRequestHandler.HandleAsync(
                    _runtime,
                    connection.ClientId,
                    session,
                    header,
                    payload,
                    cancellationToken);
            }
            _requestHandler?.Handle(_runtime, connection.ClientId, session, header, payload);
            return Task.CompletedTask;
        }

        private void OnSessionError(IServerNetworkSession session, Exception exception)
        {
            Error?.Invoke(exception);
        }

        private void Subscribe(global::AbilityKit.Network.Host.NetworkHost host)
        {
            host.SessionOpened += OnSessionOpened;
            host.SessionClosed += OnSessionClosed;
            host.SessionError += OnSessionError;
            host.ListenerError += OnListenerError;
        }

        private void Unsubscribe(global::AbilityKit.Network.Host.NetworkHost host)
        {
            host.SessionOpened -= OnSessionOpened;
            host.SessionClosed -= OnSessionClosed;
            host.SessionError -= OnSessionError;
            host.ListenerError -= OnListenerError;
        }

        private void OnListenerError(Exception exception)
        {
            Error?.Invoke(exception);
        }

        private NetworkHostOptions CreateEffectiveNetworkOptions()
        {
            if (_networkOptions.RequestHandler != null || _networkOptions.AsyncRequestHandler != null)
                throw new InvalidOperationException(
                    "HostNetworkConnectionManager owns NetworkHost request routing. Configure a host request handler instead.");
            return new NetworkHostOptions
            {
                MaxConnections = _networkOptions.MaxConnections,
                MaxPendingRequestsPerSession = _networkOptions.MaxPendingRequestsPerSession,
                IdleTimeout = _networkOptions.IdleTimeout,
                EstablishmentTimeout = _networkOptions.EstablishmentTimeout,
                FrameCodec = _networkOptions.FrameCodec,
                CallbackDispatcher = _networkOptions.CallbackDispatcher,
                IoDispatcher = _networkOptions.IoDispatcher,
                Clock = _networkOptions.Clock,
                AdmissionPolicy = _networkOptions.AdmissionPolicy,
                AsyncRequestHandler = new HostRequestAdapter(this),
                ConfigurePipeline = _networkOptions.ConfigurePipeline
            };
        }

        private sealed class HostRequestAdapter : IAsyncServerRequestHandler
        {
            private readonly HostNetworkConnectionManager _owner;

            public HostRequestAdapter(HostNetworkConnectionManager owner)
            {
                _owner = owner;
            }

            public Task HandleAsync(
                IServerNetworkSession session,
                NetworkPacketHeader header,
                ArraySegment<byte> payload,
                CancellationToken cancellationToken)
            {
                return _owner.HandleRequestAsync(session, header, payload, cancellationToken);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HostNetworkConnectionManager));
        }

        private void ThrowIfStopping()
        {
            if (_stopping)
                throw new InvalidOperationException("Wait for the active StopAsync before changing the network host lifecycle.");
        }
    }
}
