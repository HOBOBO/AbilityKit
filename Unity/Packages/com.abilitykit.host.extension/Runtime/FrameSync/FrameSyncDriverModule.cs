#nullable enable annotations
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 在宿主帧内收集并发布会话输入，可绑定当前世界实例，也可显式登记无世界会话。
    /// </summary>
    public sealed class FrameSyncDriverModule : IHostRuntimeModule, IFrameSyncInputHub, IFrameSyncDriverEvents
    {
        /// <summary>
        /// 保存会话待提交的输入及其世界绑定，不拥有世界的释放权。
        /// </summary>
        private sealed class WorldSession
        {
            public readonly List<PlayerInputCommand> PendingInputs = new List<PlayerInputCommand>(16);

            public IWorld? World;
            public FrameIndex PendingFrame;
            public PlayerInputCommand[] PendingInputsArray;
        }

        private readonly Dictionary<WorldId, WorldSession> _sessions = new Dictionary<WorldId, WorldSession>();
        private readonly List<WorldId> _detachedSessions = new List<WorldId>();

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

        /// <summary>
        /// 解除宿主回调，丢弃本次安装的会话及其中等待提交的输入。
        /// </summary>
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
            _detachedSessions.Clear();
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

        /// <summary>
        /// 显式登记无世界会话；不解除已登记的世界绑定。
        /// </summary>
        public void RegisterSession(WorldId worldId)
        {
            if (!_sessions.TryGetValue(worldId, out var session) || !IsCurrentSession(worldId, session))
                _sessions[worldId] = new WorldSession();
        }

        public void UnregisterSession(WorldId worldId)
        {
            _sessions.Remove(worldId);
        }

        /// <summary>
        /// 向会话排入输入；不存在或已脱离登记世界的会话拒绝输入。
        /// </summary>
        public bool SubmitInput(ServerClientId clientId, WorldId worldId, PlayerInputCommand input)
        {
            if (_runtime == null) return false;
            if (!_sessions.TryGetValue(worldId, out var session) || !IsCurrentSession(worldId, session))
            {
                Log.Error($"[FrameSyncDriverModule] SubmitInput rejected: session not found. worldId={worldId}, clientId={clientId.Value}, opCode={input.OpCode}");
                return false;
            }

            session.PendingInputs.Add(input);
            return true;
        }

        private void OnWorldCreated(IWorld world)
        {
            if (world == null || !IsRegistered(world.Id, world)) return;

            if (!_sessions.TryGetValue(world.Id, out var session) || !IsCurrentSession(world.Id, session))
                _sessions[world.Id] = session = new WorldSession();

            session.World = world;
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            if (_sessions.TryGetValue(worldId, out var session) &&
                (session.World == null || !IsCurrentSession(worldId, session)))
                _sessions.Remove(worldId);
        }

        private bool IsRegistered(WorldId worldId, IWorld world)
        {
            return _runtime != null && _runtime.TryGetWorld(worldId, out var current) && ReferenceEquals(current, world);
        }

        private bool IsCurrentSession(WorldId worldId, WorldSession session)
        {
            return session.World == null || IsRegistered(worldId, session.World);
        }

        private void PruneDetachedSessions()
        {
            foreach (var entry in _sessions)
            {
                if (!IsCurrentSession(entry.Key, entry.Value)) _detachedSessions.Add(entry.Key);
            }

            foreach (var worldId in _detachedSessions) _sessions.Remove(worldId);
            _detachedSessions.Clear();
        }

        private void OnPreTick(float deltaTime)
        {
            if (_runtime == null) return;

            PruneDetachedSessions();
            var nextFrame = new FrameIndex(_frame.Value + 1);

            foreach (var kv in _sessions)
            {
                var worldId = kv.Key;
                var session = kv.Value;

                var world = session.World;
                if (!IsCurrentSession(worldId, session)) continue;

                if (world?.Services?.TryResolve<IWorldFrameGuard>(out var preGuard) == true && !preGuard.CanAdvance) continue;

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

                if (world != null && IsRegistered(worldId, world))
                {
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
        }

        private void OnPostTick(float deltaTime)
        {
            if (_runtime == null) return;

            var currentFrame = new FrameIndex(_frame.Value + 1);
            var anySessionCommitted = false;

            foreach (var kv in _sessions)
            {
                var worldId = kv.Key;
                var session = kv.Value;

                var world = session.World;
                if (!IsCurrentSession(worldId, session)) continue;

                var frame = session.PendingFrame.Value > 0 ? session.PendingFrame : currentFrame;
                /*
                 * 世界异常可能被宿主隔离；独立核对完成状态，禁止发送失败帧及空成功包。
                 */
                if (world?.Services?.TryResolve<IWorldFrameGuard>(out var postGuard) == true && !postGuard.CanCommit) continue;
                anySessionCommitted = true;
                var inputs = session.PendingInputsArray ?? Array.Empty<PlayerInputCommand>();

                var broadcasted = false;
                if (world?.Services != null && world.Services.TryResolve<AbilityKit.Ability.Host.IWorldStateSnapshotProvider>(out var provider) && provider != null)
                {
                    if (provider is AbilityKit.Ability.Host.IWorldStateSnapshotBatchProvider batchProvider)
                    {
                        var snapshots = new List<WorldStateSnapshot>(16);
                        var count = batchProvider.CollectSnapshots(frame, snapshots, 32);
                        var snapshotCount = Math.Min(count, snapshots.Count);
                        for (int i = 0; i < snapshotCount; i++)
                        {
                            var packetInputs = i == 0 ? inputs : Array.Empty<PlayerInputCommand>();
                            _runtime.Broadcast(new FrameMessage(new FramePacket(worldId, frame, packetInputs, snapshots[i])));
                            broadcasted = true;
                        }
                    }
                    else if (provider.TryGetSnapshot(frame, out var snapshot))
                    {
                        _runtime.Broadcast(new FrameMessage(new FramePacket(worldId, frame, inputs, snapshot)));
                        broadcasted = true;
                    }
                }

                if (!broadcasted)
                {
                    _runtime.Broadcast(new FrameMessage(new FramePacket(worldId, frame, inputs, default)));
                }

                session.PendingInputsArray = null;
                session.PendingFrame = default;
            }

            if (_sessions.Count > 0 && !anySessionCommitted) return;

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
}
