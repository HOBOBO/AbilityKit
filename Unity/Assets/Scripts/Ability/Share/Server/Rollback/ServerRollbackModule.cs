using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Server.Rollback
{
    public sealed class ServerRollbackModule
    {
        private sealed class WorldContext
        {
            public IWorld World;
            public IWorldInputSink InputSink;
            public RollbackCoordinator Coordinator;
            public InputHistoryRingBuffer InputHistory;
            public int CaptureCounter;
        }

        private readonly int _historyFrames;
        private readonly int _captureEveryNFrames;
        private readonly Func<IWorld, RollbackRegistry> _buildRegistry;
        private readonly Dictionary<WorldId, WorldContext> _contexts = new Dictionary<WorldId, WorldContext>();

        public ServerRollbackModule(int historyFrames, int captureEveryNFrames, Func<IWorld, RollbackRegistry> buildRegistry)
        {
            if (historyFrames <= 0) throw new ArgumentOutOfRangeException(nameof(historyFrames));
            if (captureEveryNFrames <= 0) throw new ArgumentOutOfRangeException(nameof(captureEveryNFrames));

            _historyFrames = historyFrames;
            _captureEveryNFrames = captureEveryNFrames;
            _buildRegistry = buildRegistry;
        }

        public void Attach(LogicWorldServerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.OnWorldCreated = Combine(options.OnWorldCreated, OnWorldCreated);
            options.OnWorldDestroyed = Combine(options.OnWorldDestroyed, OnWorldDestroyed);
            options.OnInputsFlushed = Combine(options.OnInputsFlushed, OnInputsFlushed);
            options.OnPostStep = Combine(options.OnPostStep, OnPostStep);
        }

        public bool TryRollbackAndReplay(WorldId worldId, FrameIndex rollbackFrame, FrameIndex replayToFrame, float deltaTimePerFrame)
        {
            if (!_contexts.TryGetValue(worldId, out var ctx) || ctx == null) return false;

            if (!ctx.Coordinator.TryRestore(rollbackFrame))
            {
                return false;
            }

            for (int f = rollbackFrame.Value + 1; f <= replayToFrame.Value; f++)
            {
                var frame = new FrameIndex(f);

                if (!ctx.InputHistory.TryGet(frame, out var inputs))
                {
                    inputs = Array.Empty<PlayerInputCommand>();
                }

                ctx.InputSink?.Submit(frame, inputs);
                ctx.World.Tick(deltaTimePerFrame);

                ctx.Coordinator.CaptureAndStore(frame);
            }

            return true;
        }

        private void OnWorldCreated(IWorld world)
        {
            if (world == null) return;

            var registry = _buildRegistry != null ? _buildRegistry(world) : new RollbackRegistry();
            var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(_historyFrames));

            IWorldInputSink sink = null;
            if (world.Services != null)
            {
                world.Services.TryGet<IWorldInputSink>(out sink);
            }

            _contexts[world.Id] = new WorldContext
            {
                World = world,
                InputSink = sink,
                Coordinator = coordinator,
                InputHistory = new InputHistoryRingBuffer(_historyFrames),
                CaptureCounter = 0
            };
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            _contexts.Remove(worldId);
        }

        private void OnInputsFlushed(WorldId worldId, FrameIndex nextFrame, PlayerInputCommand[] inputs)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null)
            {
                ctx.InputHistory.Store(nextFrame, inputs);
            }
        }

        private void OnPostStep(FrameIndex frame, float deltaTime)
        {
            foreach (var kv in _contexts)
            {
                var ctx = kv.Value;
                if (ctx == null) continue;

                ctx.CaptureCounter++;
                if (ctx.CaptureCounter % _captureEveryNFrames != 0) continue;
                ctx.Coordinator.CaptureAndStore(frame);
            }
        }

        private static Action<T> Combine<T>(Action<T> a, Action<T> b)
        {
            return a == null ? b : (Action<T>)Delegate.Combine(a, b);
        }

        private static Action<T1, T2, T3> Combine<T1, T2, T3>(Action<T1, T2, T3> a, Action<T1, T2, T3> b)
        {
            return a == null ? b : (Action<T1, T2, T3>)Delegate.Combine(a, b);
        }

        private static Action<T1, T2> Combine<T1, T2>(Action<T1, T2> a, Action<T1, T2> b)
        {
            return a == null ? b : (Action<T1, T2>)Delegate.Combine(a, b);
        }
    }
}
