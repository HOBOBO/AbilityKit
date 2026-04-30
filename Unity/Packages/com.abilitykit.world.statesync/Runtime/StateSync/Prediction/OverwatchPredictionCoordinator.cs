using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Hash;
using AbilityKit.Ability.StateSync.Snapshot;

namespace AbilityKit.Ability.StateSync
{
    public sealed class OverwatchPredictionCoordinator : IPredictionCoordinator
    {
        public Action<int> OnRollbackRequested;
        public Action<int, int> OnRollbackCompleted;
        public Action<int> OnPredictionError;
        public Action<int> OnServerStateProcessed;
        public Action<string> Log;

        private readonly int _localPlayerId;
        private readonly InputBuffer _inputBuffer;
        private readonly SnapshotBuffer _snapshotBuffer;
        private readonly StateHashValidator _hashValidator;
        private readonly KeyFrameStrategy _keyFrameStrategy;

        private int _currentPredictedFrame;
        private int _serverConfirmedFrame;
        private int _lastProcessedServerFrame;
        private bool _needsRollback;
        private readonly Stack<RollbackOperation> _rollbackStack = new Stack<RollbackOperation>();
        private readonly List<int> _desyncHistory = new List<int>();

        public int LocalPlayerId => _localPlayerId;
        public int CurrentPredictedFrame => _currentPredictedFrame;
        public int ServerConfirmedFrame => _serverConfirmedFrame;
        public bool NeedsRollback => _needsRollback;

        public OverwatchPredictionCoordinator(
            int localPlayerId,
            InputBuffer inputBuffer,
            SnapshotBuffer snapshotBuffer,
            StateHashValidator hashValidator = null,
            KeyFrameStrategy keyFrameStrategy = null)
        {
            _localPlayerId = localPlayerId;
            _inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));
            _snapshotBuffer = snapshotBuffer ?? throw new ArgumentNullException(nameof(snapshotBuffer));
            _hashValidator = hashValidator ?? new StateHashValidator();
            _keyFrameStrategy = keyFrameStrategy ?? KeyFrameStrategy.OverwatchStyle();
        }

        public void RecordInput(int frame, PlayerInputCommand input)
        {
            if (input.PlayerId == _localPlayerId)
            {
                _inputBuffer.Store(frame, input);
                Log?.Invoke($"[Input] Recorded local input frame={frame} triggers={input.TriggerRequests}");
            }
        }

        public void ProcessServerState(int serverFrame, ServerGameState state)
        {
            Log?.Invoke($"[ServerState] Received frame={serverFrame} hash={state.Hash} keyFrame={state.IsKeyFrame}");

            if (serverFrame <= _lastProcessedServerFrame)
            {
                Log?.Invoke($"[ServerState] Ignoring stale server state frame={serverFrame} lastProcessed={_lastProcessedServerFrame}");
                return;
            }

            _lastProcessedServerFrame = serverFrame;

            if (serverFrame < _currentPredictedFrame)
            {
                QueueRollback(serverFrame, state);
            }
            else if (serverFrame == _currentPredictedFrame)
            {
                ValidatePrediction(serverFrame, state);
            }
            else
            {
                _serverConfirmedFrame = serverFrame;
            }

            OnServerStateProcessed?.Invoke(serverFrame);
        }

        public void ExecuteRollback()
        {
            while (_rollbackStack.Count > 0)
            {
                var rollback = _rollbackStack.Pop();
                PerformRollback(rollback);
            }

            _needsRollback = false;
        }

        public void AdvancePrediction()
        {
            _currentPredictedFrame++;
            Log?.Invoke($"[Prediction] Advanced to frame={_currentPredictedFrame}");
        }

        public void Reset()
        {
            _currentPredictedFrame = 0;
            _serverConfirmedFrame = 0;
            _lastProcessedServerFrame = -1;
            _needsRollback = false;
            _rollbackStack.Clear();
            _desyncHistory.Clear();
            _inputBuffer.Clear();
            _snapshotBuffer.Clear();

            Log?.Invoke("[Prediction] Reset complete");
        }

        private void QueueRollback(int serverFrame, ServerGameState state)
        {
            var rollback = new RollbackOperation
            {
                ServerFrame = serverFrame,
                ServerState = state,
                FromFrame = _currentPredictedFrame
            };

            _rollbackStack.Push(rollback);
            _needsRollback = true;

            Log?.Invoke($"[Rollback] Queued rollback to frame={serverFrame} from frame={_currentPredictedFrame}");
        }

        private void PerformRollback(RollbackOperation rollback)
        {
            Log?.Invoke($"[Rollback] Executing rollback to frame={rollback.ServerFrame}");

            if (!_snapshotBuffer.TryGet(rollback.ServerFrame, out var serverSnapshot))
            {
                Log?.Invoke($"[Rollback] WARNING: No snapshot found for frame={rollback.ServerFrame}, cannot restore");
                return;
            }

            RestoreSnapshot(serverSnapshot);

            int startFrame = rollback.ServerFrame + 1;
            int endFrame = rollback.FromFrame;
            int rollbackDistance = endFrame - startFrame + 1;

            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                if (_inputBuffer.TryGet(frame, out var input))
                {
                    ExecuteInput(input);
                }

                SimulateFrame(frame);
                CaptureSnapshot(frame);
            }

            _currentPredictedFrame = endFrame;
            _serverConfirmedFrame = rollback.ServerFrame;

            OnRollbackCompleted?.Invoke(rollback.ServerFrame, rollbackDistance);
            OnPredictionError?.Invoke(rollbackDistance);
            _desyncHistory.Add(rollback.ServerFrame);

            Log?.Invoke($"[Rollback] Completed. Rolled back {rollbackDistance} frames. Now at frame={_currentPredictedFrame}");
        }

        private void ValidatePrediction(int frame, ServerGameState serverState)
        {
            if (!_snapshotBuffer.TryGet(frame, out var localSnapshot))
            {
                Log?.Invoke($"[Validate] No local snapshot for frame={frame}");
                return;
            }

            var validation = _hashValidator.Validate(
                frame,
                localSnapshot,
                WorldStateSnapshot.FromBytes(serverState.StateData));

            if (!validation.IsValid)
            {
                Log?.Invoke($"[Validate] DESYNC detected: {validation}");
                QueueRollback(frame, serverState);
            }
            else
            {
                Log?.Invoke($"[Validate] Prediction correct for frame={frame}");
            }
        }

        private void RestoreSnapshot(Snapshot.WorldStateSnapshot snapshot)
        {
            Log?.Invoke($"[Restore] Restoring snapshot frame={snapshot.Frame}");
        }

        private void ExecuteInput(PlayerInputCommand input)
        {
            Log?.Invoke($"[Execute] Input frame={input.Frame} triggers={input.TriggerRequests}");
        }

        private void SimulateFrame(int frame)
        {
        }

        private void CaptureSnapshot(int frame)
        {
        }

        public IReadOnlyList<int> GetDesyncHistory() => _desyncHistory.ToArray();

        public float GetDesyncRate(int windowFrames)
        {
            if (_desyncHistory.Count == 0) return 0f;
            int recentDesyncs = 0;
            int startFrame = _currentPredictedFrame - windowFrames;
            foreach (var frame in _desyncHistory)
            {
                if (frame >= startFrame) recentDesyncs++;
            }
            return (float)recentDesyncs / windowFrames;
        }

        private struct RollbackOperation
        {
            public int ServerFrame;
            public int FromFrame;
            public ServerGameState ServerState;
        }
    }
}
