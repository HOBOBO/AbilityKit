using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Drivers;
using AbilityKit.Ability.Host.Legacy.Transport;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.Host.WorldCapabilities;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host
{
    public sealed class LogicWorldServer : ILogicWorldServer, IHostServer, IPlayerSessionHost, IFrameSyncSessionHost
    {
        private sealed class FrameSyncWorldSessionAdapter : IFrameSyncWorldSession
        {
            private readonly LogicWorldServer _server;
            public WorldId WorldId { get; }

            public FrameSyncWorldSessionAdapter(LogicWorldServer server, WorldId worldId)
            {
                _server = server ?? throw new ArgumentNullException(nameof(server));
                WorldId = worldId;
            }

            public bool Join(IServerConnection connection, PlayerId playerId)
            {
                if (connection == null) return false;

                if (_server._worlds.TryGet(WorldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryResolve<IWorldPlayerLifecycle>(out var lifecycle) && lifecycle != null)
                    {
                        lifecycle.OnPlayerJoined(playerId);
                    }
                }

                return true;
            }

            public bool Leave(IServerConnection connection, PlayerId playerId)
            {
                if (connection == null) return false;

                if (_server._worlds.TryGet(WorldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryResolve<IWorldPlayerLifecycle>(out var lifecycle) && lifecycle != null)
                    {
                        lifecycle.OnPlayerLeft(playerId);
                    }
                }

                return true;
            }

            public bool SubmitInput(IServerConnection connection, PlayerInputCommand input)
            {
                if (connection == null) return false;
                return _server.SubmitInput(connection.ClientId, WorldId, input);
            }
        }

        private readonly IWorldManager _worlds;
        private readonly LogicWorldServerOptions _options;

        private readonly Dictionary<ServerClientId, IServerConnection> _clients = new Dictionary<ServerClientId, IServerConnection>();
        private readonly Dictionary<WorldId, FrameSyncWorldServerDriver.WorldSession> _sessions = new Dictionary<WorldId, FrameSyncWorldServerDriver.WorldSession>();
        private readonly HashSet<WorldId> _realtimeWorlds = new HashSet<WorldId>();

        private readonly IWorldServerDriver _driver;
        private readonly IWorldServerDriverSelector _driverSelector;

        private FrameIndex _frame;

        public LogicWorldServer(IWorldManager worlds)
            : this(worlds, null)
        {
        }

        public LogicWorldServer(IWorldManager worlds, LogicWorldServerOptions options)
        {
            _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
            _options = options;

            _driver = new FrameSyncWorldServerDriver(_worlds, _sessions, _clients, options);
            _driverSelector = new DefaultWorldServerDriverSelector();
            _frame = _driver.Frame;
        }

        public IWorldManager Worlds => _worlds;

        public bool TryGetWorld(WorldId id, out IWorld world)
        {
            return _worlds.TryGet(id, out world);
        }

        public void Connect(ILogicServerClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            _clients[client.ClientId] = new LogicServerClientConnectionAdapter(client);
        }

        public void Connect(IHostClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            _clients[client.ClientId] = new HostClientConnectionAdapter(client);
        }

        public void Connect(IServerConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            _clients[connection.ClientId] = connection;
        }

        public void Disconnect(ServerClientId clientId)
        {
            _clients.Remove(clientId);
        }

        public IWorld CreateWorld(WorldCreateOptions options)
        {
            if (_options != null)
            {
                _options.BeforeCreateWorld.Invoke(options);
                _options.OnBeforeCreateWorld?.Invoke(options);
            }

            var world = _worlds.Create(options);

            if (_driverSelector.UseFrameSyncDriver(world))
            {
                _sessions[world.Id] = new FrameSyncWorldServerDriver.WorldSession();
            }
            else
            {
                _realtimeWorlds.Add(world.Id);
            }

            if (_options != null)
            {
                _options.WorldCreated.Invoke(world);
                _options.OnWorldCreated?.Invoke(world);
            }

            foreach (var c in _clients.Values)
            {
                c.Send(new WorldCreatedMessage(world.Id, world.WorldType));
            }

            return world;
        }

        public bool DestroyWorld(WorldId id)
        {
            if (!_worlds.Destroy(id)) return false;
            _sessions.Remove(id);
            _realtimeWorlds.Remove(id);

            if (_options != null)
            {
                _options.WorldDestroyed.Invoke(id);
                _options.OnWorldDestroyed?.Invoke(id);
            }

            foreach (var c in _clients.Values)
            {
                c.Send(new WorldDestroyedMessage(id));
            }

            return true;
        }

        public bool JoinWorld(ServerClientId clientId, WorldId worldId, PlayerId playerId)
        {
            if (!_clients.ContainsKey(clientId)) return false;
            if (!_sessions.TryGetValue(worldId, out var session)) return false;

            if (!Contains(session.Players, playerId))
            {
                session.Players.Add(playerId);

                if ((_options == null || _options.EnablePlayerLifecycleHooks) && _worlds.TryGet(worldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryResolve<IWorldPlayerLifecycle>(out var lifecycle) && lifecycle != null)
                    {
                        lifecycle.OnPlayerJoined(playerId);
                    }
                }

                if (_options != null)
                {
                    _options.PlayerJoined.Invoke(worldId, playerId);
                    _options.OnPlayerJoined?.Invoke(worldId, playerId);
                }

                if (_options == null || _options.BroadcastPlayerLifecycleMessages)
                {
                    foreach (var c in _clients.Values)
                    {
                        c.Send(new LegacyPlayerJoinedMessage(worldId, playerId));
                    }
                }
            }

            return true;
        }

        public bool LeaveWorld(ServerClientId clientId, WorldId worldId, PlayerId playerId)
        {
            if (!_clients.ContainsKey(clientId)) return false;
            if (!_sessions.TryGetValue(worldId, out var session)) return false;

            for (int i = 0; i < session.Players.Count; i++)
            {
                if (session.Players[i].Value != playerId.Value) continue;
                session.Players.RemoveAt(i);

                if ((_options == null || _options.EnablePlayerLifecycleHooks) && _worlds.TryGet(worldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryResolve<IWorldPlayerLifecycle>(out var lifecycle) && lifecycle != null)
                    {
                        lifecycle.OnPlayerLeft(playerId);
                    }
                }

                if (_options != null)
                {
                    _options.PlayerLeft.Invoke(worldId, playerId);
                    _options.OnPlayerLeft?.Invoke(worldId, playerId);
                }

                if (_options == null || _options.BroadcastPlayerLifecycleMessages)
                {
                    foreach (var c in _clients.Values)
                    {
                        c.Send(new LegacyPlayerLeftMessage(worldId, playerId));
                    }
                }

                return true;
            }

            return false;
        }

        public bool SubmitInput(ServerClientId clientId, WorldId worldId, PlayerInputCommand input)
        {
            if (!_clients.ContainsKey(clientId)) return false;
            if (!_sessions.TryGetValue(worldId, out var session)) return false;

            session.PendingInputs.Add(input);
            return true;
        }

        public bool TryGetFrameSyncWorldSession(WorldId worldId, out IFrameSyncWorldSession session)
        {
            if (_sessions.ContainsKey(worldId))
            {
                session = new FrameSyncWorldSessionAdapter(this, worldId);
                return true;
            }

            session = null;
            return false;
        }

        public void Tick(float deltaTime)
        {
            _driver.Tick(deltaTime);

            foreach (var id in _realtimeWorlds)
            {
                if (_worlds.TryGet(id, out var world))
                {
                    world.Tick(deltaTime);
                }
            }

            _frame = _driver.Frame;
        }

        private static bool Contains(List<PlayerId> list, PlayerId id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Value == id.Value) return true;
            }
            return false;
        }
    }
}
