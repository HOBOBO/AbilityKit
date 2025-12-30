using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class ClientPredictionRunner
    {
        public Action<string> Log;

        private readonly IWorld _world;
        private readonly IWorldInputSink _inputSink;
        private readonly RollbackCoordinator _rollback;
        private readonly InputHistoryRingBuffer _inputs;
        private readonly ClientPredictionReconciler _reconciler;

        private float _fixedDelta;
        private Func<FrameIndex, WorldStateHash> _computeHash;

        public ClientPredictionRunner(IWorld world, IWorldInputSink inputSink, RollbackCoordinator rollback, InputHistoryRingBuffer inputs, ClientPredictionReconciler reconciler)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _inputSink = inputSink ?? throw new ArgumentNullException(nameof(inputSink));
            _rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
            _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
            _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));

            _reconciler.OnRollbackRequested += HandleRollbackRequested;
        }

        public FrameIndex PredictedFrame { get; private set; }

        public void TickPredicted(FrameIndex nextFrame, float fixedDelta, PlayerInputCommand[] inputs, Func<FrameIndex, WorldStateHash> computeHash)
        {
            if (computeHash == null) throw new ArgumentNullException(nameof(computeHash));

            _fixedDelta = fixedDelta;
            _computeHash = computeHash;

            inputs ??= Array.Empty<PlayerInputCommand>();

            _inputSink.Submit(nextFrame, inputs);
            _inputs.Store(nextFrame, inputs);

            _world.Tick(fixedDelta);

            _rollback.CaptureAndStore(nextFrame);

            var hash = _computeHash(nextFrame);
            _reconciler.RecordPredictedHash(nextFrame, hash);

            PredictedFrame = nextFrame;
        }

        public bool OnAuthoritativeStateHash(FrameIndex frame, WorldStateHash authoritative)
        {
            return _reconciler.OnAuthoritativeHash(frame, authoritative);
        }

        private void HandleRollbackRequested(FrameIndex rollbackFrame)
        {
            Log?.Invoke($"Rollback requested at frame={rollbackFrame.Value}");

            var ok = _rollback.TryRestore(rollbackFrame);
            if (!ok)
            {
                Log?.Invoke($"Rollback restore failed (no snapshot). frame={rollbackFrame.Value}");
                return;
            }

            var end = PredictedFrame;
            for (int f = rollbackFrame.Value + 1; f <= end.Value; f++)
            {
                var frame = new FrameIndex(f);
                if (!_inputs.TryGet(frame, out var inputs))
                {
                    inputs = Array.Empty<PlayerInputCommand>();
                }

                _inputSink.Submit(frame, inputs);

                var dt = _fixedDelta;
                if (dt <= 0f)
                {
                    throw new InvalidOperationException("ClientPredictionRunner fixedDelta is not set. Call TickPredicted at least once before rollback.");
                }
                _world.Tick(dt);
                _rollback.CaptureAndStore(frame);

                if (_computeHash != null)
                {
                    var hash = _computeHash(frame);
                    _reconciler.RecordPredictedHash(frame, hash);
                }
            }

            Log?.Invoke($"Rollback replay finished. toFrame={end.Value}");
        }

        public void Reset()
        {
            PredictedFrame = new FrameIndex(0);
            _inputs.Clear();
            _rollback.ClearHistory();
            _reconciler.Clear();
        }
    }
}
