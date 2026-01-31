using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.GameModes
{
    public sealed class GameModeHost
    {
        private readonly IWorldHost _host;
        private readonly Dictionary<WorldId, IGameModeSession> _sessions = new Dictionary<WorldId, IGameModeSession>();
        private readonly Dictionary<ServerClientId, WorldId> _clientToWorld = new Dictionary<ServerClientId, WorldId>();

        public GameModeHost(IWorldHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public IWorldHost Host => _host;

        public bool TryGetSession(WorldId worldId, out IGameModeSession session)
        {
            return _sessions.TryGetValue(worldId, out session);
        }

        public IGameModeSession CreateSession(IGameMode gameMode, WorldCreateOptions options)
        {
            if (gameMode == null) throw new ArgumentNullException(nameof(gameMode));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var session = gameMode.CreateSession(_host, options);
            if (session == null) throw new InvalidOperationException("GameMode.CreateSession returned null.");

            _sessions[session.WorldId] = session;
            return session;
        }

        public bool DestroySession(WorldId worldId)
        {
            if (_sessions.TryGetValue(worldId, out var session) && session != null)
            {
                session.Dispose();
            }

            _sessions.Remove(worldId);
            return _host.DestroyWorld(worldId);
        }

        public bool Join(IServerConnection connection, WorldId worldId, PlayerId playerId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (!_sessions.TryGetValue(worldId, out var session)) return false;

            if (session.AddClient(connection, playerId))
            {
                _clientToWorld[connection.ClientId] = worldId;
                return true;
            }

            return false;
        }

        public bool Leave(IServerConnection connection, WorldId worldId, PlayerId playerId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (!_sessions.TryGetValue(worldId, out var session)) return false;

            _clientToWorld.Remove(connection.ClientId);
            return session.RemoveClient(connection, playerId);
        }

        public bool SubmitInput(IServerConnection connection, PlayerInputCommand input)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            if (!_clientToWorld.TryGetValue(connection.ClientId, out var worldId)) return false;
            if (!_sessions.TryGetValue(worldId, out var session)) return false;

            return session.SubmitInput(connection, input);
        }

        public void Tick(float deltaTime)
        {
            foreach (var s in _sessions.Values)
            {
                s?.Tick(deltaTime);
            }

            _host.Tick(deltaTime);
        }
    }
}
