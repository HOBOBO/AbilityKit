using System;
using System.Threading;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Timing;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Catalog;

namespace AbilityKit.Network.Runtime
{
    public sealed class ConnectionManager :
        IConnection,
        IReconnectableConnection,
        INetworkConnectionDiagnosticsSource,
        IConnectionIdentity
    {
        private readonly Func<ITransport> _transportFactory;
        private readonly ConnectionOptions _options;
        private readonly IDispatcher _dispatcher;
        private readonly IDispatcher _ioDispatcher;

        private ITransport _transport;
        private INetworkRuntimeSession _session;
        private INetworkHeartbeatMiddleware _heartbeat;
        private SessionEvents _sessionEvents;

        private string _host;
        private int _port;

        private float _timeSinceLastReceive;
        private float _timeSinceLastHeartbeatSend;

        private bool _openRequested;
        private readonly string _connectionId;
        private int _connectionGeneration;
        private int _closeVersion;

        private readonly IReconnectAttemptScheduler _reconnectScheduler;

        public ConnectionManager(Func<ITransport> transportFactory, ConnectionOptions options = null, IDispatcher dispatcher = null)
        {
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            _options = options ?? new ConnectionOptions();
            _options.TrafficCapture?.Validate();
            _dispatcher = dispatcher ?? InlineDispatcher.Instance;
            _ioDispatcher = _dispatcher;
            _reconnectScheduler = CreateReconnectScheduler(_options);
            _connectionId = ResolveConnectionId(_options);

            State = ConnectionState.Disconnected;
        }

        public ConnectionManager(Func<ITransport> transportFactory, ConnectionOptions options, IDispatcher callbackDispatcher, IDispatcher ioDispatcher)
        {
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            _options = options ?? new ConnectionOptions();
            _options.TrafficCapture?.Validate();
            _dispatcher = callbackDispatcher ?? InlineDispatcher.Instance;
            _ioDispatcher = ioDispatcher ?? InlineDispatcher.Instance;
            _reconnectScheduler = CreateReconnectScheduler(_options);
            _connectionId = ResolveConnectionId(_options);

            State = ConnectionState.Disconnected;
        }

        public ConnectionState State { get; private set; }

        public bool IsReconnectExhausted { get; private set; }

        public bool IsConnected => _transport != null && _transport.IsConnected;

        public string ConnectionId => _connectionId;
        public int ConnectionGeneration => _connectionGeneration;

        /// <summary>
        /// 当前会话的中间件管线。会话未建立（未 Open 或已断开）时返回 null。
        /// 调用方可在 <see cref="Connected"/> 事件后通过 <see cref="NetworkPipeline.Add"/>
        /// 注入自定义中间件（例如 <see cref="Conditioning.NetworkConditioningMiddleware"/>）。
        /// </summary>
        public NetworkPipeline Pipeline => _session?.Pipeline;

        /// <summary>当前物理会话的统一协议路由器；会话未建立时返回 null。</summary>
        public NetworkPacketRouter PacketRouter => _session?.PacketRouter;

        /// <summary>读取当前连接、重连、心跳和协议路由的只读诊断快照。</summary>
        public NetworkConnectionDiagnosticsSnapshot GetDiagnosticsSnapshot()
        {
            var scheduler = _reconnectScheduler;
            var packetRouter = _session?.PacketRouter?.GetSnapshot();
            var ioDiagnostics = _session as INetworkRuntimeSessionDiagnostics;
            return new NetworkConnectionDiagnosticsSnapshot(
                _connectionId,
                _connectionGeneration,
                _host,
                _port,
                State,
                IsConnected,
                _openRequested,
                scheduler.IsPending,
                IsReconnectExhausted,
                scheduler.AttemptsStarted,
                scheduler.MaxAttempts,
                scheduler.NextAttemptNumber,
                scheduler.NextDelaySeconds,
                scheduler.RemainingDelaySeconds,
                _timeSinceLastReceive,
                _timeSinceLastHeartbeatSend,
                ioDiagnostics?.BytesReceivedCount ?? 0L,
                ioDiagnostics?.BytesSentCount ?? 0L,
                ioDiagnostics?.PacketsReceivedCount ?? 0L,
                ioDiagnostics?.PacketsSentCount ?? 0L,
                _session?.Pipeline?.Count ?? 0,
                packetRouter);
        }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;
        public event Action<int, float> ReconnectScheduled;
        public event Action<int> ReconnectAttemptStarted;
        public event Action<int> ReconnectExhausted;

        /// <summary>
        /// 新会话管线创建并安装内置中间件后触发。重连会创建新管线并再次触发。
        /// </summary>
        public event Action<NetworkPipeline> PipelineCreated;

        /// <summary>
        /// 连接处于可收发状态时，每帧以当前单调毫秒时钟触发。
        /// 时间相关中间件可订阅该事件以释放到期数据包。
        /// </summary>
        public event Action<long> MiddlewareTick;

        public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
        public event Action<uint, ArraySegment<byte>> ServerPushReceived;
        public event Action<string, string> Kicked;

        public void Open(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

            _host = host;
            _port = port;
            _openRequested = true;

            if (IsReconnectExhausted)
            {
                return;
            }

            if (State == ConnectionState.Disconnected)
            {
                StartConnect(ConnectionState.Connecting);
            }
        }

        /// <summary>
        /// 主动关闭连接并使待执行的连接通知失效，不合成断开事件；可随后重新打开。
        /// </summary>
        public void Close()
        {
            Interlocked.Increment(ref _closeVersion);
            _openRequested = false;
            StopInternal();
        }

        public void ResetReconnect()
        {
            IsReconnectExhausted = false;
            _reconnectScheduler.Reset();
            if (_openRequested && State == ConnectionState.Disconnected)
            {
                StartConnect(ConnectionState.Connecting);
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f) return;

            if (!_openRequested)
            {
                return;
            }

            if (State == ConnectionState.Reconnecting)
            {
                if (_reconnectScheduler.TryTakeAttempt(deltaTime, out var attemptNumber))
                {
                    var closeVersion = Volatile.Read(ref _closeVersion);
                    PostNotification(closeVersion, () => ReconnectAttemptStarted?.Invoke(attemptNumber));
                    if (closeVersion != Volatile.Read(ref _closeVersion)) return;
                    try
                    {
                        StartConnect(ConnectionState.Reconnecting);
                    }
                    catch (Exception ex)
                    {
                        PostNotification(closeVersion, () => Error?.Invoke(ex));
                        if (closeVersion == Volatile.Read(ref _closeVersion) && _reconnectScheduler.IsExhausted)
                        {
                            MarkReconnectExhausted();
                        }
                    }
                }
                return;
            }

            if (!IsConnected)
            {
                return;
            }

            _timeSinceLastReceive += deltaTime;
            _timeSinceLastHeartbeatSend += deltaTime;

            var hbInterval = (float)_options.HeartbeatInterval.TotalSeconds;
            if (hbInterval > 0f && _timeSinceLastHeartbeatSend >= hbInterval)
            {
                _timeSinceLastHeartbeatSend = 0f;
                SendHeartbeat();
            }

            var hbTimeout = (float)_options.HeartbeatTimeout.TotalSeconds;
            if (hbTimeout > 0f && _timeSinceLastReceive >= hbTimeout)
            {
                ScheduleReconnect(new TimeoutException("Heartbeat timeout."));
            }

            // 驱动时间相关中间件（如网络调理模拟器），让到期包在每帧冲刷。
            // 使用高精度单调时钟，避免 32 位回绕。
            MiddlewareTick?.Invoke(MonotonicTime.GetMilliseconds());
        }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            if (_session == null) throw new InvalidOperationException("Session not started.");
            _session.Send(opCode, payload, flags, seq);
        }

