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
        private interface IFrameScheduler
        {
            FrameIndex GetNextFrame(FrameIndex current);
        }

        private interface IInputModule
        {
            List<(WorldId worldId, PlayerInputCommand[] inputs)> FlushPendingAndDispatchInputs(FrameIndex nextFrame);
        }

        private interface ISnapshotModule
        {
            WorldStateSnapshot? TryGetSnapshot(WorldId worldId, FrameIndex frame);
        }

        private sealed class DefaultFrameScheduler : IFrameScheduler
        {
            public FrameIndex GetNextFrame(FrameIndex current) => new FrameIndex(current.Value + 1);
        }

        private sealed class DefaultInputModule : IInputModule
        {
            private readonly IWorldManager _worlds;
            private readonly Dictionary<WorldId, WorldSession> _sessions;
            private readonly LogicWorldServerOptions _options;

            public DefaultInputModule(IWorldManager worlds, Dictionary<WorldId, WorldSession> sessions, LogicWorldServerOptions options)
            {
                _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
                _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
                _options = options;
            }

            public List<(WorldId worldId, PlayerInputCommand[] inputs)> FlushPendingAndDispatchInputs(FrameIndex nextFrame)
            {
                var perWorldInputs = new List<(WorldId worldId, PlayerInputCommand[] inputs)>(_sessions.Count);
                foreach (var kv in _sessions)
                {
                    var worldId = kv.Key;
                    var session = kv.Value;
                    if (session.PendingInputs.Count == 0) continue;
                    var inputs = session.PendingInputs.ToArray();
                    session.PendingInputs.Clear();
                    perWorldInputs.Add((worldId, inputs));

                    if (_options != null)
                    {
                        _options.InputsFlushed.Invoke(worldId, nextFrame, inputs);
                        _options.OnInputsFlushed?.Invoke(worldId, nextFrame, inputs);
                    }

                    if (_worlds.TryGet(worldId, out var world) && world.Services != null)
                    {
                        if (world.Services.TryGet<IWorldInputSink>(out var sink) && sink != null)
                        {
                            sink.Submit(nextFrame, inputs);
                        }
                    }
                }

                return perWorldInputs;
            }
        }

        private sealed class DefaultSnapshotModule : ISnapshotModule
        {
            private readonly IWorldManager _worlds;

            public DefaultSnapshotModule(IWorldManager worlds)
            {
                _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
            }

            public WorldStateSnapshot? TryGetSnapshot(WorldId worldId, FrameIndex frame)
            {
                if (_worlds.TryGet(worldId, out var world) && world.Services != null)
                {
                    if (world.Services.TryGet<IWorldStateSnapshotProvider>(out var provider) && provider != null)
                    {
                        if (provider.TryGetSnapshot(frame, out var snapshot))
                        {
                            return snapshot;
                        }
                    }
                }

                return null;
            }
        }

        private sealed class FramePipeline
        {
            private readonly IWorldManager _worlds;
            private readonly WorldManagerFrameDriver _driver;
            private readonly Dictionary<WorldId, WorldSession> _sessions;
            private readonly Dictionary<ServerClientId, ILogicServerClient> _clients;
            private readonly LogicWorldServerOptions _options;

            private readonly IFrameScheduler _scheduler;
            private readonly IInputModule _input;
            private readonly ISnapshotModule _snapshot;

            private FrameIndex _frame;

            public FramePipeline(
                IWorldManager worlds,
                WorldManagerFrameDriver driver,
                Dictionary<WorldId, WorldSession> sessions,
                Dictionary<ServerClientId, ILogicServerClient> clients,
                IFrameScheduler scheduler,
                IInputModule input,
                ISnapshotModule snapshot,
                LogicWorldServerOptions options)
            {
                _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
                _driver = driver ?? throw new ArgumentNullException(nameof(driver));
                _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
                _clients = clients ?? throw new ArgumentNullException(nameof(clients));
                _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
                _input = input ?? throw new ArgumentNullException(nameof(input));
                _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
                _options = options;
                _frame = _driver.Frame;
            }

            public FrameIndex Frame => _frame;

            public void Tick(float deltaTime)
            {
                var nextFrame = _scheduler.GetNextFrame(_frame);
                var perWorldInputs = _input.FlushPendingAndDispatchInputs(nextFrame);

                _driver.Step(deltaTime);
                _frame = _driver.Frame;

                if (_options != null)
                {
                    _options.PostStep.Invoke(_frame, deltaTime);
                    _options.OnPostStep?.Invoke(_frame, deltaTime);
                }

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

                    var state = _snapshot.TryGetSnapshot(worldId, _frame);
                    var packet = new FramePacket(worldId, _frame, inputs, state);
                    if (_options != null)
                    {
                        _options.BeforeBroadcastFrame.Invoke(packet);
                        _options.OnBeforeBroadcastFrame?.Invoke(packet);
                    }
                    BroadcastFrame(packet);
                    if (_options != null)
                    {
                        _options.AfterBroadcastFrame.Invoke(packet);
                        _options.OnAfterBroadcastFrame?.Invoke(packet);
                    }
                }
            }

            private void BroadcastFrame(FramePacket packet)
            {
                foreach (var c in _clients.Values)
                {
                    c.OnFrame(packet);
                }
            }
        }

        private sealed class WorldSession
        {
            public readonly List<PlayerId> Players = new List<PlayerId>();
            public readonly List<PlayerInputCommand> PendingInputs = new List<PlayerInputCommand>();
        }

        private readonly IWorldManager _worlds;
        private readonly WorldManagerFrameDriver _driver;
        private readonly FramePipeline _pipeline;
        private readonly LogicWorldServerOptions _options;

        private readonly Dictionary<ServerClientId, ILogicServerClient> _clients = new Dictionary<ServerClientId, ILogicServerClient>();
        private readonly Dictionary<WorldId, WorldSession> _sessions = new Dictionary<WorldId, WorldSession>();

        private FrameIndex _frame;

        public LogicWorldServer(IWorldManager worlds)
            : this(worlds, null)
        {
        }

        public LogicWorldServer(IWorldManager worlds, LogicWorldServerOptions options)
        {
            _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
            _driver = new WorldManagerFrameDriver(_worlds);
            _options = options;

            var scheduler = new DefaultFrameScheduler();
            var input = new DefaultInputModule(_worlds, _sessions, options);
            var snapshot = new DefaultSnapshotModule(_worlds);
            _pipeline = new FramePipeline(_worlds, _driver, _sessions, _clients, scheduler, input, snapshot, options);
            _frame = _pipeline.Frame;
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
            if (_options != null)
            {
                _options.BeforeCreateWorld.Invoke(options);
                _options.OnBeforeCreateWorld?.Invoke(options);
            }

            var world = _worlds.Create(options);
            _sessions[world.Id] = new WorldSession();

            if (_options != null)
            {
                _options.WorldCreated.Invoke(world);
                _options.OnWorldCreated?.Invoke(world);
            }

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

            if (_options != null)
            {
                _options.WorldDestroyed.Invoke(id);
                _options.OnWorldDestroyed?.Invoke(id);
            }

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
            _pipeline.Tick(deltaTime);
            _frame = _pipeline.Frame;
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
