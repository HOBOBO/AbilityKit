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
    /// Adapts transport-neutral NetworkHost sessions to authoritative HostRuntime clients.
    /// Listener creation remains injected; TCP is only one optional factory.
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
        private bool _disposed;

        public HostNetworkConnectionManager(
            Func<IChannelListener> listenerFactory,
            IHostMessageCodec messageCodec,
            IHostNetworkRequestHandler requestHandler = null,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
            : this(listenerFactory, null, messageCodec, requestHandler, null, clientIdResolver, networkOptions)
        {
        }

        public HostNetworkConnectionManager(
            Func<string, int, IChannelListener> endpointListenerFactory,
            IHostMessageCodec messageCodec,
            IHostNetworkRequestHandler requestHandler = null,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
            : this(null, endpointListenerFactory, messageCodec, requestHandler, null, clientIdResolver, networkOptions)
        {
        }

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

        public bool IsListening => _networkHost?.IsListening == true;
        public string Endpoint => _networkHost?.Endpoint ?? string.Empty;

        public event Action<IServerConnection> OnClientConnected;
        public event Action<ServerClientId> OnClientDisconnected;
        public event Action<ServerClientId, ServerClientId> OnClientRebound;
        public event Action<Exception> Error;

        public void Attach(HostRuntime runtime)
        {
            ThrowIfDisposed();
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public void Detach()
        {
            Stop();
            _runtime = null;
        }

        /// <summary>Starts a listener whose endpoint is already owned by its implementation.</summary>
        public void Start()
        {
            if (_listenerFactory == null)
                throw new InvalidOperationException("This manager requires StartListen(address, port).");
            StartCore(_listenerFactory());
        }

        public void StartListen(string address, int port)
        {
            if (_endpointListenerFactory == null)
                throw new NotSupportedException(
                    "This listener does not use address/port endpoints. Call Start() instead.");
            StartCore(_endpointListenerFactory(address, port));
        }

        public void Stop()
        {
            var host = _networkHost;
            if (host == null) return;
            _networkHost = null;
            Unsubscribe(host);
            try
            {
                host.Dispose();
            }
            finally
            {
                _lastDiagnostics = host.GetDiagnostics();
                ClearConnections();
            }
        }

        public async Task StopAsync(
            TimeSpan drainTimeout,
            CancellationToken cancellationToken = default)
        {
            var host = _networkHost;
            if (host == null) return;
            _networkHost = null;
            Unsubscribe(host);
            try
            {
                await host.StopAsync(drainTimeout, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try { host.Dispose(); }
                finally
                {
                    _lastDiagnostics = host.GetDiagnostics();
                    ClearConnections();
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
            {
                _runtime?.Disconnect(connection.ClientId);
                OnClientDisconnected?.Invoke(connection.ClientId);
            }
        }

        public void Tick()
        {
            _networkHost?.Tick();
        }

        public NetworkHostDiagnostics GetDiagnostics()
        {
            return _networkHost?.GetDiagnostics() ?? _lastDiagnostics;
        }

        public IReadOnlyList<NetworkHostSessionSnapshot> GetSessionSnapshots()
        {
            return _networkHost?.GetSessionSnapshots() ?? Array.Empty<NetworkHostSessionSnapshot>();
        }

        /// <summary>Atomically replaces a temporary channel identity after authentication.</summary>
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

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                Detach();
            }
            finally
            {
                _disposed = true;
            }
        }

        private void StartCore(IChannelListener listener)
        {
            ThrowIfDisposed();
            if (_runtime == null) throw new InvalidOperationException("Attach a HostRuntime before starting the network host.");
            if (_networkHost != null) throw new InvalidOperationException("Network host is already running.");
            if (listener == null) throw new InvalidOperationException("Listener factory returned null.");

            var host = new global::AbilityKit.Network.Host.NetworkHost(
                listener,
                CreateEffectiveNetworkOptions());
            _lastDiagnostics = default;
            Subscribe(host);
            _networkHost = host;
            try
            {
                host.Start();
            }
            catch
            {
                _networkHost = null;
                Unsubscribe(host);
                host.Dispose();
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
    }
}
