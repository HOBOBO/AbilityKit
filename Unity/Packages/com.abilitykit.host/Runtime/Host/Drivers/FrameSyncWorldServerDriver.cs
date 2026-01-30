using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Legacy.Transport;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host.Drivers
{
    public sealed class FrameSyncWorldServerDriver : IWorldServerDriver
    {
        public interface IFrameScheduler
        {
            FrameIndex GetNextFrame(FrameIndex current);
        }

        public interface IInputModule
        {
            List<(WorldId worldId, PlayerInputCommand[] inputs)> FlushPendingAndDispatchInputs(FrameIndex nextFrame);
        }

        public interface ISnapshotModule
        {
            WorldStateSnapshot? TryGetSnapshot(WorldId worldId, FrameIndex frame);
        }

        public sealed class WorldSession
        {
            public readonly List<PlayerId> Players = new List<PlayerId>();
            public readonly List<PlayerInputCommand> PendingInputs = new List<PlayerInputCommand>();
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
                        if (world.Services.TryResolve<IWorldInputSink>(out var sink) && sink != null)
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
                    if (world.Services.TryResolve<IWorldStateSnapshotProvider>(out var provider) && provider != null)
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

        private readonly IWorldManager _worlds;
        private readonly Dictionary<WorldId, WorldSession> _sessions;
        private readonly Dictionary<ServerClientId, IServerConnection> _clients;
        private readonly LogicWorldServerOptions _options;

        private readonly IFrameScheduler _scheduler;
        private readonly IInputModule _input;
        private readonly ISnapshotModule _snapshot;

        private FrameIndex _frame;

        public FrameSyncWorldServerDriver(
            IWorldManager worlds,
            Dictionary<WorldId, WorldSession> sessions,
            Dictionary<ServerClientId, IServerConnection> clients,
            LogicWorldServerOptions options)
        {
            _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _clients = clients ?? throw new ArgumentNullException(nameof(clients));
            _options = options;

            _scheduler = options?.CreateFrameScheduler?.Invoke() ?? new DefaultFrameScheduler();
            _input = options?.CreateInputModule?.Invoke(_worlds, _sessions, options) ?? new DefaultInputModule(_worlds, _sessions, options);
            _snapshot = options?.CreateSnapshotModule?.Invoke(_worlds) ?? new DefaultSnapshotModule(_worlds);

            _frame = new FrameIndex(0);
        }

        public FrameIndex Frame => _frame;

        public void Tick(float deltaTime)
        {
            var nextFrame = _scheduler.GetNextFrame(_frame);
            var perWorldInputs = _input.FlushPendingAndDispatchInputs(nextFrame);

            foreach (var kv in _sessions)
            {
                var worldId = kv.Key;
                if (_worlds.TryGet(worldId, out var world))
                {
                    world.Tick(deltaTime);
                }
            }

            _frame = nextFrame;

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
                c.Send(new LegacyFrameMessage(packet));
            }
        }
    }
}