        /// <summary>
        /// 关闭并释放当前传输会话；关闭前排队的通知不再分发。
        /// </summary>
        public void Dispose() => Close();

        private void StartConnect(ConnectionState connectState)
        {
            StopInternal(keepState: true);

            _connectionGeneration = checked(_connectionGeneration + 1);
            State = connectState;
            try
            {
                _options.PacketBoundaryValidator?.BeginConnection(_connectionGeneration);
                _transport = _transportFactory.Invoke()
                    ?? throw new InvalidOperationException("Network transport factory returned null.");
                var frameCodec = _options.FrameCodec ?? LengthPrefixedFrameCodec.Instance;
                var sessionContext = new NetworkRuntimeSessionFactoryContext(
                    _transport,
                    _dispatcher,
                    _ioDispatcher,
                    frameCodec,
                    _options.PacketBoundaryValidator);
                _session = _options.SessionFactory != null
                    ? _options.SessionFactory.Invoke(sessionContext)
                    : new NetworkSession(
                        _transport,
                        _dispatcher,
                        _ioDispatcher,
                        frameCodec,
                        _options.PacketBoundaryValidator,
                        _options.MaxFrameLength,
                        _options.ValidatePacketHeaders);
                if (_session == null)
                {
                    throw new InvalidOperationException("Network session factory returned null.");
                }

                if (_session.Pipeline == null)
                {
                    throw new InvalidOperationException("Network session factory returned a session without a pipeline.");
                }

                _session.Start();
                var events = new SessionEvents(this, Volatile.Read(ref _closeVersion));
                Volatile.Write(ref _sessionEvents, events);
                _session.PacketReceived += events.OnPacketReceived;
                _session.ServerPushReceived += events.OnServerPushReceived;
                _session.Connected += events.OnConnected;
                _session.Disconnected += events.OnDisconnected;
                _session.Error += events.OnError;

                _heartbeat = _options.HeartbeatFactory != null
                    ? _options.HeartbeatFactory.Invoke(_options.HeartbeatOpCode)
                    : new HeartbeatMiddleware(_options.HeartbeatOpCode);
                if (_heartbeat == null)
                {
                    throw new InvalidOperationException("Heartbeat middleware factory returned null.");
                }

                _heartbeat.HeartbeatReceived += events.OnHeartbeatReceived;
                InstallTrafficProbe(_session.Pipeline, _transport);
                _session.Pipeline.Add(_heartbeat);
                PipelineCreated?.Invoke(_session.Pipeline);
                if (!events.IsCurrent) return;

                _transport.BytesReceived += events.OnTransportBytesReceived;
                _transport.Connect(_host, _port);
            }
            catch
            {
                if (_session == null && _transport != null)
                {
                    try
                    {
                        _transport.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[ConnectionManager] StartConnect: transport dispose failed");
                    }

                    _transport = null;
                }

                StopInternal(
                    keepState: connectState == ConnectionState.Reconnecting,
                    resetReconnect: false);
                throw;
            }
        }

