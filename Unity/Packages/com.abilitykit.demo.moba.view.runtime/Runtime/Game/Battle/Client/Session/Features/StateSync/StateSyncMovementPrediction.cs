using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Protocol.Moba;
using MovementState = AbilityKit.Game.Flow.StateSyncMovementSandbox.MovementState;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 状态同步会话的单角色预测时钟与对账。实际预测只在主线程步进；准备输入的回调只记录值。
    /// 既不推进已导入的权威世界，也不在重演中重新提交任何命令。
    /// </summary>
    internal sealed class StateSyncMovementPrediction : IDisposable, IBattleProjectionViewSource,
        IBattleProjectionViewEpochSource, IActorProjectionProducer
    {
        private const int ReplayLimit = LocalMovementReconciler<MovementState, (float, float)>.DefaultReplayTickLimit;
        private const int InputLimit = LocalMovementReconciler<MovementState, (float, float)>.DefaultPendingInputLimit;
        private readonly object _gate = new object();
        private readonly Guid _inputGeneration;
        private readonly PlayerId _player;
        private readonly ulong _numericWorld;
        private readonly uint _numericPlayer;
        private readonly float _fixedDelta;
        private readonly List<(long Sequence, int Tick, (float X, float Z)? Move)> _beforeAnchor = new(InputLimit);
        private readonly (int Tick, MovementState Value)?[] _history = new (int, MovementState)?[ReplayLimit + 1];
        private readonly (int Tick, MovementState Value)[] _candidateSteps = new (int, MovementState)[ReplayLimit];
        private readonly (int Tick, Transform3 Before, Transform3 After)[] _lastReplay =
            new (int, Transform3, Transform3)[ReplayLimit];
        private LocalMovementReconciler<MovementState, (float X, float Z)> _reconciler;
        private StateSyncMovementSandbox _sandbox;
        private ReconciliationScope _scope;
        private Guid _stream;
        private int _entityVersion;
        private int _clockTick;
        private int _anchorTick;
        private int _candidateCount;
        private long _seenSequence;
        private long _historyLostThrough;
        private double _accumulator;
        private GatewayStateSyncActorSnapshot _actor;
        private bool _unknownSequence;
        private bool _disposed;

        internal StateSyncMovementPrediction(Guid inputGeneration, WorldId world, PlayerId player,
            ulong numericWorld, uint numericPlayer, float fixedDelta)
        {
            _inputGeneration = inputGeneration;
            WorldId = world;
            _player = player;
            _numericWorld = numericWorld;
            _numericPlayer = numericPlayer;
            _fixedDelta = fixedDelta;
        }

        public WorldId WorldId { get; }
        public long ProjectionEpoch { get; private set; }
        internal bool? LastSameTickMatch { get; private set; }
        internal int LastReplayTicks { get; private set; }
        internal int InputObservedTick
        {
            get
            {
                lock (_gate)
                    return !_unknownSequence && _reconciler?.TryGetPrediction(out _) == true ? _clockTick : _anchorTick;
            }
        }

        internal void ObservePreparedInput(object prepared, (float X, float Z)? move)
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (prepared is not SequencedInput input || input.InputGeneration != _inputGeneration ||
                    !input.Request.WorldId.Equals(WorldId) || !input.Request.Input.Player.Equals(_player) ||
                    input.CommandSequence == 0 || input.CommandSequence > long.MaxValue ||
                    input.Request.Input.Frame.Value < 0)
                {
                    _unknownSequence = true;
                    return;
                }

                var sequence = (long)input.CommandSequence;
                var tick = input.Request.Input.Frame.Value;
                if (sequence <= _seenSequence) return;
                if (sequence != _seenSequence + 1)
                    LoseCoverage(sequence - 1, tick);
                _seenSequence = sequence;
                if (input.Request.Input.OpCode != MobaOpCodes.Input.Move || !move.HasValue ||
                    !Finite(move.Value.X) || !Finite(move.Value.Z)) move = null;
                else
                {
                    var intent = MobaActorIntent.MoveDirection(move.Value.X, move.Value.Z);
                    move = (intent.MoveX, intent.MoveZ);
                }

                if (_reconciler != null)
                {
                    Record(sequence, tick, move);
                }
                else if (_beforeAnchor.Count < InputLimit)
                {
                    _beforeAnchor.Add((sequence, tick, move));
                }
                else
                {
                    LoseCoverage(sequence, tick);
                }
            }
        }

        internal void ReceiveCommitted(GatewayStateSyncMovementProof? proof, in GatewayStateSyncSnapshot snapshot)
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (!proof.HasValue || proof.Value.InputGeneration != _inputGeneration ||
                    proof.Value.WorldId != _numericWorld || proof.Value.PlayerId != _numericPlayer || _unknownSequence)
                {
                    SuspendCore();
                    _anchorTick = snapshot.Frame;
                    return;
                }

                var value = proof.Value;
                if (_reconciler == null || _stream != value.StreamGeneration || _actor.ActorId != value.ActorId ||
                    _entityVersion != value.EntityVersion)
                {
                    if (_reconciler != null) SuspendCore();
                    _stream = value.StreamGeneration;
                    _entityVersion = value.EntityVersion;
                    _scope = new ReconciliationScope(1, _numericWorld, _stream, value.ActorId, _entityVersion,
                        unchecked((int)_numericPlayer), 1);
                    _reconciler = new LocalMovementReconciler<MovementState, (float X, float Z)>(_scope, Step);
                    foreach (var input in _beforeAnchor)
                        if (input.Sequence > value.TerminalThrough)
                            Record(input.Sequence, input.Tick, input.Move);
                    _beforeAnchor.Clear();
                    _clockTick = value.Tick;
                    ProjectionEpoch++;
                }

                foreach (var actor in snapshot.Actors)
                    if (actor.ActorId == value.ActorId) { _actor = actor; break; }
                if (value.TerminalThrough > _seenSequence) _seenSequence = value.TerminalThrough;
                if (_historyLostThrough > value.TerminalThrough)
                    _reconciler.RecordUnsupportedInput(_scope, _historyLostThrough, value.Tick);
                _anchorTick = value.Tick;
                if (_clockTick < value.Tick)
                {
                    _clockTick = value.Tick;
                    _accumulator = 0;
                }
                var pose = new Transform3(new Vec3(value.X, value.Y, value.Z),
                    new Quat(value.RotationX, value.RotationY, value.RotationZ, value.RotationW), Vec3.One);
                var state = new MovementState(pose, value.MoveX, value.MoveZ, value.MoveSpeed,
                    value.CanMove && value.HasIsolatedLocomotion);
                var previous = _history[value.Tick % _history.Length];
                LastSameTickMatch = previous.HasValue && previous.Value.Tick == value.Tick
                    ? previous.Value.Value.Equals(state) : null;
                var committed = new CommittedMovement<MovementState>(_scope, value.SnapshotId,
                    value.Tick, state, snapshot.IsFullSnapshot);
                var terminal = new InputTerminalProof(_scope, value.SnapshotId, value.Tick, value.TerminalThrough);
                _candidateCount = 0;
                var result = _reconciler.Reconcile(in committed, terminal, _clockTick, LastSameTickMatch == true);
                LastReplayTicks = result.Status == ReconciliationStatus.Predicted ? result.AttemptedTicks : 0;
                for (var index = 0; index < LastReplayTicks; index++)
                {
                    var step = _candidateSteps[index];
                    var before = index == 0 ? pose : _candidateSteps[index - 1].Value.Pose;
                    _lastReplay[index] = (step.Tick, before, step.Value.Pose);
                }
                if (result.Status != ReconciliationStatus.Predicted || result.AttemptedTicks > 0)
                    Array.Clear(_history, 0, _history.Length);
                PublishCandidateHistory();
            }
        }

        /// <summary>
        /// 读取上一次权威交付中成功追平的候选步进，不泄露失败候选或普通向前预测。
        /// </summary>
        internal (int Tick, Transform3 Before, Transform3 After)[] CaptureLastReplay()
        {
            lock (_gate)
            {
                if (LastReplayTicks == 0) return Array.Empty<(int, Transform3, Transform3)>();
                var replay = new (int, Transform3, Transform3)[LastReplayTicks];
                Array.Copy(_lastReplay, replay, replay.Length);
                return replay;
            }
        }

        internal void Tick(float elapsed)
        {
            if (!Finite(elapsed) || elapsed <= 0) return;
            lock (_gate)
            {
                if (_disposed || _reconciler == null || _unknownSequence) return;
                _accumulator = Math.Min(_accumulator + elapsed, _fixedDelta * SessionSimRuntimeTuning.MaxCatchUpStepsPerUpdate);
                while (_accumulator >= _fixedDelta && (long)_clockTick - _anchorTick < ReplayLimit && _clockTick < int.MaxValue)
                {
                    _accumulator -= _fixedDelta;
                    _reconciler.SealTick(_scope, ++_clockTick);
                    _candidateCount = 0;
                    _reconciler.PredictTo(_clockTick);
                    PublishCandidateHistory();
                }
                if (_accumulator >= _fixedDelta && (long)_clockTick - _anchorTick == ReplayLimit && _clockTick < int.MaxValue)
                    _reconciler.PredictTo(_clockTick + 1);
            }
        }

        private bool Step(int tick, ReadOnlySpan<(float X, float Z)> inputs, ref MovementState candidate)
        {
            if (!candidate.Eligible) return false;
            _sandbox ??= new StateSyncMovementSandbox(_fixedDelta);
            if (!_sandbox.Step(inputs, ref candidate)) return false;
            _candidateSteps[_candidateCount++] = (tick, candidate);
            return true;
        }

        private void PublishCandidateHistory()
        {
            if (!_reconciler.TryGetPrediction(out _)) return;
            for (var index = 0; index < _candidateCount; index++)
            {
                var sample = _candidateSteps[index];
                _history[sample.Tick % _history.Length] = sample;
            }
        }

        private void Record(long sequence, int tick, (float X, float Z)? move)
        {
            if (move.HasValue) _reconciler.TryRecordInput(_scope, sequence, tick, move.Value);
            else _reconciler.RecordUnsupportedInput(_scope, sequence, tick);
        }

        private void LoseCoverage(long sequence, int tick)
        {
            _historyLostThrough = Math.Max(_historyLostThrough, sequence);
            _reconciler?.RecordUnsupportedInput(_scope, sequence, tick);
        }

        internal void Suspend()
        {
            lock (_gate) SuspendCore();
        }

        private void SuspendCore()
        {
            if (_reconciler != null) ProjectionEpoch++;
            _reconciler = null;
            _actor = default;
            _historyLostThrough = _seenSequence;
            _beforeAnchor.Clear();
            Array.Clear(_history, 0, _history.Length);
            _accumulator = 0;
            LastSameTickMatch = null;
            LastReplayTicks = 0;
        }

        internal bool TryGetState(out Transform3 pose, out int tick, out bool predicted)
        {
            lock (_gate)
            {
                pose = default;
                tick = _anchorTick;
                predicted = false;
                if (_reconciler == null || !_reconciler.TryGetAuthoritative(out var state)) return false;
                if (!_unknownSequence && _reconciler.TryGetPrediction(out var prediction))
                {
                    state = prediction;
                    predicted = true;
                    tick = _clockTick;
                }
                pose = state.Pose;
                return true;
            }
        }

        public bool TryGetProjection(out IActorProjectionProducer producer, out int frame)
        {
            producer = TryGetState(out _, out frame, out _) ? this : null;
            return producer != null;
        }

        public ActorProjectionData ExtractFull(int actorId)
        {
            if (actorId != _actor.ActorId || !TryGetState(out var pose, out _, out _)) return default;
            return new ActorProjectionData(actorId, pose.Position.X, pose.Position.Y, pose.Position.Z,
                pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W, 1f, 1f, 1f,
                _actor.Hp, _actor.HpMax, _actor.TeamId, 0f, 0f, _actor.Kind, _actor.Code, _actor.OwnerNetId,
                ActorProjectionFields.Core | ActorProjectionFields.Hp | ActorProjectionFields.TeamId | ActorProjectionFields.Spawn);
        }

        public void ExtractAll(List<ActorProjectionData> buffer)
        {
            var actor = ExtractFull(_actor.ActorId);
            if (actor.ActorId > 0) buffer.Add(actor);
        }

        public ActorProjectionData ExtractSpawn(int actorId) => ExtractFull(actorId);

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                SuspendCore();
                _sandbox?.Dispose();
                _sandbox = null;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
