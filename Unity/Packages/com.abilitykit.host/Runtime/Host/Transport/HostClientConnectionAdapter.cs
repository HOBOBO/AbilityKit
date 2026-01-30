using System;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Host.Transport
{
    public sealed class HostClientConnectionAdapter : IServerConnection
    {
        private readonly IHostClient _client;

        public HostClientConnectionAdapter(IHostClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public ServerClientId ClientId => _client.ClientId;

        public void Send(ServerMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            _client.OnMessage(message);
        }
    }
}
