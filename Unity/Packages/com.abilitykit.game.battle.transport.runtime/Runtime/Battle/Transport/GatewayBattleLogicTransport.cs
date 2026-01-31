using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using System.Text;

namespace AbilityKit.Game.Battle.Transport
{
    public sealed class GatewayBattleLogicTransport : IBattleLogicTransport, IDisposable
    {
        private readonly GatewayBattleLogicTransportOptions _options;
        private readonly ConnectionManager _connection;
        private readonly RequestClient _request;

        public GatewayBattleLogicTransport(GatewayBattleLogicTransportOptions options, IDispatcher dispatcher = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.TransportFactory == null) throw new ArgumentException("TransportFactory is required.", nameof(options));
            if (_options.Port <= 0) throw new ArgumentException("Port must be set.", nameof(options));

            var connOptions = new ConnectionOptions
            {
                FrameCodec = _options.FrameCodec
            };

            _connection = new ConnectionManager(_options.TransportFactory, connOptions, dispatcher);
            _connection.PacketReceived += OnPacketReceived;

            _connection.Connected += OnConnected;
            _connection.Disconnected += OnDisconnected;
            _connection.Error += OnError;

            _request = new RequestClient(_connection);
        }

        public event Action<FramePacket> FramePushed;

        public void Connect()
        {
            Log.Info($"[GatewayBattleLogicTransport] Connect -> {_options.Host}:{_options.Port}");
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
            var payload = _options.SerializeSubmitInput.Invoke(request);
            _connection.Send(_options.OpSubmitInput, payload, flags: (ushort)NetworkPacketFlags.Request);
        }

        public void Dispose()
        {
            _connection.PacketReceived -= OnPacketReceived;

            _connection.Connected -= OnConnected;
            _connection.Disconnected -= OnDisconnected;
            _connection.Error -= OnError;

            _request.Dispose();
            _connection.Dispose();
        }

        private void OnConnected()
        {
            Log.Info($"[GatewayBattleLogicTransport] Connected: {_options.Host}:{_options.Port}");

            if (_options.OpRenewSession != 0 && !string.IsNullOrWhiteSpace(_options.SessionToken))
            {
                _ = TryRenewSessionAsync();
            }
        }

        private async System.Threading.Tasks.Task TryRenewSessionAsync()
        {
            try
            {
                var json = $"{{\"SessionToken\":\"{Escape(_options.SessionToken)}\",\"ExtendSeconds\":0,\"RotateToken\":false}}";
                var bytes = Encoding.UTF8.GetBytes(json);
                await _request.SendRequestAsync(_options.OpRenewSession, new ArraySegment<byte>(bytes));
                Log.Info("[GatewayBattleLogicTransport] RenewSession ok (bound token/account to this connection).");
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[GatewayBattleLogicTransport] RenewSession failed");
            }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void OnDisconnected()
        {
            Log.Warning($"[GatewayBattleLogicTransport] Disconnected: {_options.Host}:{_options.Port}");
        }

        private void OnError(Exception ex)
        {
            Log.Exception(ex, $"[GatewayBattleLogicTransport] Error: {_options.Host}:{_options.Port}");
        }

        private void OnPacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            if (opCode != _options.OpFramePushed) return;
            if (_options.DeserializeFramePushed == null) return;

            var packet = _options.DeserializeFramePushed.Invoke(payload);
            FramePushed?.Invoke(packet);
        }

    }
}
