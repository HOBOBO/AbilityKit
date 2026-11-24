#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.Rollback
{
    /// <summary>
    /// 为本次安装期间由宿主创建的世界保存服务端输入历史与回滚快照。
    /// 仅对仍由世界管理器登记的同一实例提供捕获与重演。
    /// </summary>
    public sealed class ServerRollbackModule : IHostRuntimeModule
    {
        /// <summary>
        /// 绑定一个世界实例及其本地历史，不拥有世界或输入服务的释放权。
        /// </summary>
        private sealed class WorldContext
        {
            internal WorldContext(IWorld world, IWorldInputSink? inputSink, RollbackCoordinator coordinator, int historyFrames)
            {
                World = world;
                InputSink = inputSink;
                Coordinator = coordinator;
                InputHistory = new InputHistoryRingBuffer(historyFrames);
            }

            public readonly IWorld World;
            public readonly IWorldInputSink? InputSink;
            public readonly RollbackCoordinator Coordinator;
            public readonly InputHistoryRingBuffer InputHistory;
            public int CaptureCounter;
        }

        private readonly int _historyFrames;
        private readonly int _captureEveryNFrames;
        private readonly Func<IWorld, RollbackRegistry>? _buildRegistry;
        private readonly Dictionary<WorldId, WorldContext> _contexts = new Dictionary<WorldId, WorldContext>();
        private readonly List<WorldId> _detached = new List<WorldId>();

        private readonly Action<IWorld> _onWorldCreated;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPreTick;
        private readonly Action<WorldId, FrameIndex, PlayerInputCommand[]> _onInputsFlushed;
        private readonly Action<FrameIndex, float> _onPostStep;

        private HostRuntime? _runtime;
        private IFrameSyncDriverEvents? _frameEvents;

        /// <summary>
        /// 设置历史容量、捕获间隔及逐世界的状态提供器注册方式。
        /// </summary>
        public ServerRollbackModule(int historyFrames, int captureEveryNFrames, Func<IWorld, RollbackRegistry>? buildRegistry)
        {
            if (historyFrames <= 0) throw new ArgumentOutOfRangeException(nameof(historyFrames));
            if (captureEveryNFrames <= 0) throw new ArgumentOutOfRangeException(nameof(captureEveryNFrames));

            _historyFrames = historyFrames;
            _captureEveryNFrames = captureEveryNFrames;
            _buildRegistry = buildRegistry;

            _onWorldCreated = OnWorldCreated;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPreTick = OnPreTick;
            _onInputsFlushed = OnInputsFlushed;
            _onPostStep = OnPostStep;
        }

        /// <summary>
        /// 在帧同步驱动之后安装，订阅世界生命周期及帧同步回调，不补建已有世界的历史。
        /// </summary>
        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (!runtime.Features.TryGetFeature<IFrameSyncDriverEvents>(out _frameEvents) || _frameEvents == null)
            {
                throw new InvalidOperationException($"{nameof(ServerRollbackModule)} requires {nameof(IFrameSyncDriverEvents)} feature. Install {nameof(FrameSyncDriverModule)} first.");
            }

            _runtime = runtime;
            options.WorldCreated.Add(_onWorldCreated);
            options.WorldDestroyed.Add(_onWorldDestroyed);
            options.PreTick.Add(_onPreTick);

            _frameEvents.AddInputsFlushed(_onInputsFlushed);
            _frameEvents.AddPostStep(_onPostStep);
        }

        /// <summary>
        /// 解除回调并丢弃本次安装的回滚上下文；不释放宿主登记的世界。
        /// </summary>
        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.WorldCreated.Remove(_onWorldCreated);
            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PreTick.Remove(_onPreTick);

            _frameEvents?.RemoveInputsFlushed(_onInputsFlushed);
            _frameEvents?.RemovePostStep(_onPostStep);
            _frameEvents = null;
            _runtime = null;
            _contexts.Clear();
            _detached.Clear();
        }

        /// <summary>
        /// 恢复当前世界的指定帧末状态，并从下一帧重演到目标帧。
        /// 未安装、世界已摘除或无法恢复时返回假；保留重演异常的传播行为。
        /// </summary>
        public bool TryRollbackAndReplay(WorldId worldId, FrameIndex rollbackFrame, FrameIndex replayToFrame, float deltaTimePerFrame)
        {
            if (!_contexts.TryGetValue(worldId, out var ctx) || !IsRegistered(ctx.World)) return false;

            if (!ctx.Coordinator.TryRestore(rollbackFrame))
            {
                return false;
            }

            var replaySink = ctx.InputSink as IWorldInputReplaySink;
            replaySink?.BeginReplay(rollbackFrame, replayToFrame);
            try
            {
                for (int f = rollbackFrame.Value + 1; f <= replayToFrame.Value; f++)
                {
                    var frame = new FrameIndex(f);

                    if (!ctx.InputHistory.TryGet(frame, out var inputs))
                    {
                        inputs = Array.Empty<PlayerInputCommand>();
                    }

                    if (replaySink != null)
                    {
                        replaySink.Replay(frame, inputs);
                    }
                    else
                    {
                        ctx.InputSink?.Submit(frame, inputs);
                    }
                    ctx.World.Tick(deltaTimePerFrame);

                    if (!ctx.Coordinator.CaptureAndStore(frame)) return false;
                }
            }
            finally
            {
                replaySink?.EndReplay();
            }

            return true;
        }

        private void OnWorldCreated(IWorld world)
        {
            if (world == null || !IsRegistered(world)) return;

            var registry = _buildRegistry != null ? _buildRegistry(world) : new RollbackRegistry();
            var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(_historyFrames));

            IWorldInputSink? sink = null;
            if (world.Services != null)
            {
                world.Services.TryResolve<IWorldInputSink>(out sink);
            }

            if (IsRegistered(world))
            {
                _contexts[world.Id] = new WorldContext(world, sink, coordinator, _historyFrames);
            }
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            if (_contexts.TryGetValue(worldId, out var context) && !IsRegistered(context.World))
            {
                _contexts.Remove(worldId);
            }
        }

        private bool IsRegistered(IWorld world)
        {
            return _runtime != null && _runtime.TryGetWorld(world.Id, out var current) && ReferenceEquals(current, world);
        }

        private void OnPreTick(float deltaTime)
        {
            /*
             * 释放异常和批量关闭可能没有销毁回调，也不保证之后还有成功帧可捕获。
             */
            foreach (var entry in _contexts)
            {
                if (!IsRegistered(entry.Value.World)) _detached.Add(entry.Key);
            }

            foreach (var worldId in _detached) _contexts.Remove(worldId);
            _detached.Clear();
        }

        private void OnInputsFlushed(WorldId worldId, FrameIndex nextFrame, PlayerInputCommand[] inputs)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && IsRegistered(ctx.World))
            {
                ctx.InputHistory.Store(nextFrame, inputs);
            }
        }

        private void OnPostStep(FrameIndex frame, float deltaTime)
        {
            foreach (var kv in _contexts)
            {
                var ctx = kv.Value;
                if (!IsRegistered(ctx.World)) continue;

                if (ctx.World.Services?.TryResolve<IWorldFrameGuard>(out var guard) == true && !guard.CanCommit) continue;

                ctx.CaptureCounter++;
                if (ctx.CaptureCounter % _captureEveryNFrames != 0) continue;
                ctx.Coordinator.CaptureAndStore(frame);
            }
        }
    }
}
