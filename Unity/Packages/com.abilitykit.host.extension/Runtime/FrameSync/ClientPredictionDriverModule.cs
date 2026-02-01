using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public sealed class ClientPredictionDriverModule : IHostRuntimeModule, IClientPredictionDriverStats
    {
        private sealed class WorldContext
        {
            public IWorld World;
            public IWorldInputSink InputSink;

            public FrameIndex ConfirmedFrame;
            public FrameIndex PredictedFrame;

            public Queue<LocalPlayerInputEvent[]> LocalDelayQueue;
        }

        private readonly Dictionary<WorldId, WorldContext> _contexts = new Dictionary<WorldId, WorldContext>();

        private readonly Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> _resolveRemoteInputs;
        private readonly Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> _resolveLocalInputs;

        private readonly int _inputDelayFrames;

        private readonly int _maxLocalDelayQueueDepth;

        private readonly int _maxConsumeConfirmedFramesPerTick;

        private readonly int _maxConsumePredictedFramesPerTick;

        private int _lastConsumedConfirmedFrames;
        private int _lastConsumedPredictedFrames;
        private long _totalConsumedConfirmedFrames;
        private long _totalPredictedFrames;

        private long _totalLocalDelayQueueDroppedBatches;

        private readonly Action<IWorld> _onWorldCreated;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPreTick;

        private HostRuntime _runtime;
        private HostRuntimeOptions _options;

        public ClientPredictionDriverModule(
            Func<WorldId, IConsumableRemoteFrameSource<PlayerInputCommand[]>> resolveRemoteInputs,
            Func<WorldId, ILocalInputSource<LocalPlayerInputEvent[]>> resolveLocalInputs,
            int inputDelayFrames = 0,
            int maxConsumeConfirmedFramesPerTick = 4,
            int maxConsumePredictedFramesPerTick = 2)
        {
            _resolveRemoteInputs = resolveRemoteInputs;
            _resolveLocalInputs = resolveLocalInputs;

            if (inputDelayFrames < 0) inputDelayFrames = 0;
            _inputDelayFrames = inputDelayFrames;

            _maxLocalDelayQueueDepth = _inputDelayFrames + 6;

            if (maxConsumeConfirmedFramesPerTick <= 0) maxConsumeConfirmedFramesPerTick = 1;
            _maxConsumeConfirmedFramesPerTick = maxConsumeConfirmedFramesPerTick;

            if (maxConsumePredictedFramesPerTick <= 0) maxConsumePredictedFramesPerTick = 1;
            _maxConsumePredictedFramesPerTick = maxConsumePredictedFramesPerTick;

            _onWorldCreated = OnWorldCreated;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPreTick = OnPreTick;
        }

        public int MaxConsumeConfirmedFramesPerTick => _maxConsumeConfirmedFramesPerTick;

        public int MaxConsumePredictedFramesPerTick => _maxConsumePredictedFramesPerTick;

        public int InputDelayFrames => _inputDelayFrames;

        public int LastConsumedConfirmedFrames => _lastConsumedConfirmedFrames;

        public int LastConsumedPredictedFrames => _lastConsumedPredictedFrames;

        public long TotalConsumedConfirmedFrames => _totalConsumedConfirmedFrames;

        public long TotalPredictedFrames => _totalPredictedFrames;

        public long TotalLocalDelayQueueDroppedBatches => _totalLocalDelayQueueDroppedBatches;

        public bool TryGetLocalDelayQueueDepth(WorldId worldId, out int depth)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null && ctx.LocalDelayQueue != null)
            {
                depth = ctx.LocalDelayQueue.Count;
                return true;
            }

            depth = 0;
            return false;
        }

        public bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted)
        {
            if (_contexts.TryGetValue(worldId, out var ctx) && ctx != null)
            {
                confirmed = ctx.ConfirmedFrame;
                predicted = ctx.PredictedFrame;
                return true;
            }

            confirmed = default;
            predicted = default;
            return false;
        }

        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _runtime = runtime;
            _options = options;

            options.WorldCreated.Add(_onWorldCreated);
            options.WorldDestroyed.Add(_onWorldDestroyed);
            options.PreTick.Add(_onPreTick);

            runtime.Features.RegisterFeature<IClientPredictionDriverStats>(this);
        }

        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.WorldCreated.Remove(_onWorldCreated);
            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PreTick.Remove(_onPreTick);

            runtime.Features.UnregisterFeature<IClientPredictionDriverStats>();

            _contexts.Clear();
            _lastConsumedConfirmedFrames = 0;
            _totalConsumedConfirmedFrames = 0;
            _totalPredictedFrames = 0;
            _runtime = null;
            _options = null;
        }

        private void OnWorldCreated(IWorld world)
        {
            if (world == null) return;

            IWorldInputSink sink = null;
            if (world.Services != null)
            {
                world.Services.TryResolve<IWorldInputSink>(out sink);
            }

            _contexts[world.Id] = new WorldContext
            {
                World = world,
                InputSink = sink,
                ConfirmedFrame = new FrameIndex(0),
                PredictedFrame = new FrameIndex(0),
                LocalDelayQueue = new Queue<LocalPlayerInputEvent[]>(_inputDelayFrames + 2)
            };
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            _contexts.Remove(worldId);
        }

        private void OnPreTick(float deltaTime)
        {
            if (_runtime == null) return;

            _lastConsumedConfirmedFrames = 0;
            _lastConsumedPredictedFrames = 0;

            foreach (var kv in _contexts)
            {
                var worldId = kv.Key;
                var ctx = kv.Value;
                if (ctx?.World == null || ctx.InputSink == null) continue;

                var remote = _resolveRemoteInputs != null ? _resolveRemoteInputs(worldId) : null;
                var local = _resolveLocalInputs != null ? _resolveLocalInputs(worldId) : null;

                // Step 1: advance confirmed frames as far as remote input allows.
                if (remote != null)
                {
                    var target = remote.TargetFrame;
                    var start = ctx.ConfirmedFrame.Value + 1;
                    var end = target;
                    if (_maxConsumeConfirmedFramesPerTick > 0)
                    {
                        var maxEnd = start + _maxConsumeConfirmedFramesPerTick - 1;
                        if (maxEnd < end) end = maxEnd;
                    }

                    for (int f = start; f <= end; f++)
                    {
                        var frame = new FrameIndex(f);
                        if (!remote.TryConsume(f, out var inputs) || inputs == null)
                        {
                            inputs = Array.Empty<PlayerInputCommand>();
                        }

                        ctx.InputSink.Submit(frame, inputs);

                        ctx.ConfirmedFrame = frame;
                        _lastConsumedConfirmedFrames++;
                        _totalConsumedConfirmedFrames++;
                        if (ctx.PredictedFrame.Value < frame.Value)
                        {
                            ctx.PredictedFrame = frame;
                        }
                    }
                }

                // Step 2: speculative/predicted step using local input.
                if (local != null)
                {
                    var evts = Array.Empty<LocalPlayerInputEvent>();
                    if (local.TryDequeue(out var dequeued) && dequeued != null)
                    {
                        evts = dequeued;
                    }

                    ctx.LocalDelayQueue ??= new Queue<LocalPlayerInputEvent[]>(_inputDelayFrames + 2);
                    ctx.LocalDelayQueue.Enqueue(evts);

                    while (ctx.LocalDelayQueue.Count > _maxLocalDelayQueueDepth)
                    {
                        ctx.LocalDelayQueue.Dequeue();
                        _totalLocalDelayQueueDroppedBatches++;
                    }

                    var predictedSteps = 0;
                    while (predictedSteps < _maxConsumePredictedFramesPerTick && (ctx.LocalDelayQueue.Count - _inputDelayFrames) > 0)
                    {
                        var delayed = ctx.LocalDelayQueue.Dequeue() ?? Array.Empty<LocalPlayerInputEvent>();

                        var next = new FrameIndex(ctx.PredictedFrame.Value + 1);

                        PlayerInputCommand[] predictedInputs;
                        if (delayed.Length == 0)
                        {
                            predictedInputs = Array.Empty<PlayerInputCommand>();
                        }
                        else
                        {
                            predictedInputs = new PlayerInputCommand[delayed.Length];
                            for (int i = 0; i < delayed.Length; i++)
                            {
                                var e = delayed[i];
                                predictedInputs[i] = new PlayerInputCommand(next, e.PlayerId, e.OpCode, e.Payload ?? Array.Empty<byte>());
                            }
                        }

                        ctx.InputSink.Submit(next, predictedInputs);
                        ctx.PredictedFrame = next;
                        _totalPredictedFrames++;
                        _lastConsumedPredictedFrames++;
                        predictedSteps++;
                    }
                }
            }
        }
    }
}
