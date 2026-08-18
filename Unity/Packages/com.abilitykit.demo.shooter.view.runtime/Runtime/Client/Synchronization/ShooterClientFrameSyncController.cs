#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host.Extensions.Client.FrameSync;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using FrameworkPlayerInputCommand = AbilityKit.Ability.Host.PlayerInputCommand;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterClientFrameSyncController
    {
        private readonly IShooterBattleRuntimePort _runtime;
        private readonly ShooterPresentationFacade _presentation;
        private readonly ShooterClientSnapshotApplyCoordinator _snapshotApply;
        private readonly ClientPredictionReconciliationCoordinator<ShooterPlayerCommand> _predictionReconciliation = new ClientPredictionReconciliationCoordinator<ShooterPlayerCommand>();
        private readonly ShooterClientPredictionBufferOptions _predictionBufferOptions;
        private readonly RollbackCoordinator? _rollback;
        private readonly ShooterClientPredictionRuntimeAdapter _predictionRuntimeAdapter;
        private readonly InputHistoryRingBuffer? _frameworkInputHistory;
        private readonly WorldStateHashRingBuffer? _frameworkStateHashHistory;
        private readonly ShooterClientDriftRecoveryPolicy _recoveryPolicy;
        private readonly ShooterClientRecoveryCoordinator _recovery;
        private static readonly SyncHealthEvent[] EmptyReconciliationHealthEvents = Array.Empty<SyncHealthEvent>();
        private SyncHealthEvent[] _lastReconciliationHealthEvents = EmptyReconciliationHealthEvents;
        private readonly float _fixedDeltaTime;
        private float _accumulator;

        public ShooterClientFrameSyncController(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation, int tickRate)
            : this(runtime, presentation, tickRate, null)
        {
        }

        public ShooterClientFrameSyncController(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation, int tickRate, ShooterGatewaySnapshotDecoder? decoder)
            : this(runtime, presentation, tickRate, decoder, 0ul, 240)
        {
        }

        public ShooterClientFrameSyncController(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation, int tickRate, ShooterGatewaySnapshotDecoder? decoder, ulong rollbackWorldId, int rollbackBufferFrames)
            : this(runtime, presentation, tickRate, decoder, rollbackWorldId, rollbackBufferFrames, ShooterClientDriftRecoveryPolicy.Default)
        {
        }

        public ShooterClientFrameSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            ulong rollbackWorldId,
            int rollbackBufferFrames,
            ShooterClientDriftRecoveryPolicy recoveryPolicy)
            : this(
                runtime,
                presentation,
                tickRate,
                decoder,
                rollbackWorldId,
                new ShooterClientPredictionBufferOptions(
                    ShooterClientPredictionBufferFeatures.All,
                    rollbackBufferFrames,
                    rollbackBufferFrames,
                    rollbackBufferFrames),
                recoveryPolicy)
        {
        }

        public ShooterClientFrameSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            ulong rollbackWorldId,
            ShooterClientPredictionBufferOptions predictionBufferOptions)
            : this(
                runtime,
                presentation,
                tickRate,
                decoder,
                rollbackWorldId,
                predictionBufferOptions,
                ShooterClientDriftRecoveryPolicy.Default)
        {
        }

        public ShooterClientFrameSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            ulong rollbackWorldId,
            ShooterClientPredictionBufferOptions predictionBufferOptions,
            ShooterClientDriftRecoveryPolicy recoveryPolicy)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));

            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _predictionBufferOptions = predictionBufferOptions ?? throw new ArgumentNullException(nameof(predictionBufferOptions));
            _snapshotApply = new ShooterClientSnapshotApplyCoordinator(_runtime, _presentation, decoder);
            _predictionRuntimeAdapter = new ShooterClientPredictionRuntimeAdapter(_runtime);
            if (_predictionBufferOptions.Has(ShooterClientPredictionBufferFeatures.RollbackSnapshots))
            {
                var registry = new RollbackRegistry();
                registry.Register(new ShooterPackedSnapshotRollbackProvider(_runtime, rollbackWorldId));
                _rollback = new RollbackCoordinator(
                    registry,
                    new RollbackSnapshotRingBuffer(_predictionBufferOptions.RollbackSnapshotCapacity));
            }

            if (_predictionBufferOptions.Has(ShooterClientPredictionBufferFeatures.InputHistory))
            {
                _frameworkInputHistory = new InputHistoryRingBuffer(_predictionBufferOptions.InputHistoryCapacity);
            }

            if (_predictionBufferOptions.Has(ShooterClientPredictionBufferFeatures.StateHashHistory))
            {
                _frameworkStateHashHistory = new WorldStateHashRingBuffer(_predictionBufferOptions.StateHashHistoryCapacity);
            }

            _recoveryPolicy = recoveryPolicy;
            _recovery = new ShooterClientRecoveryCoordinator(recoveryPolicy, () => _runtime.CurrentFrame);
            _fixedDeltaTime = 1f / tickRate;
        }

        public int CurrentFrame => _runtime.CurrentFrame;
        public float FixedDeltaTime => _fixedDeltaTime;
        public float AccumulatedTime => _accumulator;
        public ShooterClientPredictionBufferOptions PredictionBufferOptions => _predictionBufferOptions;
        public bool HasFrameworkInputHistory => _frameworkInputHistory != null;
        public bool HasRollbackSnapshotHistory => _rollback != null;
        public bool HasStateHashHistory => _frameworkStateHashHistory != null;
        public int PendingInputFrameCount => _predictionReconciliation.PendingInputFrameCount;
        public ShooterSnapshotApplyResult LastSnapshotApplyResult { get; private set; } = ShooterSnapshotApplyResult.Ignored;
        public ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics => _snapshotApply.Diagnostics;
        public ShooterClientImportedSnapshotEvidence LastImportedSnapshotEvidence { get; private set; } = ShooterClientImportedSnapshotEvidence.None;
        public uint LastFreshImportedStateHash { get; private set; }
        public byte[]? LastMismatchPackedPayload { get; private set; }
        public byte[]? LastFreshExportedPackedPayload { get; private set; }
        public ShooterClientReconciliationResult LastReconciliationResult { get; private set; } = ShooterClientReconciliationResult.None;
        public bool NeedsFullSnapshotResync => _recovery.NeedsFullSnapshotResync;
        public ShooterClientRecoveryState RecoveryState => _recovery.State;
        public FastReconnectPhase FastReconnectPhase => _recovery.FastReconnectPhase;
        public IReadOnlyList<SyncHealthEvent> LastFastReconnectHealthEvents => _recovery.LastFastReconnectHealthEvents;
        public IReadOnlyList<SyncHealthEvent> LastReconciliationHealthEvents => _lastReconciliationHealthEvents;
        public ShooterClientResyncReason LastResyncReason => _recovery.LastResyncReason;
        public int LastResyncClientFrame => _recovery.LastResyncClientFrame;
        public int LastResyncAuthoritativeFrame => _recovery.LastResyncAuthoritativeFrame;
        public uint LastResyncClientStateHash => _recovery.LastResyncClientStateHash;
        public uint LastResyncAuthoritativeStateHash => _recovery.LastResyncAuthoritativeStateHash;

        public bool TryRestorePredictedSnapshot(int frame)
        {
            if (_rollback == null || !_rollback.TryRestore(new FrameIndex(frame)))
            {
                return false;
            }

            _predictionReconciliation.Clear();
            _accumulator = 0f;
            PublishRuntimeSnapshot();
            return true;
        }

        public ShooterClientFrameTickResult CatchUpToFrame(int targetFrame)
        {
            if (!_runtime.IsStarted)
            {
                return ShooterClientFrameTickResult.NotStarted;
            }

            if (RecoveryState == ShooterClientRecoveryState.AwaitingFullSnapshot || RecoveryState == ShooterClientRecoveryState.ApplyingFullSnapshot)
            {
                return new ShooterClientFrameTickResult(0, _runtime.CurrentFrame, _runtime.ComputeStateHash());
            }

            if (targetFrame <= _runtime.CurrentFrame)
            {
                _accumulator = 0f;
                return new ShooterClientFrameTickResult(0, _runtime.CurrentFrame, _runtime.ComputeStateHash());
            }

            var ticks = 0;
            while (_runtime.CurrentFrame < targetFrame)
            {
                if (!StepPredictedFrame(Array.Empty<FrameworkPlayerInputCommand>()))
                {
                    break;
                }

                ticks++;
            }

            _accumulator = 0f;
            if (ticks > 0)
            {
                PublishRuntimeSnapshot();
            }

            return new ShooterClientFrameTickResult(ticks, _runtime.CurrentFrame, _runtime.ComputeStateHash());
        }

        public int SubmitLocalInput(in ShooterPlayerCommand command)
        {
            return SubmitLocalInputs(new[] { command });
        }

        public int SubmitLocalInputs(ShooterPlayerCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                return 0;
            }

            if (RecoveryState == ShooterClientRecoveryState.AwaitingFullSnapshot || RecoveryState == ShooterClientRecoveryState.ApplyingFullSnapshot)
            {
                return 0;
            }

            var frame = _runtime.CurrentFrame;
            var accepted = _runtime.SubmitInput(frame, commands);
            if (accepted > 0)
            {
                RecordPendingInput(frame, commands);
                _frameworkInputHistory?.Store(
                    new FrameIndex(frame),
                    ShooterClientPredictionRuntimeAdapter.CreateInputCommands(new FrameIndex(frame), commands));
            }

            return accepted;
        }

        public bool TryEnterCatchUp(int authoritativeFrame)
        {
            if (!_runtime.IsStarted || authoritativeFrame <= _runtime.CurrentFrame)
            {
                return false;
            }

            var frameGap = authoritativeFrame - _runtime.CurrentFrame;
            if (frameGap > _recoveryPolicy.SmallCatchUpThreshold)
            {
                MarkFullSnapshotResyncNeeded(
                    ShooterClientResyncReason.FrameTooFarBehind,
                    _runtime.CurrentFrame,
                    authoritativeFrame,
                    _runtime.ComputeStateHash(),
                    0u);
                return false;
            }

            _recovery.EnterCatchUp(authoritativeFrame);
            return true;
        }

        public void MarkGatewayInputResyncRequested(int clientFrame, int authoritativeFrame, uint clientStateHash = 0u, uint authoritativeStateHash = 0u)
        {
            MarkFullSnapshotResyncNeeded(
                ShooterClientResyncReason.ClientHashRejectedByServer,
                clientFrame,
                authoritativeFrame,
                clientStateHash,
                authoritativeStateHash);
        }

        private void RecordPendingInput(int frame, ShooterPlayerCommand[] commands)
        {
            _predictionReconciliation.RecordLocalInput(frame, commands);
        }

        public ShooterSnapshotApplyResult ApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            var replayTargetFrame = _runtime.CurrentFrame;
            var predictedHashBeforeCorrection = _runtime.IsStarted ? _runtime.ComputeStateHash() : 0u;
            var wasAwaitingFullSnapshot = NeedsFullSnapshotResync;
            var hasPredictedControlledPlayer = TryCapturePredictedControlledPlayer(out var predictedControlledPlayer);
            var snapshotApply = _snapshotApply.ApplyGatewayPush(opCode, payload);

            LastSnapshotApplyResult = snapshotApply.ApplyResult;
            LastFreshImportedStateHash = 0u;
            LastMismatchPackedPayload = null;
            LastFreshExportedPackedPayload = null;
            if (!snapshotApply.IsSnapshotPush)
            {
                LastReconciliationResult = ShooterClientReconciliationResult.None;
                _lastReconciliationHealthEvents = EmptyReconciliationHealthEvents;
                return LastSnapshotApplyResult;
            }

            if (LastSnapshotApplyResult == ShooterSnapshotApplyResult.AppliedPackedSnapshot)
            {
                var authoritativeFrame = snapshotApply.AuthoritativeFrame;
                var authoritativeStateHash = snapshotApply.AuthoritativeStateHash;
                var importedStateHash = snapshotApply.ImportedStateHash;
                if (authoritativeStateHash != 0u && authoritativeStateHash != importedStateHash)
                {
                    // Distinguish an importer/codec defect from stale state left in a predicted ECS world.
                    // This diagnostic runs only on a mismatch and never participates in reconciliation.
                    try
                    {
                        var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
                        var packedPayload = wire.Payload ?? Array.Empty<byte>();
                        LastMismatchPackedPayload = (byte[])packedPayload.Clone();
                        var packed = ShooterPackedSnapshotCodec.Deserialize(packedPayload);
                        var freshRuntime = new ShooterBattleRuntimePort();
                        if (freshRuntime.ImportPackedSnapshot(in packed))
                        {
                            LastFreshImportedStateHash = freshRuntime.ComputeStateHash();
                            LastFreshExportedPackedPayload = freshRuntime.ExportPackedSnapshotBytes(
                                packed.WorldId,
                                isFullSnapshot: true,
                                authorityOverride: true);
                        }
                    }
                    catch
                    {
                        LastFreshImportedStateHash = 0u;
                    }
                }
                LastImportedSnapshotEvidence = new ShooterClientImportedSnapshotEvidence(
                    authoritativeFrame,
                    authoritativeStateHash,
                    importedStateHash);
                var isStrongRecoverySnapshot = IsStrongRecoverySnapshot(snapshotApply.SnapshotFlags);

                _recovery.SetState(wasAwaitingFullSnapshot ? ShooterClientRecoveryState.ApplyingFullSnapshot : ShooterClientRecoveryState.Normal);
                _accumulator = 0f;
                CaptureRollbackSnapshot();
                var reconciliation = new ShooterClientReconciliationResult(
                    LastSnapshotApplyResult,
                    _predictionReconciliation.ReconcileAfterAuthoritativeSnapshot(
                        replayTargetFrame,
                        predictedHashBeforeCorrection,
                        authoritativeFrame,
                        authoritativeStateHash,
                        importedStateHash,
                        _runtime.CurrentFrame,
                        () => _runtime.CurrentFrame,
                        () => _runtime.ComputeStateHash(),
                        SubmitReplayInput,
                        StepReplayFrame));

                LastReconciliationResult = reconciliation;
                SetReconciliationHealthEvents(in reconciliation);
                PreserveReasonableControlledPrediction(hasPredictedControlledPlayer, in predictedControlledPlayer, in reconciliation);
                if (reconciliation.AuthoritativeHashMatched)
                {
                    var frameDelta = replayTargetFrame - authoritativeFrame;
                    if (Math.Abs(frameDelta) > _recoveryPolicy.ReplayThreshold)
                    {
                        MarkFullSnapshotResyncNeeded(
                            frameDelta > 0 ? ShooterClientResyncReason.FrameTooFarAhead : ShooterClientResyncReason.FrameTooFarBehind,
                            replayTargetFrame,
                            authoritativeFrame,
                            predictedHashBeforeCorrection,
                            authoritativeStateHash);
                    }
                    else
                    {
                        _recovery.ClearFullSnapshotResync();
                        _recovery.SetState(wasAwaitingFullSnapshot && isStrongRecoverySnapshot ? ShooterClientRecoveryState.Recovered : ShooterClientRecoveryState.Normal);
                    }
                }
                else
                {
                    MarkFullSnapshotResyncNeeded(
                        ShooterClientResyncReason.AuthoritativeHashMismatch,
                        replayTargetFrame,
                        authoritativeFrame,
                        predictedHashBeforeCorrection,
                        authoritativeStateHash);
                }

                _presentation.PublishReconciliation(in reconciliation);
                PublishRuntimeSnapshot();

                if (RecoveryState == ShooterClientRecoveryState.Normal && !wasAwaitingFullSnapshot)
                {
                    _recovery.HeartbeatFastReconnect(authoritativeFrame);
                }
            }
            else
            {
                LastReconciliationResult = ShooterClientReconciliationResult.None;
                _lastReconciliationHealthEvents = EmptyReconciliationHealthEvents;
                if (LastSnapshotApplyResult == ShooterSnapshotApplyResult.ImportFailed)
                {
                    MarkFullSnapshotResyncNeeded(
                        ShooterClientResyncReason.ImportFailed,
                        replayTargetFrame,
                        0,
                        predictedHashBeforeCorrection,
                        0u);
                }
            }

            return LastSnapshotApplyResult;
        }

        public ShooterClientFrameTickResult Tick(float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!_runtime.IsStarted)
            {
                return ShooterClientFrameTickResult.NotStarted;
            }

            if (RecoveryState == ShooterClientRecoveryState.AwaitingFullSnapshot || RecoveryState == ShooterClientRecoveryState.ApplyingFullSnapshot)
            {
                _accumulator = 0f;
                return new ShooterClientFrameTickResult(0, _runtime.CurrentFrame, _runtime.ComputeStateHash());
            }

            if (RecoveryState == ShooterClientRecoveryState.CatchUp)
            {
                return TickCatchUp();
            }

            if (RecoveryState == ShooterClientRecoveryState.Recovered)
            {
                _recovery.SetState(ShooterClientRecoveryState.Normal);
                _accumulator = 0f;
                return new ShooterClientFrameTickResult(0, _runtime.CurrentFrame, _runtime.ComputeStateHash());
            }

            _accumulator += deltaTime;
            var ticks = 0;
            while (_accumulator >= _fixedDeltaTime)
            {
                if (!StepPredictedFrame(Array.Empty<FrameworkPlayerInputCommand>()))
                {
                    break;
                }

                _accumulator -= _fixedDeltaTime;
                ticks++;
            }

            if (ticks > 0)
            {
                PublishRuntimeSnapshot();
            }

            return new ShooterClientFrameTickResult(ticks, _runtime.CurrentFrame, _runtime.ComputeStateHash());
        }

        private bool StepPredictedFrame(FrameworkPlayerInputCommand[] inputs)
        {
            if (!_runtime.IsStarted)
            {
                return false;
            }

            var currentFrame = _runtime.CurrentFrame;
            var nextFrame = new FrameIndex(currentFrame + 1);
            _predictionRuntimeAdapter.Submit(nextFrame, inputs);
            _frameworkInputHistory?.Store(nextFrame, inputs);
            _predictionRuntimeAdapter.Tick(_fixedDeltaTime);
            _rollback?.CaptureAndStore(nextFrame);
            _frameworkStateHashHistory?.Store(nextFrame, _predictionRuntimeAdapter.ComputeHash(nextFrame));
            return _runtime.CurrentFrame > currentFrame;
        }

        private bool StepReplayFrame()
        {
            return StepPredictedFrame(GetStoredFrameworkInputs(_runtime.CurrentFrame));
        }

        private int SubmitReplayInput(int frame, ShooterPlayerCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                return 0;
            }

            if (_frameworkInputHistory != null)
            {
                _frameworkInputHistory.Store(
                    new FrameIndex(frame),
                    ShooterClientPredictionRuntimeAdapter.CreateInputCommands(new FrameIndex(frame), commands));
                return commands.Length;
            }

            return _runtime.SubmitInput(frame, commands);
        }

        private FrameworkPlayerInputCommand[] GetStoredFrameworkInputs(int frame)
        {
            return _frameworkInputHistory != null && _frameworkInputHistory.TryGet(new FrameIndex(frame), out var inputs)
                ? inputs
                : Array.Empty<FrameworkPlayerInputCommand>();
        }

        private ShooterClientFrameTickResult TickCatchUp()
        {
            var targetFrame = _recovery.CatchUpTargetFrame;
            if (targetFrame <= _runtime.CurrentFrame)
            {
                _recovery.SetState(ShooterClientRecoveryState.Recovered);
                _accumulator = 0f;
                return new ShooterClientFrameTickResult(0, _runtime.CurrentFrame, _runtime.ComputeStateHash());
            }

            var ticks = 0;
            var maxTicks = Math.Min(_recoveryPolicy.MaxCatchUpTicksPerUpdate, targetFrame - _runtime.CurrentFrame);
            while (ticks < maxTicks && _runtime.CurrentFrame < targetFrame)
            {
                if (!StepPredictedFrame(Array.Empty<FrameworkPlayerInputCommand>()))
                {
                    break;
                }

                ticks++;
            }

            _accumulator = 0f;
            if (ticks > 0)
            {
                PublishRuntimeSnapshot();
            }

            if (_runtime.CurrentFrame >= targetFrame)
            {
                _recovery.SetState(ShooterClientRecoveryState.Normal);
            }

            return new ShooterClientFrameTickResult(ticks, _runtime.CurrentFrame, _runtime.ComputeStateHash());
        }

        private void CaptureRollbackSnapshot()
        {
            if (_runtime.IsStarted && _rollback != null)
            {
                _rollback.CaptureAndStore(new FrameIndex(_runtime.CurrentFrame));
            }
        }

        private void MarkFullSnapshotResyncNeeded(
            ShooterClientResyncReason reason,
            int clientFrame,
            int authoritativeFrame,
            uint clientStateHash,
            uint authoritativeStateHash)
        {
            _recovery.MarkFullSnapshotResyncNeeded(
                reason,
                clientFrame,
                authoritativeFrame,
                clientStateHash,
                authoritativeStateHash);
        }

        private static bool IsStrongRecoverySnapshot(uint snapshotFlags)
        {
            return (snapshotFlags & ShooterPackedSnapshotFlags.Full) != 0u
                || (snapshotFlags & ShooterPackedSnapshotFlags.AuthorityOverride) != 0u;
        }

        private bool TryCapturePredictedControlledPlayer(out ShooterSveltoPlayerComponent player)
        {
            var controlledPlayerId = _presentation.ControlledPlayerId;
            if (controlledPlayerId <= 0 || !_runtime.IsStarted)
            {
                player = default;
                return false;
            }

            return _runtime.TryGetPlayer(controlledPlayerId, out player);
        }

        private void PreserveReasonableControlledPrediction(
            bool hasPredictedControlledPlayer,
            in ShooterSveltoPlayerComponent predictedControlledPlayer,
            in ShooterClientReconciliationResult reconciliation)
        {
            if (!hasPredictedControlledPlayer || predictedControlledPlayer.PlayerId <= 0)
            {
                return;
            }

            if (!_runtime.TryGetPlayer(predictedControlledPlayer.PlayerId, out var reconciledPlayer))
            {
                return;
            }

            var dx = predictedControlledPlayer.X - reconciledPlayer.X;
            var dy = predictedControlledPlayer.Y - reconciledPlayer.Y;
            var distanceSquared = dx * dx + dy * dy;
            var maxReasonableDistance = GetMaxReasonableControlledPredictionDistance(in reconciliation);
            if (distanceSquared > maxReasonableDistance * maxReasonableDistance)
            {
                return;
            }

            if (distanceSquared <= 0.000001f)
            {
                return;
            }

            reconciledPlayer.X = predictedControlledPlayer.X;
            reconciledPlayer.Y = predictedControlledPlayer.Y;
            reconciledPlayer.AimX = predictedControlledPlayer.AimX;
            reconciledPlayer.AimY = predictedControlledPlayer.AimY;
            _runtime.SetPlayer(in reconciledPlayer);
        }

        private float GetMaxReasonableControlledPredictionDistance(in ShooterClientReconciliationResult reconciliation)
        {
            var replayFrames = Math.Max(1, Math.Abs(reconciliation.PredictedFrameBeforeCorrection - reconciliation.AuthoritativeFrame));
            var replayDistance = ShooterBattleTuning.PlayerSpeed * _fixedDeltaTime * replayFrames;
            return replayDistance + ShooterBattleTuning.PlayerSpeed * _fixedDeltaTime + 0.05f;
        }

        private void PublishRuntimeSnapshot()
        {
            var snapshot = _runtime.GetSnapshotTransient();
            _presentation.ApplyLocalPredictionSnapshot(in snapshot);
        }

        private void SetReconciliationHealthEvents(in ShooterClientReconciliationResult reconciliation)
        {
            if (reconciliation.ReplayTicks <= 0)
            {
                _lastReconciliationHealthEvents = EmptyReconciliationHealthEvents;
                return;
            }

            var rollback = reconciliation.AuthoritativeHashMatched
                ? SyncHealthEvent.Warning(
                    SyncHealthEventKind.RollbackStarted,
                    reconciliation.AuthoritativeFrame,
                    reconciliation.ReplayTicks)
                : SyncHealthEvent.Error(
                    SyncHealthEventKind.RollbackStarted,
                    reconciliation.AuthoritativeFrame,
                    reconciliation.ReplayTicks);
            var replay = reconciliation.AuthoritativeHashMatched
                ? SyncHealthEvent.Info(
                    SyncHealthEventKind.ReplayCompleted,
                    reconciliation.FinalFrame,
                    reconciliation.ReplayTicks)
                : SyncHealthEvent.Error(
                    SyncHealthEventKind.ReplayCompleted,
                    reconciliation.FinalFrame,
                    reconciliation.ReplayTicks);
            _lastReconciliationHealthEvents = new[] { rollback, replay };
        }
    }

    public enum ShooterClientResyncReason
    {
        None = 0,
        ImportFailed = 1,
        AuthoritativeHashMismatch = 2,
        ClientHashRejectedByServer = 3,
        FrameTooFarBehind = 4,
        FrameTooFarAhead = 5,
        SnapshotTimeout = 6,
        WorldMismatch = 7
    }

    public readonly struct ShooterClientImportedSnapshotEvidence
    {
        public static readonly ShooterClientImportedSnapshotEvidence None = new ShooterClientImportedSnapshotEvidence(0, 0u, 0u);
        public readonly int Frame;
        public readonly uint AuthoritativeStateHash;
        public readonly uint ImportedStateHash;

        public ShooterClientImportedSnapshotEvidence(int frame, uint authoritativeStateHash, uint importedStateHash)
        {
            Frame = frame;
            AuthoritativeStateHash = authoritativeStateHash;
            ImportedStateHash = importedStateHash;
        }
    }

    public readonly struct ShooterClientFrameTickResult
    {
        public static readonly ShooterClientFrameTickResult NotStarted = new ShooterClientFrameTickResult(0, 0, 0u);
        public readonly int Ticks;
        public readonly int Frame;
        public readonly uint StateHash;
        public ShooterClientFrameTickResult(int ticks, int frame, uint stateHash)
        {
            Ticks = ticks;
            Frame = frame;
            StateHash = stateHash;
        }
    }

    public readonly struct ShooterClientReconciliationResult
    {
        public static readonly ShooterClientReconciliationResult None = new ShooterClientReconciliationResult(ShooterSnapshotApplyResult.Ignored, 0, 0u, 0, 0u, 0u, false, 0, 0, 0u, 0, 0, 0);
        public readonly ShooterSnapshotApplyResult ApplyResult;
        public readonly int PredictedFrameBeforeCorrection;
        public readonly uint PredictedHashBeforeCorrection;
        public readonly int AuthoritativeFrame;
        public readonly uint AuthoritativeStateHash;
        public readonly uint ImportedStateHash;
        public readonly bool AuthoritativeHashMatched;
        public readonly int ReplayTicks;
        public readonly int FinalFrame;
        public readonly uint FinalStateHash;
        public readonly int PendingInputFramesBeforeCorrection;
        public readonly int PendingInputFramesAfterTrim;
        public readonly int PendingInputFramesAfterReplay;

        public ShooterClientReconciliationResult(
            ShooterSnapshotApplyResult applyResult,
            int predictedFrameBeforeCorrection,
            uint predictedHashBeforeCorrection,
            int authoritativeFrame,
            uint authoritativeStateHash,
            uint importedStateHash,
            bool authoritativeHashMatched,
            int replayTicks,
            int finalFrame,
            uint finalStateHash,
            int pendingInputFramesBeforeCorrection,
            int pendingInputFramesAfterTrim,
            int pendingInputFramesAfterReplay)
        {
            ApplyResult = applyResult;
            PredictedFrameBeforeCorrection = predictedFrameBeforeCorrection;
            PredictedHashBeforeCorrection = predictedHashBeforeCorrection;
            AuthoritativeFrame = authoritativeFrame;
            AuthoritativeStateHash = authoritativeStateHash;
            ImportedStateHash = importedStateHash;
            AuthoritativeHashMatched = authoritativeHashMatched;
            ReplayTicks = replayTicks;
            FinalFrame = finalFrame;
            FinalStateHash = finalStateHash;
            PendingInputFramesBeforeCorrection = pendingInputFramesBeforeCorrection;
            PendingInputFramesAfterTrim = pendingInputFramesAfterTrim;
            PendingInputFramesAfterReplay = pendingInputFramesAfterReplay;
        }

        public ShooterClientReconciliationResult(ShooterSnapshotApplyResult applyResult, in ClientPredictionReconciliationResult result)
            : this(
                applyResult,
                result.PredictedFrameBeforeCorrection,
                result.PredictedHashBeforeCorrection,
                result.AuthoritativeFrame,
                result.AuthoritativeStateHash,
                result.ImportedStateHash,
                result.AuthoritativeHashMatched,
                result.ReplayTicks,
                result.FinalFrame,
                result.FinalStateHash,
                result.PendingInputFramesBeforeCorrection,
                result.PendingInputFramesAfterTrim,
                result.PendingInputFramesAfterReplay)
        {
        }
    }
}
