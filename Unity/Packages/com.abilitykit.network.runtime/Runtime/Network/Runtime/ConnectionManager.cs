using System;
using System.Text;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Timing;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Network.Runtime
{
    public sealed class ConnectionManager : IConnection, IReconnectableConnection
    {
        private readonly Func<ITransport> _transportFactory;
        private readonly ConnectionOptions _options;
        private readonly IDispatcher _dispatcher;
        private readonly IDispatcher _ioDispatcher;

        private ITransport _transport;
        private INetworkRuntimeSession _session;
        private INetworkHeartbeatMiddleware _heartbeat;

        private string _host;
        private int _port;

        private float _timeSinceLastReceive;
        private float _timeSinceLastHeartbeatSend;

        private bool _openRequested;

        private readonly IReconnectAttemptScheduler _reconnectScheduler;

        public ConnectionManager(Func<ITransport> transportFactory, ConnectionOptions options = null, IDispatcher dispatcher = null)
        {
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            _options = options ?? new ConnectionOptions();
            _dispatcher = dispatcher ?? InlineDispatcher.Instance;
            _ioDispatcher = _dispatcher;
            _reconnectScheduler = CreateReconnectScheduler(_options);

            State = ConnectionState.Disconnected;
        }

        public ConnectionManager(Func<ITransport> transportFactory, ConnectionOptions options, IDispatcher callbackDispatcher, IDispatcher ioDispatcher)
        {
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            _options = options ?? new ConnectionOptions();
            _dispatcher = callbackDispatcher ?? InlineDispatcher.Instance;
            _ioDispatcher = ioDispatcher ?? InlineDispatcher.Instance;
            _reconnectScheduler = CreateReconnectScheduler(_options);

            State = ConnectionState.Disconnected;
        }

        public ConnectionState State { get; private set; }

        public bool IsReconnectExhausted { get; private set; }

        public bool IsConnected => _transport != null && _transport.IsConnected;

        /// <summary>
        /// 当前会话的中间件管线。会话未建立（未 Open 或已断开）时返回 null。
        /// 调用方可在 <see cref="Connected"/> 事件后通过 <see cref="NetworkPipeline.Add"/>
        /// 注入自定义中间件（例如 <see cref="Conditioning.NetworkConditioningMiddleware"/>）。
        /// </summary>
        public NetworkPipeline Pipeline => _session?.Pipeline;

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

        public void Close()
        {
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
                    _dispatcher.Post(() => ReconnectAttemptStarted?.Invoke(attemptNumber));
                    try
                    {
                        StartConnect(ConnectionState.Reconnecting);
                    }
                    catch (Exception ex)
                    {
                        _dispatcher.Post(() => Error?.Invoke(ex));
                        if (_reconnectScheduler.IsExhausted)
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

        public void Dispose()
        {
            _openRequested = false;
            StopInternal();
        }

        private void StartConnect(ConnectionState connectState)
        {
            StopInternal(keepState: true);

            State = connectState;
            try
            {
                _transport = _transportFactory.Invoke()
                    ?? throw new InvalidOperationException("Network transport factory returned null.");
                var sessionContext = new NetworkRuntimeSessionFactoryContext(
                    _transport,
                    _dispatcher,
                    _ioDispatcher,
                    _options.FrameCodec);
                _session = _options.SessionFactory != null
                    ? _options.SessionFactory.Invoke(sessionContext)
                    : new NetworkSession(_transport, _dispatcher, _ioDispatcher, _options.FrameCodec);
                if (_session == null)
                {
                    throw new InvalidOperationException("Network session factory returned null.");
                }

                if (_session.Pipeline == null)
                {
                    throw new InvalidOperationException("Network session factory returned a session without a pipeline.");
                }

                _session.Start();
                _session.PacketReceived += OnSessionPacketReceived;
                _session.ServerPushReceived += OnSessionServerPushReceived;
                _session.Connected += OnSessionConnected;
                _session.Disconnected += OnSessionDisconnected;
                _session.Error += OnSessionError;

                _heartbeat = _options.HeartbeatFactory != null
                    ? _options.HeartbeatFactory.Invoke(_options.HeartbeatOpCode)
                    : new HeartbeatMiddleware(_options.HeartbeatOpCode);
                if (_heartbeat == null)
                {
                    throw new InvalidOperationException("Heartbeat middleware factory returned null.");
                }

                _heartbeat.HeartbeatReceived += OnHeartbeatReceived;
                _session.Pipeline.Add(_heartbeat);
                PipelineCreated?.Invoke(_session.Pipeline);

                _transport.BytesReceived += OnTransportBytesReceived;
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

        private void StopInternal(bool keepState = false, bool resetReconnect = true)
        {
            if (_session != null)
            {
                _session.PacketReceived -= OnSessionPacketReceived;
                _session.ServerPushReceived -= OnSessionServerPushReceived;
                _session.Connected -= OnSessionConnected;
                _session.Disconnected -= OnSessionDisconnected;
                _session.Error -= OnSessionError;
            }

            if (_heartbeat != null)
            {
                _heartbeat.HeartbeatReceived -= OnHeartbeatReceived;
            }

            if (_transport != null)
            {
                _transport.BytesReceived -= OnTransportBytesReceived;
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

        private void OnTransportBytesReceived(ArraySegment<byte> bytes)
        {
            _timeSinceLastReceive = 0f;
        }

        private void OnHeartbeatReceived()
        {
            _timeSinceLastReceive = 0f;
        }

        private void OnSessionConnected()
        {
            State = ConnectionState.Connected;
            IsReconnectExhausted = false;
            _reconnectScheduler.Reset();
            _timeSinceLastReceive = 0f;
            _timeSinceLastHeartbeatSend = 0f;

            _dispatcher.Post(() => Connected?.Invoke());
        }

        private void OnSessionDisconnected()
        {
            _dispatcher.Post(() => Disconnected?.Invoke());

            if (_openRequested && _options.EnableReconnect)
            {
                ScheduleReconnect(null);
            }
            else
            {
                StopInternal();
            }
        }

        private void OnSessionError(Exception ex)
        {
            _dispatcher.Post(() => Error?.Invoke(ex));

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
                var token = string.Empty;
                var reason = string.Empty;

                try
                {
                    if (payload.Array != null && payload.Count > 0)
                    {
                        var json = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);
                        token = TryGetJsonStringValue(json, "sessionToken") ?? string.Empty;
                        reason = TryGetJsonStringValue(json, "reason") ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[ConnectionManager] Kick push json decode failed");
                    if (payload.Array != null && payload.Count > 0)
                    {
                        reason = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);
                    }
                }

                Kicked?.Invoke(token, reason);

                Close();
                return;
            }

            ServerPushReceived?.Invoke(opCode, payload);
        }

        private static string TryGetJsonStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;

            // Very small JSON extractor for {"key":"value"} style payloads.
            // It is NOT a general JSON parser; it is sufficient for our kick push payload.
            var pattern = "\"" + key + "\"";
            var i = json.IndexOf(pattern, StringComparison.Ordinal);
            if (i < 0) return null;

            i = json.IndexOf(':', i + pattern.Length);
            if (i < 0) return null;
            i++;

            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;

            var start = i;
            var sb = (StringBuilder)null;

            while (i < json.Length)
            {
                var c = json[i];
                if (c == '"')
                {
                    if (sb == null) return json.Substring(start, i - start);
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    if (i + 1 >= json.Length) return null;
                    sb ??= new StringBuilder(json.Substring(start, i - start));
                    var esc = json[i + 1];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default:
                            // Keep unknown escapes as-is
                            sb.Append(esc);
                            break;
                    }
                    i += 2;
                    start = i;
                    continue;
                }

                i++;
            }

            return null;
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

            State = ConnectionState.Reconnecting;
            if (!wasPending)
            {
                var attemptNumber = _reconnectScheduler.NextAttemptNumber;
                var delaySeconds = _reconnectScheduler.NextDelaySeconds;
                _dispatcher.Post(() => ReconnectScheduled?.Invoke(attemptNumber, delaySeconds));
            }

            try
            {
                _transport?.Close();
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
            var attempts = _reconnectScheduler.AttemptsStarted;
            StopInternal(resetReconnect: false);
            _dispatcher.Post(() => ReconnectExhausted?.Invoke(attempts));
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
