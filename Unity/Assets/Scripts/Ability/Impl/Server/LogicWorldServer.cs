using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Server
{
    public sealed class LogicWorldServer : ILogicWorldServer
    {
        private sealed class WorldSession
        {
            public readonly List<PlayerId> Players = new List<PlayerId>();
            public readonly List<PlayerInputCommand> PendingInputs = new List<PlayerInputCommand>();
        }

        private readonly IWorldManager _worlds;
        private readonly WorldManagerFrameDriver _driver;

        private readonly Dictionary<ServerClientId, ILogicServerClient> _clients = new Dictionary<ServerClientId, ILogicServerClient>();
        private readonly Dictionary<WorldId, WorldSession> _sessions = new Dictionary<WorldId, WorldSession>();

        private FrameIndex _frame;

        public LogicWorldServer(IWorldManager worlds)
        {
            _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
            _driver = new WorldManagerFrameDriver(_worlds);
            _frame = _driver.Frame;
        }

        public IWorldManager Worlds => _worlds;

        public void Connect(ILogicServerClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            _clients[client.ClientId] = client;
        }

        public void Disconnect(ServerClientId clientId)
        {
            _clients.Remove(clientId);
        }

        public IWorld CreateWorld(WorldCreateOptions options)
        {
            var world = _worlds.Create(options);
            _sessions[world.Id] = new WorldSession();

            foreach (var c in _clients.Values)
            {
                c.OnWorldCreated(world.Id, world.WorldType);
            }

            return world;
        }

        public bool DestroyWorld(WorldId id)
        {
            if (!_worlds.Destroy(id)) return false;
            _sessions.Remove(id);

            foreach (var c in _clients.Values)
            {
                c.OnWorldDestroyed(id);
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

                if (_worlds.TryGet(worldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryGet<MobaLobbyStateService>(out var lobby) && lobby != null)
                    {
                        lobby.OnPlayerJoined(playerId);
                    }
                }

                foreach (var c in _clients.Values)
                {
                    c.OnPlayerJoined(worldId, playerId);
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

                if (_worlds.TryGet(worldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryGet<MobaLobbyStateService>(out var lobby) && lobby != null)
                    {
                        lobby.OnPlayerLeft(playerId);
                    }
                }

                foreach (var c in _clients.Values)
                {
                    c.OnPlayerLeft(worldId, playerId);
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

        public void Tick(float deltaTime)
        {
            var nextFrame = new FrameIndex(_frame.Value + 1);

            var perWorldInputs = new List<(WorldId worldId, PlayerInputCommand[] inputs)>(_sessions.Count);
            foreach (var kv in _sessions)
            {
                var worldId = kv.Key;
                var session = kv.Value;
                if (session.PendingInputs.Count == 0) continue;
                var inputs = session.PendingInputs.ToArray();
                session.PendingInputs.Clear();
                perWorldInputs.Add((worldId, inputs));

                if (_worlds.TryGet(worldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryGet<IWorldInputSink>(out var sink) && sink != null)
                    {
                        sink.Submit(nextFrame, inputs);
                    }
                }
            }

            _driver.Step(deltaTime);
            _frame = _driver.Frame;

            foreach (var kv in _sessions)
            {
                var worldId = kv.Key;
                PlayerInputCommand[] inputs = Array.Empty<PlayerInputCommand>();
                for (int i = 0; i < perWorldInputs.Count; i++)
                {
                    if (perWorldInputs[i].worldId.Value != worldId.Value) continue;
                    inputs = perWorldInputs[i].inputs;
                    break;
                }

                WorldStateSnapshot? state = null;
                if (_worlds.TryGet(worldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryGet<IWorldStateSnapshotProvider>(out var provider) && provider != null)
                    {
                        if (provider.TryGetSnapshot(_frame, out var snapshot))
                        {
                            state = snapshot;
                        }
                    }
                }

                BroadcastFrame(new FramePacket(worldId, _frame, inputs, state));
            }
        }

        private void BroadcastFrame(FramePacket packet)
        {
            foreach (var c in _clients.Values)
            {
                c.OnFrame(packet);
            }
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
