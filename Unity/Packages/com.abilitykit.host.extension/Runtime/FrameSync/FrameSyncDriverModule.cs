using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public sealed class FrameSyncDriverModule : IHostRuntimeModule, IFrameSyncInputHub, IFrameSyncDriverEvents
    {
        private sealed class WorldSession
        {
            public readonly List<PlayerInputCommand> PendingInputs = new List<PlayerInputCommand>(16);

            public FrameIndex PendingFrame;
            public PlayerInputCommand[] PendingInputsArray;
        }

        private readonly Dictionary<WorldId, WorldSession> _sessions = new Dictionary<WorldId, WorldSession>();

        private HostRuntime _runtime;
        private HostRuntimeOptions _options;

        private readonly Action<IWorld> _onWorldCreated;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPreTick;
        private readonly Action<float> _onPostTick;

        private readonly List<Action<WorldId, FrameIndex, PlayerInputCommand[]>> _inputsFlushed = new List<Action<WorldId, FrameIndex, PlayerInputCommand[]>>(8);
        private readonly List<Action<FrameIndex, float>> _postStep = new List<Action<FrameIndex, float>>(8);

        private FrameIndex _frame;

        public FrameSyncDriverModule()
        {
            _onWorldCreated = OnWorldCreated;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPreTick = OnPreTick;
            _onPostTick = OnPostTick;
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
            options.PostTick.Add(_onPostTick);

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
            options.PostTick.Remove(_onPostTick);

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

                session.PendingFrame = nextFrame;
                session.PendingInputsArray = inputs;

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
            }
        }

        private void OnPostTick(float deltaTime)
        {
            if (_runtime == null) return;

            var currentFrame = new FrameIndex(_frame.Value + 1);

            foreach (var kv in _sessions)
            {
                var worldId = kv.Key;
                var session = kv.Value;

                if (!_runtime.Worlds.TryGet(worldId, out var world) || world == null) continue;

                var frame = session.PendingFrame.Value > 0 ? session.PendingFrame : currentFrame;
                var inputs = session.PendingInputsArray ?? Array.Empty<PlayerInputCommand>();

                WorldStateSnapshot? state = null;
                if (world.Services != null && world.Services.TryResolve<AbilityKit.Ability.Host.IWorldStateSnapshotProvider>(out var provider) && provider != null)
                {
                    if (provider.TryGetSnapshot(frame, out var snapshot))
                    {
                        state = snapshot;
                    }
                }

                var packet = new FramePacket(worldId, frame, inputs, state);
                _runtime.Broadcast(new FrameMessage(packet));

                session.PendingInputsArray = null;
                session.PendingFrame = default;
            }

            if (_postStep.Count > 0)
            {
                for (int i = 0; i < _postStep.Count; i++)
                {
                    _postStep[i]?.Invoke(currentFrame, deltaTime);
                }
            }

            _frame = currentFrame;
        }
    }

    public static class WorldCatchUpDriver
    {
        public static int CatchUpAndFeedSnapshots(
            HostRuntime runtime,
            IWorld world,
            int lastTickedFrame,
            int driveTargetFrame,
            float fixedDelta,
            int stepsBudget,
            AbilityKit.Ability.Host.IWorldStateSnapshotProvider provider,
            int maxSnapshotsPerStep,
            Action<FramePacket> feed)
        {
            if (runtime == null) return lastTickedFrame;
            if (world == null) return lastTickedFrame;
            if (driveTargetFrame <= 0) return lastTickedFrame;
            if (stepsBudget <= 0) return lastTickedFrame;

            if (maxSnapshotsPerStep <= 0) maxSnapshotsPerStep = 0;

            var worldId = world.Id;

            var steps = 0;
            while (steps < stepsBudget && lastTickedFrame < driveTargetFrame)
            {
                var nextFrame = lastTickedFrame + 1;
                var frameIndex = new FrameIndex(nextFrame);

                runtime.Tick(fixedDelta);

                if (provider != null && feed != null && maxSnapshotsPerStep > 0)
                {
                    SnapshotProviderDrain.DrainSnapshots(provider, worldId, frameIndex, maxSnapshotsPerStep, feed);
                }

                lastTickedFrame = nextFrame;
                steps++;
            }

            return lastTickedFrame;
        }
    }

    public static class FrameSyncInputHubFactory
    {
        public static FrameJitterBufferHub<TFrame> CreateJitterBufferHub<TFrame>(
            int delayFrames,
            MissingFrameMode missingMode,
            Func<TFrame> missingFrameFactory,
            int initialCapacity = 256)
        {
            var buf = new AbilityKit.Network.Runtime.FrameJitterBuffer<TFrame>(delayFrames, missingMode, missingFrameFactory, initialCapacity);
            return new FrameJitterBufferHub<TFrame>(buf);
        }
    }

    public sealed class FrameJitterBufferHub<TFrame> :
        AbilityKit.Network.Abstractions.IConsumableRemoteFrameSource<TFrame>,
        AbilityKit.Network.Abstractions.IRemoteFrameSink<TFrame>
    {
        private readonly AbilityKit.Network.Runtime.FrameJitterBuffer<TFrame> _buf;

        public FrameJitterBufferHub(AbilityKit.Network.Runtime.FrameJitterBuffer<TFrame> buf)
        {
            _buf = buf ?? throw new ArgumentNullException(nameof(buf));
        }

        public int DelayFrames
        {
            get => _buf.DelayFrames;
            set => _buf.DelayFrames = value;
        }

        public int MaxReceivedFrame => _buf.MaxReceivedFrame;

        public int TargetFrame => _buf.TargetFrame;

        public bool TryGet(int frame, out TFrame frameData) => _buf.TryGet(frame, out frameData);

        public bool TryConsume(int frame, out TFrame frameData) => _buf.TryConsume(frame, out frameData);

        public void TrimBefore(int minFrameInclusive) => _buf.TrimBefore(minFrameInclusive);

        public void Add(int frame, TFrame frameData) => _buf.Add(frame, frameData);

        public void Dispose() => _buf.Dispose();
    }
}
