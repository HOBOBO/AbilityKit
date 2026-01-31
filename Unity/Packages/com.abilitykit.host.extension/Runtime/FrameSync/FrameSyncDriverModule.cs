using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public sealed class FrameSyncDriverModule : IHostRuntimeModule, IFrameSyncInputHub, IFrameSyncDriverEvents
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

        private readonly List<Action<WorldId, FrameIndex, PlayerInputCommand[]>> _inputsFlushed = new List<Action<WorldId, FrameIndex, PlayerInputCommand[]>>(8);
        private readonly List<Action<FrameIndex, float>> _postStep = new List<Action<FrameIndex, float>>(8);

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
            runtime.Features.RegisterFeature<IFrameSyncDriverEvents>(this);
        }

        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.WorldCreated.Remove(_onWorldCreated);
            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PreTick.Remove(_onPreTick);

            runtime.Features.UnregisterFeature<IFrameSyncInputHub>();
            runtime.Features.UnregisterFeature<IFrameSyncDriverEvents>();

            _sessions.Clear();
            _inputsFlushed.Clear();
            _postStep.Clear();
            _runtime = null;
            _options = null;
        }

        public void AddInputsFlushed(Action<WorldId, FrameIndex, PlayerInputCommand[]> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _inputsFlushed.Add(handler);
        }

        public void RemoveInputsFlushed(Action<WorldId, FrameIndex, PlayerInputCommand[]> handler)
        {
            if (handler == null) return;
            _inputsFlushed.Remove(handler);
        }

        public void AddPostStep(Action<FrameIndex, float> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _postStep.Add(handler);
        }

        public void RemovePostStep(Action<FrameIndex, float> handler)
        {
            if (handler == null) return;
            _postStep.Remove(handler);
        }

        public bool SubmitInput(ServerClientId clientId, WorldId worldId, PlayerInputCommand input)
        {
            if (_runtime == null) return false;
            if (!_sessions.TryGetValue(worldId, out var session))
            {
                Log.Error($"[FrameSyncDriverModule] SubmitInput rejected: session not found. worldId={worldId}, clientId={clientId.Value}, opCode={input.OpCode}");
                return false;
            }

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

                if (_inputsFlushed.Count > 0)
                {
                    for (int i = 0; i < _inputsFlushed.Count; i++)
                    {
                        _inputsFlushed[i]?.Invoke(worldId, nextFrame, inputs);
                    }
                }

                if (world.Services == null)
                {
                    Log.Error($"[FrameSyncDriverModule] world.Services is null; skipping sink.Submit. worldId={worldId}");
                }
                else if (world.Services.TryResolve<AbilityKit.Ability.Host.IWorldInputSink>(out var sink) && sink != null)
                {
                    sink.Submit(nextFrame, inputs);
                }
                else
                {
                    if (world.Services is IWorldServiceContainer c)
                    {
                        Log.Error($"[FrameSyncDriverModule] IWorldInputSink resolve failed; registered={c.IsRegistered(typeof(AbilityKit.Ability.Host.IWorldInputSink))}. worldId={worldId}");
                    }
                    else
                    {
                        Log.Error($"[FrameSyncDriverModule] IWorldInputSink resolve failed. worldId={worldId}, servicesType={world.Services.GetType().FullName}");
                    }
                }

                world.Tick(deltaTime);

                WorldStateSnapshot? state = null;
                if (world.Services != null && world.Services.TryResolve<AbilityKit.Ability.Host.IWorldStateSnapshotProvider>(out var provider) && provider != null)
                {
                    if (provider.TryGetSnapshot(nextFrame, out var snapshot))
                    {
                        state = snapshot;
                    }
                }

                var packet = new FramePacket(worldId, nextFrame, inputs, state);
                _runtime.Broadcast(new FrameMessage(packet));
            }

            if (_postStep.Count > 0)
            {
                for (int i = 0; i < _postStep.Count; i++)
                {
                    _postStep[i]?.Invoke(nextFrame, deltaTime);
                }
            }

            _frame = nextFrame;
        }
    }
}
