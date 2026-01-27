using System;
using System.Text;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    public sealed class ConnectionManager : IConnection
    {
        private readonly Func<ITransport> _transportFactory;
        private readonly ConnectionOptions _options;
        private readonly IDispatcher _dispatcher;

        private ITransport _transport;
        private NetworkSession _session;
        private HeartbeatMiddleware _heartbeat;

        private string _host;
        private int _port;

        private float _timeSinceLastReceive;
        private float _timeSinceLastHeartbeatSend;

        private bool _openRequested;

        private int _reconnectAttempts;
        private float _reconnectDelaySeconds;
        private float _timeToReconnect;

        public ConnectionManager(Func<ITransport> transportFactory, ConnectionOptions options = null, IDispatcher dispatcher = null)
        {
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            _options = options ?? new ConnectionOptions();
            _dispatcher = dispatcher ?? InlineDispatcher.Instance;

            State = ConnectionState.Disconnected;
        }

        public ConnectionState State { get; private set; }

        public bool IsConnected => _transport != null && _transport.IsConnected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;

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

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f) return;

            if (!_openRequested)
            {
                return;
            }

            if (State == ConnectionState.Reconnecting)
            {
                _timeToReconnect -= deltaTime;
                if (_timeToReconnect <= 0f)
                {
                    StartConnect(ConnectionState.Reconnecting);
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
            _reconnectDelaySeconds = 0f;

            _transport = _transportFactory.Invoke();
            _session = new NetworkSession(_transport, _dispatcher, _options.FrameCodec);

            _session.Start();
            _session.PacketReceived += OnSessionPacketReceived;
            _session.ServerPushReceived += OnSessionServerPushReceived;
            _session.Connected += OnSessionConnected;
            _session.Disconnected += OnSessionDisconnected;
            _session.Error += OnSessionError;

            _heartbeat = new HeartbeatMiddleware(_options.HeartbeatOpCode);
            _heartbeat.HeartbeatReceived += OnHeartbeatReceived;
            _session.Pipeline.Add(_heartbeat);

            _transport.BytesReceived += OnTransportBytesReceived;

            _transport.Connect(_host, _port);
        }

        private void StopInternal(bool keepState = false)
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
            catch
            {
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

            _reconnectAttempts = 0;
            _timeToReconnect = 0f;
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
            _reconnectAttempts = 0;
            _timeToReconnect = 0f;
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
                catch
                {
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

            if (_options.ReconnectMaxAttempts >= 0 && _reconnectAttempts >= _options.ReconnectMaxAttempts)
            {
                StopInternal();
                return;
            }

            _reconnectAttempts++;

            var initial = (float)_options.ReconnectInitialDelay.TotalSeconds;
            var max = (float)_options.ReconnectMaxDelay.TotalSeconds;
            if (_reconnectDelaySeconds <= 0f)
            {
                _reconnectDelaySeconds = initial;
            }
            else
            {
                _reconnectDelaySeconds = (float)Math.Min(max, _reconnectDelaySeconds * _options.ReconnectBackoffMultiplier);
            }

            State = ConnectionState.Reconnecting;
            _timeToReconnect = _reconnectDelaySeconds;

            try
            {
                _transport?.Close();
            }
            catch
            {
            }
        }

    }
}