        /// <summary>
        /// 将远端目录通告应用于已配置协议会话。
        /// </summary>
        public ProtocolCatalogNegotiationResult ApplyRemoteCatalog(
            ProtocolCatalogDefinition remoteCatalog)
        {
            if (_options.PacketBoundaryValidator == null)
                throw new InvalidOperationException("No protocol catalog boundary is configured.");
            return _options.PacketBoundaryValidator.ApplyRemoteCatalog(remoteCatalog);
        }

        private void StopInternal(bool keepState = false, bool resetReconnect = true)
        {
            var events = Interlocked.Exchange(ref _sessionEvents, null);
            if (events != null)
            {
                _session.PacketReceived -= events.OnPacketReceived;
                _session.ServerPushReceived -= events.OnServerPushReceived;
                _session.Connected -= events.OnConnected;
                _session.Disconnected -= events.OnDisconnected;
                _session.Error -= events.OnError;
                if (_heartbeat != null) _heartbeat.HeartbeatReceived -= events.OnHeartbeatReceived;
                if (_transport != null) _transport.BytesReceived -= events.OnTransportBytesReceived;
            }

            try
            {
                _session?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[ConnectionManager] StopInternal: session dispose failed");
            }

            _session = null;
            _heartbeat = null;
            _transport = null;

            if (!keepState)
            {
                State = ConnectionState.Disconnected;
            }

            _timeSinceLastReceive = 0f;
            _timeSinceLastHeartbeatSend = 0f;

            if (!keepState && resetReconnect)
            {
                _reconnectScheduler.Reset();
            }
        }

