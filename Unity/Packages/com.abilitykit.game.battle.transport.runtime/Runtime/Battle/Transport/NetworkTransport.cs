using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Battle.Transport
{
    public sealed class NetworkTransport : IBattleLogicTransport, IDisposable
    {
        private readonly NetworkTransportOptions _options;
        private readonly ConnectionManager _connection;
        private readonly RequestClient _request;

        public NetworkTransport(NetworkTransportOptions options, IDispatcher dispatcher = null)
            : this(options, dispatcher, dispatcher)
        {
        }

        public NetworkTransport(NetworkTransportOptions options, IDispatcher callbackDispatcher, IDispatcher ioDispatcher)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.TransportFactory == null) throw new ArgumentException("TransportFactory is required.", nameof(options));
            if (_options.Port <= 0) throw new ArgumentException("Port must be set.", nameof(options));

            var connOptions = new ConnectionOptions
            {
                FrameCodec = _options.FrameCodec
            };

            _connection = new ConnectionManager(_options.TransportFactory, connOptions, callbackDispatcher, ioDispatcher);
            _connection.PacketReceived += OnPacketReceived;
            _connection.ServerPushReceived += OnServerPushReceived;

            _connection.Connected += OnConnected;
            _connection.Disconnected += OnDisconnected;
            _connection.Error += OnError;

            _request = new RequestClient(_connection);
        }

        public event Action<FramePacket> FramePushed;

        public void Connect()
        {
            Log.Info($"[NetworkTransport] Connect -> {_options.Host}:{_options.Port}");
            _connection.Open(_options.Host, _options.Port);
        }

        public void Disconnect()
        {
            _connection.Close();
        }

        public void SendCreateWorld(CreateWorldRequest request)
        {
            if (_options.SerializeCreateWorld == null) throw new InvalidOperationException("SerializeCreateWorld is not configured.");
            var payload = _options.SerializeCreateWorld.Invoke(request);
            _connection.Send(_options.OpCreateWorld, payload, flags: (ushort)NetworkPacketFlags.Request);
        }

        public void SendJoin(JoinWorldRequest request)
        {
            if (_options.SerializeJoin == null) throw new InvalidOperationException("SerializeJoin is not configured.");
            var payload = _options.SerializeJoin.Invoke(request);
            _connection.Send(_options.OpJoin, payload, flags: (ushort)NetworkPacketFlags.Request);
        }

        public void SendLeave(LeaveWorldRequest request)
        {
            if (_options.SerializeLeave == null) throw new InvalidOperationException("SerializeLeave is not configured.");
            var payload = _options.SerializeLeave.Invoke(request);
            _connection.Send(_options.OpLeave, payload, flags: (ushort)NetworkPacketFlags.Request);
        }

        public void SendInput(SubmitInputRequest request)
        {
            if (_options.SerializeSubmitInput == null) throw new InvalidOperationException("SerializeSubmitInput is not configured.");
            if (_options.DeserializeSubmitInputResponse == null)
            {
                var payload = _options.SerializeSubmitInput.Invoke(request);
                _connection.Send(_options.OpSubmitInput, payload, flags: (ushort)NetworkPacketFlags.Request);
                return;
            }

            _ = SendInputWithResponseAsync(request);
        }

        private async System.Threading.Tasks.Task SendInputWithResponseAsync(SubmitInputRequest request)
        {
            object current = request;
            try
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var payload = _options.SerializeSubmitInput.Invoke(current);
                    var responsePayload = await _request.SendRequestAsync(
                        _options.OpSubmitInput,
                        payload,
                        TimeSpan.FromSeconds(5));
                    var response = _options.DeserializeSubmitInputResponse.Invoke(responsePayload);
                    if (response.Accepted)
                    {
                        return;
                    }

                    if (!response.RetryAtAuthoritativeFrame ||
                        attempt > 0 ||
                        _options.RewriteSubmitInputFrame == null)
                    {
                        Log.Warning(
                            $"[NetworkTransport] Input rejected. serverFrame={response.ServerFrame} " +
                            $"reasonCode={response.ReasonCode}");
                        return;
                    }

                    var retryFrame = response.ServerFrame + Math.Max(1, _options.SubmitInputRetryFrameLead);
                    current = _options.RewriteSubmitInputFrame.Invoke(current, retryFrame);
                    Log.Warning(
                        $"[NetworkTransport] Retrying stale input. retryFrame={retryFrame} " +
                        $"serverFrame={response.ServerFrame}");
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[NetworkTransport] Input submission failed.");
            }
        }

        public void Dispose()
        {
            _connection.PacketReceived -= OnPacketReceived;
            _connection.ServerPushReceived -= OnServerPushReceived;

            _connection.Connected -= OnConnected;
            _connection.Disconnected -= OnDisconnected;
            _connection.Error -= OnError;

            _request.Dispose();
            _connection.Dispose();
        }

        private void OnConnected()
        {
            Log.Info($"[NetworkTransport] Connected: {_options.Host}:{_options.Port}");
            _ = AuthenticateConnectionAsync();
        }

        private async System.Threading.Tasks.Task AuthenticateConnectionAsync()
        {
            try
            {
                if (_options.OpRenewSession != 0 && !string.IsNullOrWhiteSpace(_options.SessionToken))
                {
                    if (_options.SerializeRenewSession == null)
                        throw new InvalidOperationException("SerializeRenewSession is not configured.");

                    var renewPayload = _options.SerializeRenewSession.Invoke(_options.SessionToken);
                    await _request.SendRequestAsync(_options.OpRenewSession, renewPayload);
                    Log.Info("[NetworkTransport] RenewSession ok (bound token/account to this connection).");
                }

                if (_options.OpPostAuthentication != 0)
                {
                    if (_options.SerializePostAuthentication == null)
                        throw new InvalidOperationException("SerializePostAuthentication is not configured.");

                    var subscribePayload = _options.SerializePostAuthentication.Invoke();
                    await _request.SendRequestAsync(_options.OpPostAuthentication, subscribePayload);
                    Log.Info("[NetworkTransport] Post-authentication request ok (authoritative frame subscription active).");
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[NetworkTransport] Connection authentication failed");
            }
        }

        private void OnDisconnected()
        {
            Log.Warning($"[NetworkTransport] Disconnected: {_options.Host}:{_options.Port}");
        }

        private void OnError(Exception ex)
        {
            Log.Exception(ex, $"[NetworkTransport] Error: {_options.Host}:{_options.Port}");
        }

        private void OnPacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            if (opCode != _options.OpFramePushed) return;
            if (_options.DeserializeFramePushed == null) return;

            var packet = _options.DeserializeFramePushed.Invoke(payload);
            FramePushed?.Invoke(packet);
        }

        private void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            if (opCode != _options.OpFramePushed) return;
            if (_options.DeserializeFramePushed == null) return;

            var packet = _options.DeserializeFramePushed.Invoke(payload);
            FramePushed?.Invoke(packet);
        }

    }
}
