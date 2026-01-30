using System;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.GameModes
{
    public sealed class BasicFrameSyncSession : IGameModeSession
    {
        private readonly IFrameSyncWorldSession _worldSession;
        private readonly IServerConnectionHost _connectionHost;

        public BasicFrameSyncSession(IFrameSyncWorldSession worldSession, IServerConnectionHost connectionHost)
        {
            _worldSession = worldSession ?? throw new ArgumentNullException(nameof(worldSession));
            _connectionHost = connectionHost ?? throw new ArgumentNullException(nameof(connectionHost));
        }

        public WorldId WorldId => _worldSession.WorldId;

        public bool AddClient(IServerConnection connection, PlayerId playerId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            _connectionHost.Connect(connection);
            return _worldSession.Join(connection, playerId);
        }

        public bool RemoveClient(IServerConnection connection, PlayerId playerId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            var ok = _worldSession.Leave(connection, playerId);
            _connectionHost.Disconnect(connection.ClientId);
            return ok;
        }

        public bool SubmitInput(IServerConnection connection, PlayerInputCommand input)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            return _worldSession.SubmitInput(connection, input);
        }

        public void Tick(float deltaTime)
        {
        }

        public void Dispose()
        {
        }
    }
}