        private void PostNotification(int closeVersion, Action notify)
        {
            _dispatcher.Post(() =>
            {
                if (closeVersion == Volatile.Read(ref _closeVersion)) notify();
            });
        }

        private void OnSessionConnected(SessionEvents events)
        {
            State = ConnectionState.Connected;
            IsReconnectExhausted = false;
            _reconnectScheduler.Reset();
            _timeSinceLastReceive = 0f;
            _timeSinceLastHeartbeatSend = 0f;

            PostNotification(events.CloseVersion, () => Connected?.Invoke());
        }

        private void OnSessionDisconnected(SessionEvents events)
        {
            PostNotification(events.CloseVersion, () => Disconnected?.Invoke());
            if (!events.IsCurrent) return;

            if (_openRequested && _options.EnableReconnect)
            {
                ScheduleReconnect(null);
            }
            else
            {
                StopInternal();
            }
        }

        private void OnSessionError(SessionEvents events, Exception ex)
        {
            PostNotification(events.CloseVersion, () => Error?.Invoke(ex));
            if (!events.IsCurrent) return;

            if (_openRequested && _options.EnableReconnect)
            {
                ScheduleReconnect(ex);
            }
        }

        private void OnSessionPacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            PacketReceived?.Invoke(opCode, seq, payload);
        }

        private void OnSessionServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            if (_options.EnableKickHandling && opCode == _options.KickPushOpCode)
            {
                KickPushDecoder.Decode(payload, out var token, out var reason);

                Kicked?.Invoke(token, reason);

                Close();
                return;
            }

            ServerPushReceived?.Invoke(opCode, payload);
        }

        private void SendHeartbeat()
        {
            if (_session == null) return;

            _session.Send(_options.HeartbeatOpCode, default, flags: (ushort)NetworkPacketFlags.Heartbeat, seq: 0);
        }

        private void ScheduleReconnect(Exception ex)
        {
            if (!_options.EnableReconnect)
            {
                StopInternal();
                return;
            }

            var wasPending = _reconnectScheduler.IsPending;
            if (_options.ReconnectMaxAttempts == 0 ||
                _reconnectScheduler.IsExhausted ||
                !_reconnectScheduler.Request())
            {
                MarkReconnectExhausted();
                return;
            }

            var closeVersion = Volatile.Read(ref _closeVersion);
            var transport = _transport;
            State = ConnectionState.Reconnecting;
            if (!wasPending)
            {
                var attemptNumber = _reconnectScheduler.NextAttemptNumber;
                var delaySeconds = _reconnectScheduler.NextDelaySeconds;
                PostNotification(closeVersion, () => ReconnectScheduled?.Invoke(attemptNumber, delaySeconds));
            }

            try
            {
                if (closeVersion == Volatile.Read(ref _closeVersion) && ReferenceEquals(_transport, transport))
                    transport?.Close();
            }
            catch (Exception ex2)
            {
                Log.Exception(ex2, "[ConnectionManager] ScheduleReconnect: transport close failed");
            }
        }

        private void MarkReconnectExhausted()
        {
            if (IsReconnectExhausted) return;
            IsReconnectExhausted = true;
            var closeVersion = Volatile.Read(ref _closeVersion);
            var attempts = _reconnectScheduler.AttemptsStarted;
            StopInternal(resetReconnect: false);
            PostNotification(closeVersion, () => ReconnectExhausted?.Invoke(attempts));
        }

        private sealed class SessionEvents
        {
            private readonly ConnectionManager _owner;

            public SessionEvents(ConnectionManager owner, int closeVersion)
            {
                _owner = owner;
                CloseVersion = closeVersion;
            }

            public int CloseVersion { get; }
            public bool IsCurrent => CloseVersion == Volatile.Read(ref _owner._closeVersion) &&
                ReferenceEquals(this, Volatile.Read(ref _owner._sessionEvents));

            public void OnConnected()
            {
                if (IsCurrent) _owner.OnSessionConnected(this);
            }

            public void OnDisconnected()
            {
                if (IsCurrent) _owner.OnSessionDisconnected(this);
            }

            public void OnError(Exception error)
            {
                if (IsCurrent) _owner.OnSessionError(this, error);
            }

            public void OnPacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
            {
                if (IsCurrent) _owner.OnSessionPacketReceived(opCode, seq, payload);
            }

            public void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
            {
                if (IsCurrent) _owner.OnSessionServerPushReceived(opCode, payload);
            }

            public void OnHeartbeatReceived()
            {
                if (IsCurrent) _owner._timeSinceLastReceive = 0f;
            }

            public void OnTransportBytesReceived(ArraySegment<byte> bytes) => OnHeartbeatReceived();
        }

        private static IReconnectAttemptScheduler CreateReconnectScheduler(
            ConnectionOptions options)
        {
            var maxAttempts = options.ReconnectMaxAttempts > 0
                ? options.ReconnectMaxAttempts
                : int.MaxValue;
            Func<int, float> resolveDelay =
                attemptIndex => ResolveReconnectDelay(options, attemptIndex);
            if (options.ReconnectSchedulerFactory == null)
            {
                return new ReconnectAttemptScheduler(maxAttempts, resolveDelay);
            }

            var context = new ReconnectAttemptSchedulerFactoryContext(maxAttempts, resolveDelay);
            return options.ReconnectSchedulerFactory.Invoke(context)
                ?? throw new InvalidOperationException("Reconnect scheduler factory returned null.");
        }

        private void InstallTrafficProbe(NetworkPipeline pipeline, ITransport transport)
        {
            var capture = _options.TrafficCapture;
            if (capture == null) return;

            capture.Validate();
            var context = new NetworkTrafficConnectionContext(
                _connectionId,
                _connectionGeneration,
                capture.Role,
                capture.CatalogId,
                $"{_host}:{_port}",
                string.IsNullOrWhiteSpace(capture.TransportName)
                    ? transport.GetType().Name
                    : capture.TransportName);
            var observer = capture.ObserverFactory.Invoke(context)
                ?? throw new InvalidOperationException("Traffic observer factory returned null.");
            var filter = capture.FilterFactory?.Invoke(context) ?? capture.Filter;
            pipeline.AddFirst(new NetworkTrafficProbeMiddleware(
                context,
                observer,
                capture.MaximumPayloadPreviewBytes,
                filter,
                capture.UtcNowProvider,
                capture.ObserverErrorHandler));
        }

        private static string ResolveConnectionId(ConnectionOptions options)
        {
            var configured = options.TrafficCapture?.ConnectionId;
            return string.IsNullOrWhiteSpace(configured)
                ? Guid.NewGuid().ToString("N")
                : configured;
        }

        private static float ResolveReconnectDelay(
            ConnectionOptions options,
            int attemptIndex)
        {
            var initial = Math.Max(0d, options.ReconnectInitialDelay.TotalSeconds);
            var max = Math.Max(initial, options.ReconnectMaxDelay.TotalSeconds);
            var multiplier = Math.Max(0d, options.ReconnectBackoffMultiplier);
            var delay = initial * Math.Pow(multiplier, Math.Max(0, attemptIndex));
            return (float)Math.Min(max, delay);
        }

    }
}
