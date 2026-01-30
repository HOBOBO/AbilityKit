using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public sealed class FrameSyncDriverModule : IHostRuntimeModule, IFrameSyncInputHub
    {
        private sealed class WorldSession
        {
            public readonly List<PlayerInputCommand> PendingInputs = new List<PlayerInputCommand>(16);
        }

        private readonly Dictionary<WorldId, WorldSession> _sessions = new Dictionary<WorldId, WorldSession>();

        private HostRuntime _runtime;
        private HostRuntimeOptions _options;

        private readonly Action<IWorld> _onWorldCreated;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPreTick;

        private FrameIndex _frame;

        public FrameSyncDriverModule()
        {
            _onWorldCreated = OnWorldCreated;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPreTick = OnPreTick;
        }

        public FrameIndex Frame => _frame;

        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _runtime = runtime;
            _options = options;

            _frame = new FrameIndex(0);

            options.WorldCreated.Add(_onWorldCreated);
            options.WorldDestroyed.Add(_onWorldDestroyed);
            options.PreTick.Add(_onPreTick);

            runtime.Features.RegisterFeature<IFrameSyncInputHub>(this);
        }

        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.WorldCreated.Remove(_onWorldCreated);
            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PreTick.Remove(_onPreTick);

            runtime.Features.UnregisterFeature<IFrameSyncInputHub>();

            _sessions.Clear();
            _runtime = null;
            _options = null;
        }

        public bool SubmitInput(ServerClientId clientId, WorldId worldId, PlayerInputCommand input)
        {
            if (_runtime == null) return false;
            if (!_sessions.TryGetValue(worldId, out var session)) return false;

            session.PendingInputs.Add(input);
            return true;
        }

        private void OnWorldCreated(IWorld world)
        {
            if (world == null) return;
            _sessions[world.Id] = new WorldSession();
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            _sessions.Remove(worldId);
        }

        private void OnPreTick(float deltaTime)
        {
            if (_runtime == null) return;

            var nextFrame = new FrameIndex(_frame.Value + 1);

            foreach (var kv in _sessions)
            {
                var worldId = kv.Key;
                var session = kv.Value;

                if (!_runtime.Worlds.TryGet(worldId, out var world) || world == null) continue;

                PlayerInputCommand[] inputs;
                if (session.PendingInputs.Count > 0)
                {
                    inputs = session.PendingInputs.ToArray();
                    session.PendingInputs.Clear();
                }
                else
                {
                    inputs = Array.Empty<PlayerInputCommand>();
                }

                if (world.Services != null && world.Services.TryResolve<IWorldInputSink>(out var sink) && sink != null)
                {
                    sink.Submit(nextFrame, inputs);
                }

                world.Tick(deltaTime);

                WorldStateSnapshot? state = null;
                if (world.Services != null && world.Services.TryResolve<IWorldStateSnapshotProvider>(out var provider) && provider != null)
                {
                    if (provider.TryGetSnapshot(nextFrame, out var snapshot))
                    {
                        state = snapshot;
                    }
                }

                var packet = new FramePacket(worldId, nextFrame, inputs, state);
                _runtime.Broadcast(new FrameMessage(packet));
            }

            _frame = nextFrame;
        }
    }
}
