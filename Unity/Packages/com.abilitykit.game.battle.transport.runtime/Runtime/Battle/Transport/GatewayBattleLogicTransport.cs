using System;
using AbilityKit.Ability.Server;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Battle.Transport
{
    public sealed class GatewayBattleLogicTransport : IBattleLogicTransport, IDisposable
    {
        private readonly GatewayBattleLogicTransportOptions _options;
        private readonly ConnectionManager _connection;

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
        }

        public event Action<FramePacket> FramePushed;

        public void Connect()
        {
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
            _connection.Dispose();
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
