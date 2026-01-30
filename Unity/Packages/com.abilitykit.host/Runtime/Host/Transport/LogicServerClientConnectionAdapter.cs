using System;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Host.Transport
{
    public sealed class LogicServerClientConnectionAdapter : IServerConnection
    {
        private readonly ILogicServerClient _client;

        public LogicServerClientConnectionAdapter(ILogicServerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public ServerClientId ClientId => _client.ClientId;

        public void Send(ServerMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (message is WorldCreatedMessage worldCreated)
            {
                _client.OnWorldCreated(worldCreated.WorldId, worldCreated.WorldType);
                return;
            }

            if (message is WorldDestroyedMessage worldDestroyed)
            {
                _client.OnWorldDestroyed(worldDestroyed.WorldId);
                return;
            }

            if (message is PlayerJoinedMessage playerJoined)
            {
                _client.OnPlayerJoined(playerJoined.WorldId, playerJoined.PlayerId);
                return;
            }

            if (message is PlayerLeftMessage playerLeft)
            {
                _client.OnPlayerLeft(playerLeft.WorldId, playerLeft.PlayerId);
                return;
            }

            if (message is FrameMessage frame)
            {
                _client.OnFrame(frame.Packet);
                return;
            }

            throw new InvalidOperationException($"Unsupported message type: {message.GetType().FullName}");
        }
    }
}
