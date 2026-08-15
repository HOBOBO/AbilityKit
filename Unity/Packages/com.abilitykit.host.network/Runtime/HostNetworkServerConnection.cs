using System;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Network.Host;

namespace AbilityKit.Ability.Host.Network
{
    public sealed class HostNetworkServerConnection : IServerConnection
    {
        private readonly object _identityGate = new object();
        private readonly IServerNetworkSession _session;
        private readonly IHostMessageCodec _codec;
        private ServerClientId _clientId;

        public HostNetworkServerConnection(
            ServerClientId clientId,
            IServerNetworkSession session,
            IHostMessageCodec codec)
        {
            _clientId = clientId;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public ServerClientId ClientId { get { lock (_identityGate) return _clientId; } }
        public IServerNetworkSession Session => _session;

        public void Send(ServerMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (!_codec.TryEncode(message, out var header, out var payload))
            {
                throw new InvalidOperationException(
                    $"No host message encoding is available for '{message.GetType().FullName}'.");
            }
            _session.Send(header, payload);
        }

        internal void Rebind(ServerClientId clientId)
        {
            lock (_identityGate) _clientId = clientId;
        }
    }
}
