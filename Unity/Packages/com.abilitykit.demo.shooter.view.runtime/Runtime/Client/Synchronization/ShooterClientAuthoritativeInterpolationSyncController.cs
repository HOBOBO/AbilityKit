#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 客户端控制器。
    /// 本地玩家使用权威 pose、输入确认和有界未确认输入重放；远端 actor 只进入服务器时间线插值，
    /// 不导入本地模拟，也不触发整世界回滚。
    /// </summary>
    public sealed class ShooterClientAuthoritativeInterpolationSyncController : IShooterClientSyncController, IShooterClientFrameSyncCapability, IShooterClientInputCapability, IInterpolationDiagnosticsProvider
    {
        private const int MaxPendingInputs = 128;
        private const int MaxReplayFrames = 120;
        private const float PositionQuantizationScale = 1000f;
        private const float SmallErrorTolerance = 0.05f;
        private const float MaxCorrectionPerSnapshot = 0.25f;

        private readonly IShooterBattleRuntimePort _runtime;
        private readonly ShooterClientSyncCore _core;
        private readonly ShooterPresentationFacade _presentation;
        private readonly ShooterGatewaySnapshotDecoder _decoder;
        private readonly RemoteInterpolationPlayback<ShooterRemoteSnapshotSample> _playback;
        private readonly ShooterSnapshotStream _pureStatePlayback = new ShooterSnapshotStream(32);
        private readonly ShooterRemoteSnapshotProjector _projector = new ShooterRemoteSnapshotProjector();
        private readonly NetworkSyncModel _syncModel;
        private readonly float _fixedDeltaTime;
        private readonly object _pendingInputLock = new object();
        private readonly List<PendingLocalInput> _pendingInputs = new List<PendingLocalInput>(MaxPendingInputs);
        private readonly ShooterPlayerCommand[] _replayCommands = new ShooterPlayerCommand[MaxPendingInputs];
        private readonly int[] _replayFrames = new int[MaxPendingInputs];
        private readonly CompositeReadOnlyList<ShooterViewEntityChange> _compositeEntities = new CompositeReadOnlyList<ShooterViewEntityChange>();
        private readonly CompositeReadOnlyList<ShooterViewEntityKey> _compositeRemoved = new CompositeReadOnlyList<ShooterViewEntityKey>();
        private readonly CompositeReadOnlyList<ShooterViewTransformComponentChange> _compositeTransforms = new CompositeReadOnlyList<ShooterViewTransformComponentChange>();
        private readonly CompositeReadOnlyList<ShooterViewHealthComponentChange> _compositeHealth = new CompositeReadOnlyList<ShooterViewHealthComponentChange>();
        private readonly CompositeReadOnlyList<ShooterViewScoreComponentChange> _compositeScore = new CompositeReadOnlyList<ShooterViewScoreComponentChange>();
        private readonly CompositeReadOnlyList<ShooterViewProjectileLifetimeComponentChange> _compositeProjectileLifetime = new CompositeReadOnlyList<ShooterViewProjectileLifetimeComponentChange>();
        private readonly CompositeReadOnlyList<ShooterEventSnapshot> _compositeEvents = new CompositeReadOnlyList<ShooterEventSnapshot>();
        private readonly HashSet<ShooterViewEntityKey> _suppressedPureStateTransforms = new HashSet<ShooterViewEntityKey>();
        private readonly ReusableFilteredTransformList _filteredPureStateTransforms = new ReusableFilteredTransformList();
        private readonly PureStateDiscreteBatchAccumulator _pureStateDiscreteA = new PureStateDiscreteBatchAccumulator();
        private readonly PureStateDiscreteBatchAccumulator _pureStateDiscreteB = new PureStateDiscreteBatchAccumulator();
        private ShooterPlayerCommand _lastPredictedCommand;
        private bool _usesPureStatePresentation;
        private PureStateDiscreteBatchAccumulator? _pendingPureStateDiscrete;
        private PureStateDiscreteBatchAccumulator? _publishedPureStateDiscrete;
        private ShooterStateSyncPredictionState _predictionState = ShooterStateSyncPredictionState.Empty;
        private SyncReconciliationReport _localReconciliationReport = SyncReconciliationReport.None;
        private ulong _authorityWorldId;
        private int _lastAuthorityFrame = -1;
        private int _lastGatewaySnapshotFrame = -1;
        private long _nextSyntheticSubmissionId;

        public ShooterClientAuthoritativeInterpolationSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway)
            : this(runtime, presentation, tickRate, decoder, gateway, InterpolationConfig.Default)
        {
        }

        public ShooterClientAuthoritativeInterpolationSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            InterpolationConfig config)
            : this(runtime, presentation, tickRate, decoder, gateway, config, NetworkSyncModel.AuthoritativeInterpolation)
        {
        }

        public ShooterClientAuthoritativeInterpolationSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            InterpolationConfig config,
            NetworkSyncModel syncModel,
            ShooterClientPredictionBufferOptions? predictionBufferOptions = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _core = new ShooterClientSyncCore(
                _runtime,
                presentation,
                tickRate,
                decoder,
                gateway,
                predictionBufferOptions ?? ShooterClientPredictionBufferOptions.Disabled);
            _core.FrameSync.ComputeTickResultStateHash =
                predictionBufferOptions != null
                    && !ReferenceEquals(predictionBufferOptions, ShooterClientPredictionBufferOptions.Disabled);
            _decoder = decoder ?? new ShooterGatewaySnapshotDecoder();
            _playback = new RemoteInterpolationPlayback<ShooterRemoteSnapshotSample>(config);
            _syncModel = syncModel;
            _fixedDeltaTime = 1f / Math.Max(1, tickRate);
            _pureStatePlayback.PlaybackFramesPerSecond = Math.Max(1, tickRate);
        }

        public NetworkSyncModel SyncModel => _syncModel;

        public bool IsStarted => _core.IsStarted;

        public int CurrentFrame => _core.CurrentFrame;

        public int GatewayInputFrame => _lastGatewaySnapshotFrame >= 0 ? _lastGatewaySnapshotFrame : CurrentFrame;

        public ShooterClientFrameSyncController FrameSync => _core.FrameSync;

        public ShooterClientInputCoordinator InputCoordinator => _core.InputCoordinator;

        public ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics => _core.FrameworkSnapshotPipelineDiagnostics;

        public ShooterClientReconciliationResult LastReconciliationResult => _core.LastReconciliationResult;

        public bool NeedsFullSnapshotResync => _core.NeedsFullSnapshotResync;

        public ShooterClientRecoveryState RecoveryState => _core.RecoveryState;

        public AbilityKit.Network.Runtime.Sync.FastReconnectPhase FastReconnectPhase => _core.FastReconnectPhase;

        public IReadOnlyList<SyncHealthEvent> LastFastReconnectHealthEvents => _core.LastFastReconnectHealthEvents;

        public ShooterClientResyncReason LastResyncReason => _core.LastResyncReason;

        public int LastResyncClientFrame => _core.LastResyncClientFrame;

        public int LastResyncAuthoritativeFrame => _core.LastResyncAuthoritativeFrame;

        public uint LastResyncClientStateHash => _core.LastResyncClientStateHash;

        public uint LastResyncAuthoritativeStateHash => _core.LastResyncAuthoritativeStateHash;

        public bool HasGateway => _core.HasGateway;

        /// <summary>当前为插值缓冲的远端权威快照数量。</summary>
        public int BufferedRemoteSnapshotCount => _playback.BufferedSampleCount;

        /// <summary>当前延迟远端播放时间，单位为时间线 tick。</summary>
        public long RemotePlaybackTicks => _playback.PlaybackTicks;

        /// <summary>当前本地估算的权威服务器时间，单位为时间线 tick。</summary>
        public long EstimatedServerTicks => _playback.EstimatedServerTicks;

        /// <summary>是否已经向表现层发布过至少一帧远端插值结果。</summary>
        public bool HasPublishedRemoteFrame => _playback.HasPublished;

        /// <summary>
        /// 最近一次发布尝试是否发现延迟播放时间已经超过最新缓冲快照
        /// <see cref="InterpolationConfig.MaxExtrapolationTicks"/> 以上。
        /// 表示远端缓冲已经饥饿（例如快照停止到达），播放会保持最后一个权威姿态，而不是继续外推。
        /// </summary>
        public bool IsRemotePlaybackStarved => _playback.IsStarved;

        public ShooterStateSyncPredictionState PredictionState => _predictionState;

        public int PendingGatewayInputCount
        {
            get
            {
                lock (_pendingInputLock)
                {
                    return _pendingInputs.Count;
                }
            }
        }

        public bool StartGame(in ShooterStartGamePayload startGame)
        {
            ResetAuthoritativeState();
            return _core.StartGame(in startGame);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            // 与 PredictRollback 控制器一致：经 InputBuilder.CreateCommand 做归一化，
            // 避免同一输入因同步模型不同产生不同的命令（原始向量未归一）。
            var command = ShooterClientInputBuilder.CreateCommand(playerId, moveX, moveY, aimX, aimY, fire);
            return SubmitLocalInput(in command);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(in ShooterPlayerCommand command)
        {
            var result = _core.SubmitLocalInput(in command);
            if (command.PlayerId > 0 && result.AcceptedInputs > 0)
            {
                _lastPredictedCommand = command;
                RefreshPredictedPose(command.PlayerId, CurrentFrame, EstimatedServerTicks);
            }

            return result;
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var local = SubmitLocalInput(in command).WithRequestedFrame(context.Frame);
            return await SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            RecordPendingInput(in local);
            MarkGatewayStarted(in local);
            try
            {
                var result = await _core.SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken).ConfigureAwait(false);
                BindGatewayResult(in result.Local, in result.Remote);
                return result;
            }
            catch
            {
                RemovePendingInput(in local);
                throw;
            }
        }

        public ShooterClientFrameTickResult Tick(float deltaTime)
        {
            var result = _core.Tick(deltaTime);
            RefreshPredictedPose(_lastPredictedCommand.PlayerId, result.Frame, EstimatedServerTicks);
            _playback.Advance(deltaTime);
            PublishInterpolatedRemoteFrame();
            PublishPureStatePresentationFrame(deltaTime);
            return result;
        }

        public ShooterClientFrameTickResult CatchUpToFrame(int targetFrame)
        {
            return _core.CatchUpToFrame(targetFrame);
        }

        public bool TryEnterCatchUp(int authoritativeFrame)
        {
            return _core.TryEnterCatchUp(authoritativeFrame);
        }

        /// <summary>
        /// 应用权威快照。本地玩家只做局部 pose 纠偏；远端玩家进入延迟插值缓冲。
        /// </summary>
        public ShooterSnapshotApplyResult ApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            if (!_decoder.IsSnapshotPush(opCode))
            {
                return ShooterSnapshotApplyResult.Ignored;
            }

            var snapshot = _decoder.Decode(payload);
            return BufferRemoteSnapshot(in snapshot);
        }

        /// <summary>
        /// 为延迟插值缓冲一个已经解码的网关快照。
        /// </summary>
        public ShooterSnapshotApplyResult BufferRemoteSnapshot(in ShooterGatewaySnapshot snapshot)
        {
            if (snapshot.PureStateSnapshot.HasValue)
            {
                var pureStateResult = _presentation.ApplyPureStateGatewaySnapshot(in snapshot);
                if (pureStateResult == ShooterPureStateSnapshotApplyResult.AppliedFullBaseline
                    || pureStateResult == ShooterPureStateSnapshotApplyResult.AppliedDelta)
                {
                    _usesPureStatePresentation = true;
                    _core.FrameSync.PublishControlledPlayerPredictionOnly = true;
                    _playback.Reset();
                    var pureState = snapshot.PureStateSnapshot!.Value;
                    var presentationBatch = _presentation.ViewModel.Current;
                    var pureStateSettings = pureState.Settings;
                    ObservePureStatePresentationBatch(in presentationBatch, in pureStateSettings);
                    ObserveGatewaySnapshotFrame(snapshot.Frame);
                    ReconcileControlledPlayer(in snapshot, forceAuthorityReset: false);
                }

                return ShooterSnapshotApplyResults.FromPureStateResult(pureStateResult);
            }

            var worldChanged = snapshot.WorldId != 0
                && _authorityWorldId != 0
                && snapshot.WorldId != _authorityWorldId;
            if (worldChanged)
            {
                ResetAuthoritativeState();
            }

            var sample = new ShooterRemoteSnapshotSample(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.ServerTicks,
                snapshot.Actors,
                snapshot.PackedSnapshot,
                excludedActorId: _presentation.ControlledPlayerId);

            if (!_playback.Observe(sample))
            {
                return ShooterSnapshotApplyResult.IgnoredStaleSnapshot;
            }

            _usesPureStatePresentation = false;
            _core.FrameSync.PublishControlledPlayerPredictionOnly = false;
            _pureStatePlayback.Reset();
            ResetPureStateDiscreteBatches();
            ObserveGatewaySnapshotFrame(snapshot.Frame);
            ReconcileControlledPlayer(in snapshot, worldChanged);
            ObserveAuthoritativeProjectileActions(in snapshot);
            return ShooterSnapshotApplyResult.AppliedActorSnapshot;
        }

        private void MarkGatewayStarted(in ShooterClientInputSubmitResult local)
        {
            lock (_pendingInputLock)
            {
                for (var i = _pendingInputs.Count - 1; i >= 0; i--)
                {
                    if (local.SubmissionId > 0 && _pendingInputs[i].SubmissionId == local.SubmissionId)
                    {
                        _pendingInputs[i].GatewayStarted = true;
                        return;
                    }
                }
            }
        }

        private void ResetAuthoritativeState()
        {
            lock (_pendingInputLock)
            {
                _pendingInputs.Clear();
            }

            _authorityWorldId = 0;
            _lastAuthorityFrame = -1;
            _lastGatewaySnapshotFrame = -1;
            _localReconciliationReport = SyncReconciliationReport.None;
            _predictionState = ShooterStateSyncPredictionState.Empty;
            _lastPredictedCommand = default;
            _usesPureStatePresentation = false;
            _core.FrameSync.PublishControlledPlayerPredictionOnly = false;
            _playback.Reset();
            _pureStatePlayback.Reset();
            ResetPureStateDiscreteBatches();
        }

        private void ObservePureStatePresentationBatch(
            in ShooterSnapshotViewBatch batch,
            in ShooterPureStateSyncSettings settings)
        {
            if (batch.IsFullSnapshot)
            {
                _suppressedPureStateTransforms.Clear();
            }

            for (var i = 0; i < batch.EntityChanges.Count; i++)
            {
                var entity = batch.EntityChanges[i];
                if (entity.Alive)
                {
                    _suppressedPureStateTransforms.Remove(entity.Key);
                }
            }

            for (var i = 0; i < batch.RemovedEntities.Count; i++)
            {
                _suppressedPureStateTransforms.Add(batch.RemovedEntities[i]);
            }

            if (_pendingPureStateDiscrete == null)
            {
                _pendingPureStateDiscrete = ReferenceEquals(_publishedPureStateDiscrete, _pureStateDiscreteA)
                    ? _pureStateDiscreteB
                    : _pureStateDiscreteA;
                _pendingPureStateDiscrete.Reset(in batch);
            }
            else
            {
                _pendingPureStateDiscrete.Append(in batch);
            }

            _pureStatePlayback.InterpolationDelayFrames = Math.Max(
                1f,
                Math.Min(
                    Math.Max(1, settings.InterpolationDelayFrames),
                    Math.Max(1, settings.DeltaIntervalFrames)));

            var removed = ShooterSnapshotViewModelMapper.RentPooledRemovedEntities(batch.RemovedEntities.Count);
            for (var i = 0; i < batch.RemovedEntities.Count; i++)
            {
                removed.Add(batch.RemovedEntities[i]);
            }

            var transforms = ShooterSnapshotViewModelMapper.RentPooledTransformChanges(batch.TransformChanges.Count);
            for (var i = 0; i < batch.TransformChanges.Count; i++)
            {
                var transform = batch.TransformChanges[i];
                if (!transform.IsPredictedLocal)
                {
                    transforms.Add(transform);
                }
            }

            var playbackBatch = new ShooterSnapshotViewBatch(
                batch.WorldId,
                batch.Frame,
                batch.Sequence,
                batch.SnapshotKind,
                batch.Source,
                Array.Empty<ShooterViewEntityChange>(),
                removed,
                transforms,
                Array.Empty<ShooterViewHealthComponentChange>(),
                Array.Empty<ShooterViewScoreComponentChange>(),
                Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
                Array.Empty<ShooterEventSnapshot>());
            _pureStatePlayback.Publish(in playbackBatch);
        }

        private void PublishPureStatePresentationFrame(float deltaTime)
        {
            if (!_usesPureStatePresentation ||
                !_pureStatePlayback.TryAdvancePlaybackTransient(deltaTime, out var remotePlayback))
            {
                return;
            }

            var local = _presentation.ViewModel.Current;
            var pendingDiscrete = _pendingPureStateDiscrete;
            var remoteDiscrete = pendingDiscrete != null
                ? pendingDiscrete.CreateBatch()
                : ShooterSnapshotViewBatch.Empty;
            var remoteTransforms = remotePlayback.TransformChanges;
            if (_suppressedPureStateTransforms.Count > 0)
            {
                _filteredPureStateTransforms.Reset(remoteTransforms, _suppressedPureStateTransforms);
                remoteTransforms = _filteredPureStateTransforms;
            }

            _compositeEntities.Reset(remoteDiscrete.EntityChanges, local.EntityChanges);
            _compositeRemoved.Reset(remoteDiscrete.RemovedEntities, local.RemovedEntities);
            _compositeTransforms.Reset(remoteTransforms, local.TransformChanges);
            _compositeHealth.Reset(remoteDiscrete.HealthChanges, local.HealthChanges);
            _compositeScore.Reset(remoteDiscrete.ScoreChanges, local.ScoreChanges);
            _compositeProjectileLifetime.Reset(
                remoteDiscrete.ProjectileLifetimeChanges,
                local.ProjectileLifetimeChanges);
            _compositeEvents.Reset(remoteDiscrete.Events, local.Events);

            var hasDiscrete = pendingDiscrete != null;
            var composed = new ShooterSnapshotViewBatch(
                hasDiscrete ? remoteDiscrete.WorldId : remotePlayback.WorldId,
                Math.Max(remotePlayback.Frame, local.Frame),
                Math.Max(remotePlayback.Sequence, local.Sequence),
                hasDiscrete ? remoteDiscrete.SnapshotKind : ShooterViewSnapshotKind.Delta,
                hasDiscrete ? remoteDiscrete.Source : ShooterViewBatchSource.LocalPrediction,
                _compositeEntities,
                _compositeRemoved,
                _compositeTransforms,
                _compositeHealth,
                _compositeScore,
                _compositeProjectileLifetime,
                _compositeEvents,
                remotePlayback.SampleFrame);
            _presentation.SetRenderBatch(in composed);
            if (pendingDiscrete != null)
            {
                _publishedPureStateDiscrete = pendingDiscrete;
                _pendingPureStateDiscrete = null;
            }
        }

        private void ResetPureStateDiscreteBatches()
        {
            _pureStateDiscreteA.Clear();
            _pureStateDiscreteB.Clear();
            _suppressedPureStateTransforms.Clear();
            _filteredPureStateTransforms.Clear();
            _pendingPureStateDiscrete = null;
            _publishedPureStateDiscrete = null;
        }

        private void ObserveGatewaySnapshotFrame(int frame)
        {
            if (frame >= 0 && frame > _lastGatewaySnapshotFrame)
            {
                _lastGatewaySnapshotFrame = frame;
            }
        }

        private void RecordPendingInput(in ShooterClientInputSubmitResult result)
        {
            if (result.AcceptedInputs <= 0 || result.Packet.Command.PlayerId <= 0)
            {
                return;
            }

            lock (_pendingInputLock)
            {
                var submissionId = result.SubmissionId > 0
                    ? result.SubmissionId
                    : Interlocked.Increment(ref _nextSyntheticSubmissionId);
                for (var i = 0; i < _pendingInputs.Count; i++)
                {
                    if (_pendingInputs[i].SubmissionId == submissionId)
                    {
                        _pendingInputs[i].RequestedFrame = result.RequestedFrame;
                        return;
                    }
                }

                if (_pendingInputs.Count >= MaxPendingInputs)
                {
                    _pendingInputs.RemoveAt(0);
                }

                _pendingInputs.Add(new PendingLocalInput(
                    submissionId,
                    result.RequestedFrame,
                    in result.Packet.Command));
            }
        }

        private void BindGatewayResult(
            in ShooterClientInputSubmitResult local,
            in ShooterGatewayBattleInputResult remote)
        {
            lock (_pendingInputLock)
            {
                for (var i = _pendingInputs.Count - 1; i >= 0; i--)
                {
                    var pending = _pendingInputs[i];
                    if (local.SubmissionId > 0 && pending.SubmissionId != local.SubmissionId)
                    {
                        continue;
                    }

                    if (local.SubmissionId <= 0
                        && (pending.Command.PlayerId != local.Packet.Command.PlayerId
                            || pending.RequestedFrame != local.RequestedFrame))
                    {
                        continue;
                    }

                    pending.GatewayCompleted = true;
                    pending.CommandSequence = remote.CommandSequence;
                    pending.AcceptedFrame = remote.AcceptedFrame;
                    if (!remote.Success || remote.ShouldResync)
                    {
                        _pendingInputs.RemoveAt(i);
                    }

                    return;
                }
            }
        }

        private void RemovePendingInput(in ShooterClientInputSubmitResult local)
        {
            lock (_pendingInputLock)
            {
                for (var i = _pendingInputs.Count - 1; i >= 0; i--)
                {
                    var pending = _pendingInputs[i];
                    if ((local.SubmissionId > 0 && pending.SubmissionId == local.SubmissionId)
                        || (local.SubmissionId <= 0
                            && pending.Command.PlayerId == local.Packet.Command.PlayerId
                            && pending.RequestedFrame == local.RequestedFrame))
                    {
                        _pendingInputs.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        private void ReconcileControlledPlayer(
            in ShooterGatewaySnapshot snapshot,
            bool forceAuthorityReset)
        {
            var playerId = _presentation.ControlledPlayerId;
            if (playerId <= 0 || snapshot.Frame < 0 || !_runtime.TryGetPlayer(playerId, out var current))
            {
                return;
            }

            var worldChanged = forceAuthorityReset
                || (snapshot.WorldId != 0
                    && _authorityWorldId != 0
                    && snapshot.WorldId != _authorityWorldId);
            if (worldChanged && !forceAuthorityReset)
            {
                ResetAuthoritativeState();
            }

            if (!worldChanged && snapshot.Frame <= _lastAuthorityFrame)
            {
                return;
            }

            if (!TryExtractAuthoritativePlayer(in snapshot, playerId, in current, out var target))
            {
                return;
            }

            var firstAuthority = _lastAuthorityFrame < 0;
            _authorityWorldId = snapshot.WorldId != 0 ? snapshot.WorldId : _authorityWorldId;
            _lastAuthorityFrame = snapshot.Frame;
            var acknowledgedSequence = ResolveAcknowledgedSequence(in snapshot, playerId);
            var replayCount = ReplayPendingInputs(
                ref target,
                playerId,
                snapshot.Frame,
                acknowledgedSequence);

            var deltaX = target.X - current.X;
            var deltaY = target.Y - current.Y;
            var error = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var forceSnap = firstAuthority
                || worldChanged
                || (snapshot.PackedSnapshot?.SnapshotFlags & ShooterPackedSnapshotFlags.AuthorityOverride) != 0;
            if (!forceSnap && error <= SmallErrorTolerance)
            {
                target.X = current.X;
                target.Y = current.Y;
            }
            else if (!forceSnap)
            {
                var scale = Math.Min(1f, MaxCorrectionPerSnapshot / error);
                target.X = current.X + deltaX * scale;
                target.Y = current.Y + deltaY * scale;
            }

            if (PlayersEqual(in current, in target))
            {
                return;
            }

            _runtime.SetPlayer(in target);
            RefreshPredictedPose(playerId, CurrentFrame, snapshot.ServerTicks);
            _localReconciliationReport = new SyncReconciliationReport(
                SyncReconciliationReason.LocalAuthorityCorrection,
                SyncRecoveryState.Normal,
                needsFullSnapshot: false,
                clientFrame: CurrentFrame,
                authoritativeFrame: snapshot.Frame,
                clientStateHash: 0u,
                authoritativeStateHash: ResolveAuthoritativeStateHash(in snapshot),
                replayTicks: replayCount);
        }

        private int ReplayPendingInputs(
            ref ShooterSveltoPlayerComponent player,
            int playerId,
            int authorityFrame,
            ulong acknowledgedSequence)
        {
            lock (_pendingInputLock)
            {
                for (var i = _pendingInputs.Count - 1; i >= 0; i--)
                {
                    var pending = _pendingInputs[i];
                    var explicitlyAcknowledged = acknowledgedSequence > 0
                        && pending.CommandSequence > 0
                        && pending.CommandSequence <= acknowledgedSequence;
                    var legacyFrameAcknowledged = acknowledgedSequence == 0
                        && pending.GatewayCompleted
                        && pending.AcceptedFrame <= authorityFrame;
                    if (explicitlyAcknowledged || legacyFrameAcknowledged)
                    {
                        _pendingInputs.RemoveAt(i);
                    }
                }

                var uniqueCount = 0;
                for (var i = 0; i < _pendingInputs.Count; i++)
                {
                    var pending = _pendingInputs[i];
                    if (pending.Command.PlayerId != playerId)
                    {
                        continue;
                    }

                    var effectiveFrame = pending.GatewayCompleted
                        ? pending.AcceptedFrame
                        : pending.RequestedFrame;
                    var insertIndex = 0;
                    while (insertIndex < uniqueCount && _replayFrames[insertIndex] < effectiveFrame)
                    {
                        insertIndex++;
                    }

                    if (insertIndex < uniqueCount && _replayFrames[insertIndex] == effectiveFrame)
                    {
                        // Multiple render samples can target one simulation frame. The server
                        // consumes the latest command state once for that frame, so reconciliation
                        // must replace rather than replay every sample as a separate simulation tick.
                        _replayCommands[insertIndex] = pending.Command;
                        continue;
                    }

                    if (uniqueCount >= MaxPendingInputs)
                    {
                        continue;
                    }

                    for (var shift = uniqueCount; shift > insertIndex; shift--)
                    {
                        _replayFrames[shift] = _replayFrames[shift - 1];
                        _replayCommands[shift] = _replayCommands[shift - 1];
                    }

                    _replayFrames[insertIndex] = effectiveFrame;
                    _replayCommands[insertIndex] = pending.Command;
                    uniqueCount++;
                }

                var replayed = Math.Min(uniqueCount, MaxReplayFrames);
                for (var i = 0; i < replayed; i++)
                {
                    var command = _replayCommands[i];
                    ApplyPredictedPoseInput(ref player, in command);
                }

                return replayed;
            }
        }

        private void ApplyPredictedPoseInput(
            ref ShooterSveltoPlayerComponent player,
            in ShooterPlayerCommand source)
        {
            var moveX = source.MoveX;
            var moveY = source.MoveY;
            if (ShooterBattleMath.Normalize(ref moveX, ref moveY) > 0f)
            {
                player.X += moveX * ShooterBattleTuning.PlayerSpeed * _fixedDeltaTime;
                player.Y += moveY * ShooterBattleTuning.PlayerSpeed * _fixedDeltaTime;
            }

            var aimX = source.AimX;
            var aimY = source.AimY;
            if (ShooterBattleMath.Normalize(ref aimX, ref aimY) > 0f)
            {
                player.AimX = aimX;
                player.AimY = aimY;
            }
        }

        private static bool TryExtractAuthoritativePlayer(
            in ShooterGatewaySnapshot snapshot,
            int playerId,
            in ShooterSveltoPlayerComponent current,
            out ShooterSveltoPlayerComponent player)
        {
            player = current;
            if (snapshot.PackedSnapshot.HasValue)
            {
                var found = false;
                var chunks = snapshot.PackedSnapshot.Value.ComponentChunks;
                for (var chunkIndex = 0; chunkIndex < SafeLength(chunks); chunkIndex++)
                {
                    var chunk = chunks[chunkIndex];
                    if (chunk.EntityKind != ShooterPackedEntityKinds.Player)
                    {
                        continue;
                    }

                    var count = Math.Min(chunk.Count, SafeLength(chunk.EntityIds));
                    for (var i = 0; i < count; i++)
                    {
                        if (chunk.EntityIds[i] != playerId)
                        {
                            continue;
                        }

                        if (chunk.ComponentKind == ShooterPackedComponentKinds.Transform
                            && i < SafeLength(chunk.ValueX)
                            && i < SafeLength(chunk.ValueY)
                            && i < SafeLength(chunk.ValueZ)
                            && i < SafeLength(chunk.ValueW))
                        {
                            player.X = chunk.ValueX[i];
                            player.Y = chunk.ValueY[i];
                            player.AimX = chunk.ValueZ[i];
                            player.AimY = chunk.ValueW[i];
                            found = true;
                        }
                        else if (chunk.ComponentKind == ShooterPackedComponentKinds.Health
                            && i < SafeLength(chunk.IntValues))
                        {
                            player.Hp = chunk.IntValues[i];
                            found = true;
                        }
                        else if (chunk.ComponentKind == ShooterPackedComponentKinds.Score
                            && i < SafeLength(chunk.IntValues))
                        {
                            player.Score = chunk.IntValues[i];
                            found = true;
                        }
                        else if (chunk.ComponentKind == ShooterPackedComponentKinds.EntityLifecycle
                            && i < SafeLength(chunk.Flags))
                        {
                            player.Alive = (chunk.Flags[i] & ShooterPackedEntityFlags.Alive) != 0;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    return true;
                }
            }

            if (snapshot.PureStateSnapshot.HasValue)
            {
                var entities = snapshot.PureStateSnapshot.Value.Entities;
                for (var i = 0; i < SafeLength(entities); i++)
                {
                    var entity = entities[i];
                    if (entity.EntityId != playerId
                        || entity.EntityKind != ShooterPackedEntityKinds.Player
                        || entity.DeltaKind == ShooterPureStateDeltaKinds.Despawn)
                    {
                        continue;
                    }

                    player.X = entity.QuantizedX / PositionQuantizationScale;
                    player.Y = entity.QuantizedY / PositionQuantizationScale;
                    player.AimX = entity.QuantizedVelocityX / PositionQuantizationScale;
                    player.AimY = entity.QuantizedVelocityY / PositionQuantizationScale;
                    player.Hp = entity.Hp;
                    player.Score = entity.Score;
                    player.Alive = (entity.Flags & ShooterPureStateEntityFlags.Alive) != 0;
                    return true;
                }
            }

            var actors = snapshot.Actors;
            for (var i = 0; i < actors.Count; i++)
            {
                if (actors[i].ActorId != playerId)
                {
                    continue;
                }

                player.X = actors[i].X;
                player.Y = actors[i].Y;
                player.Hp = (int)actors[i].Hp;
                return true;
            }

            return false;
        }

        private static bool PlayersEqual(
            in ShooterSveltoPlayerComponent left,
            in ShooterSveltoPlayerComponent right)
        {
            return left.X == right.X
                && left.Y == right.Y
                && left.AimX == right.AimX
                && left.AimY == right.AimY
                && left.Hp == right.Hp
                && left.Score == right.Score
                && left.Alive == right.Alive;
        }

        private static ulong ResolveAcknowledgedSequence(in ShooterGatewaySnapshot snapshot, int playerId)
        {
            var acknowledgements = snapshot.PackedSnapshot?.AcknowledgedCommands
                ?? snapshot.PureStateSnapshot?.AcknowledgedCommands
                ?? Array.Empty<ShooterCommandAcknowledgement>();
            for (var i = 0; i < acknowledgements.Length; i++)
            {
                if (acknowledgements[i].PlayerId == playerId)
                {
                    return acknowledgements[i].CommandSequence;
                }
            }

            return 0;
        }

        private static uint ResolveAuthoritativeStateHash(in ShooterGatewaySnapshot snapshot)
        {
            return snapshot.PackedSnapshot?.StateHash
                ?? snapshot.PureStateSnapshot?.StateHash
                ?? 0u;
        }

        private sealed class PendingLocalInput
        {
            public PendingLocalInput(long submissionId, int requestedFrame, in ShooterPlayerCommand command)
            {
                SubmissionId = submissionId;
                RequestedFrame = requestedFrame;
                AcceptedFrame = requestedFrame;
                Command = command;
            }

            public long SubmissionId { get; }
            public int RequestedFrame { get; set; }
            public int AcceptedFrame { get; set; }
            public ShooterPlayerCommand Command { get; }
            public bool GatewayStarted { get; set; }
            public bool GatewayCompleted { get; set; }
            public ulong CommandSequence { get; set; }
        }

        private void RefreshPredictedPose(int playerId, int frame, long serverTicks)
        {
            if (playerId <= 0 || !_runtime.TryGetPlayer(playerId, out var player))
            {
                return;
            }

            _predictionState = _predictionState.WithPredictedPose(
                player.PlayerId,
                player.X,
                player.Y,
                player.AimX,
                player.AimY,
                frame,
                serverTicks);
        }

        private void ObserveAuthoritativeProjectileActions(in ShooterGatewaySnapshot snapshot)
        {
            if (!snapshot.PackedSnapshot.HasValue)
            {
                return;
            }

            var chunks = snapshot.PackedSnapshot.Value.ComponentChunks;
            if (chunks == null)
            {
                return;
            }

            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                if (chunk.ComponentKind != ShooterPackedComponentKinds.EntityLifecycle || chunk.EntityKind != ShooterPackedEntityKinds.Projectile)
                {
                    continue;
                }

                var count = Math.Min(chunk.Count, Math.Min(SafeLength(chunk.EntityIds), SafeLength(chunk.OwnerIds)));
                for (int i = 0; i < count; i++)
                {
                    var ownerPlayerId = chunk.OwnerIds[i];
                    if (ownerPlayerId <= 0)
                    {
                        continue;
                    }

                    SetPredictedAction(
                        ownerPlayerId,
                        ShooterStateSyncPredictedAction.Fire,
                        snapshot.Frame,
                        snapshot.ServerTicks,
                        CurrentFrame,
                        needsCatchUp: true,
                        ownerPlayerId,
                        0,
                        chunk.EntityIds[i]);
                }
            }
        }

        private void SetPredictedAction(
            int playerId,
            ShooterStateSyncPredictedAction action,
            int sourceFrame,
            long sourceServerTicks,
            int playbackFrame,
            bool needsCatchUp,
            int sourcePlayerId,
            int targetPlayerId,
            int bulletId)
        {
            if (playerId <= 0 || action == ShooterStateSyncPredictedAction.None)
            {
                return;
            }

            _predictionState = _predictionState.WithAction(
                playerId,
                action,
                sourceFrame,
                sourceServerTicks,
                playbackFrame,
                needsCatchUp,
                Math.Max(0, playbackFrame - sourceFrame),
                sourcePlayerId,
                targetPlayerId,
                bulletId);
        }

        private static int SafeLength<T>(T[]? values)
        {
            return values?.Length ?? 0;
        }

        private void PublishInterpolatedRemoteFrame()
        {
            // Pure-state snapshots already carry observer-specific AOI and LOD decisions. Once that
            // source is active, an older actor sample must not repopulate entities it despawned.
            if (_usesPureStatePresentation)
            {
                return;
            }

            // 框架播放层负责缓冲、时间线以及外推/饥饿策略；
            // Shooter 控制器只提供“投影 + 应用到表现层”这一半循环。
            if (!_playback.TrySample(out var interpolation))
            {
                return;
            }

            var projected = _projector.Project(in interpolation);
            _presentation.ApplyInterpolatedGatewaySnapshot(in projected);
        }

        /// <summary>
        /// 采集当前插值播放健康状态，用于诊断与 smoke 输出。
        /// </summary>
        public InterpolationDiagnostics GetInterpolationDiagnostics()
        {
            return _playback.GetDiagnostics();
        }

        private sealed class PureStateDiscreteBatchAccumulator
        {
            private readonly List<ShooterViewEntityChange> _entities = new List<ShooterViewEntityChange>(256);
            private readonly List<ShooterViewEntityKey> _removed = new List<ShooterViewEntityKey>(64);
            private readonly Dictionary<ShooterViewEntityKey, int> _entityIndices = new Dictionary<ShooterViewEntityKey, int>(256);
            private readonly HashSet<ShooterViewEntityKey> _removedKeys = new HashSet<ShooterViewEntityKey>();
            private readonly List<ShooterViewHealthComponentChange> _health = new List<ShooterViewHealthComponentChange>(256);
            private readonly List<ShooterViewScoreComponentChange> _score = new List<ShooterViewScoreComponentChange>(16);
            private readonly List<ShooterViewProjectileLifetimeComponentChange> _projectileLifetime = new List<ShooterViewProjectileLifetimeComponentChange>(128);
            private readonly List<ShooterEventSnapshot> _events = new List<ShooterEventSnapshot>(32);
            private ulong _worldId;
            private int _frame;
            private ulong _sequence;
            private ShooterViewSnapshotKind _snapshotKind;
            private ShooterViewBatchSource _source;

            public void Reset(in ShooterSnapshotViewBatch batch)
            {
                Clear();
                Append(in batch);
            }

            public void Append(in ShooterSnapshotViewBatch batch)
            {
                // A later full baseline supersedes all earlier deltas received before this render tick.
                if (batch.IsFullSnapshot && _sequence != 0UL)
                {
                    Clear();
                }

                _worldId = batch.WorldId;
                _frame = Math.Max(_frame, batch.Frame);
                _sequence = Math.Max(_sequence, batch.Sequence);
                if (batch.IsFullSnapshot || _snapshotKind == 0)
                {
                    _snapshotKind = batch.SnapshotKind;
                    _source = batch.Source;
                }

                for (var i = 0; i < batch.EntityChanges.Count; i++)
                {
                    var entity = batch.EntityChanges[i];
                    if (entity.Alive && _removedKeys.Remove(entity.Key))
                    {
                        _removed.Remove(entity.Key);
                    }

                    if (_entityIndices.TryGetValue(entity.Key, out var existingIndex))
                    {
                        _entities[existingIndex] = entity;
                    }
                    else
                    {
                        _entityIndices.Add(entity.Key, _entities.Count);
                        _entities.Add(entity);
                    }
                }

                for (var i = 0; i < batch.RemovedEntities.Count; i++)
                {
                    var key = batch.RemovedEntities[i];
                    if (_removedKeys.Add(key))
                    {
                        _removed.Add(key);
                    }

                    // Component groups are applied after removals. Marking an earlier spawn dead
                    // prevents a full+delta burst from re-adding an entity that was just despawned.
                    if (_entityIndices.TryGetValue(key, out var existingIndex))
                    {
                        var entity = _entities[existingIndex];
                        _entities[existingIndex] = new ShooterViewEntityChange(
                            entity.Key,
                            entity.OwnerEntityId,
                            alive: false);
                    }
                }

                AddRange(_health, batch.HealthChanges);
                AddRange(_score, batch.ScoreChanges);
                AddRange(_projectileLifetime, batch.ProjectileLifetimeChanges);
                AddRange(_events, batch.Events);
            }

            public ShooterSnapshotViewBatch CreateBatch()
            {
                return new ShooterSnapshotViewBatch(
                    _worldId,
                    _frame,
                    _sequence,
                    _snapshotKind,
                    _source,
                    _entities,
                    _removed,
                    Array.Empty<ShooterViewTransformComponentChange>(),
                    _health,
                    _score,
                    _projectileLifetime,
                    _events);
            }

            public void Clear()
            {
                _entities.Clear();
                _removed.Clear();
                _entityIndices.Clear();
                _removedKeys.Clear();
                _health.Clear();
                _score.Clear();
                _projectileLifetime.Clear();
                _events.Clear();
                _worldId = 0UL;
                _frame = 0;
                _sequence = 0UL;
                _snapshotKind = 0;
                _source = 0;
            }

            private static void AddRange<T>(List<T> destination, IReadOnlyList<T> source)
            {
                if (destination.Capacity < destination.Count + source.Count)
                {
                    destination.Capacity = destination.Count + source.Count;
                }

                for (var i = 0; i < source.Count; i++)
                {
                    destination.Add(source[i]);
                }
            }
        }

        private sealed class CompositeReadOnlyList<T> : IReadOnlyList<T>
        {
            private IReadOnlyList<T> _first = Array.Empty<T>();
            private IReadOnlyList<T> _second = Array.Empty<T>();

            public int Count => _first.Count + _second.Count;

            public T this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    return index < _first.Count ? _first[index] : _second[index - _first.Count];
                }
            }

            public void Reset(IReadOnlyList<T> first, IReadOnlyList<T> second)
            {
                _first = first ?? Array.Empty<T>();
                _second = second ?? Array.Empty<T>();
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (var i = 0; i < Count; i++)
                {
                    yield return this[i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class ReusableFilteredTransformList : IReadOnlyList<ShooterViewTransformComponentChange>
        {
            private ShooterViewTransformComponentChange[] _items = Array.Empty<ShooterViewTransformComponentChange>();

            public int Count { get; private set; }

            public ShooterViewTransformComponentChange this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    return _items[index];
                }
            }

            public void Reset(
                IReadOnlyList<ShooterViewTransformComponentChange> source,
                HashSet<ShooterViewEntityKey> suppressed)
            {
                if (_items.Length < source.Count)
                {
                    _items = new ShooterViewTransformComponentChange[Math.Max(source.Count, Math.Max(16, _items.Length * 2))];
                }

                Count = 0;
                for (var i = 0; i < source.Count; i++)
                {
                    var transform = source[i];
                    if (!suppressed.Contains(transform.Key))
                    {
                        _items[Count++] = transform;
                    }
                }
            }

            public void Clear()
            {
                Count = 0;
            }

            public IEnumerator<ShooterViewTransformComponentChange> GetEnumerator()
            {
                for (var i = 0; i < Count; i++)
                {
                    yield return _items[i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        // --- IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> ---
        // 显式框架契约接口，映射到现有示例行为。

        SyncTickResult IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.Tick(float deltaSeconds)
        {
            return ShooterClientSyncStrategyMapping.ToSyncTickResult(Tick(deltaSeconds));
        }

        void IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.SubmitInput(in ShooterPlayerCommand input)
        {
            SubmitLocalInput(in input);
        }

        void IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.ObserveRemote(in ShooterRemoteSnapshotSample sample)
        {
            // 对权威插值来说，观察远端样本会写入延迟播放缓冲
            // （与 BufferRemoteSnapshot/ApplyGatewayPush 使用同一路径），绝不会进入本地模拟。
            _playback.Observe(sample);
        }

        SyncReconciliationReport IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.GetReconciliationReport()
        {
            return _localReconciliationReport.DidReconcile
                ? _localReconciliationReport
                : ShooterClientSyncStrategyMapping.ToReconciliationReport(this);
        }
    }

    public enum ShooterStateSyncPredictedAction
    {
        None = 0,
        Fire = 1,
        Hit = 2
    }

    public readonly struct ShooterStateSyncPredictionState
    {
        public static readonly ShooterStateSyncPredictionState Empty = new ShooterStateSyncPredictionState(
            0,
            false,
            0f,
            0f,
            0f,
            0f,
            0,
            0L,
            0,
            ShooterStateSyncPredictedAction.None,
            0,
            0L,
            0,
            false,
            0,
            0,
            0,
            0);

        public readonly int PlayerId;
        public readonly bool HasPredictedPose;
        public readonly float PredictedX;
        public readonly float PredictedY;
        public readonly float PredictedAimX;
        public readonly float PredictedAimY;
        public readonly int PredictedFrame;
        public readonly long PredictedServerTicks;
        public readonly int ActionPlayerId;
        public readonly ShooterStateSyncPredictedAction Action;
        public readonly int ActionSourceFrame;
        public readonly long ActionSourceServerTicks;
        public readonly int ActionPlaybackFrame;
        public readonly bool NeedsActionCatchUp;
        public readonly int ActionCatchUpFrames;
        public readonly int ActionSourcePlayerId;
        public readonly int ActionTargetPlayerId;
        public readonly int ActionBulletId;

        public ShooterStateSyncPredictionState(
            int playerId,
            bool hasPredictedPose,
            float predictedX,
            float predictedY,
            float predictedAimX,
            float predictedAimY,
            int predictedFrame,
            long predictedServerTicks,
            int actionPlayerId,
            ShooterStateSyncPredictedAction action,
            int actionSourceFrame,
            long actionSourceServerTicks,
            int actionPlaybackFrame,
            bool needsActionCatchUp,
            int actionCatchUpFrames,
            int actionSourcePlayerId,
            int actionTargetPlayerId,
            int actionBulletId)
        {
            PlayerId = playerId;
            HasPredictedPose = hasPredictedPose;
            PredictedX = predictedX;
            PredictedY = predictedY;
            PredictedAimX = predictedAimX;
            PredictedAimY = predictedAimY;
            PredictedFrame = predictedFrame;
            PredictedServerTicks = predictedServerTicks;
            ActionPlayerId = actionPlayerId;
            Action = action;
            ActionSourceFrame = actionSourceFrame;
            ActionSourceServerTicks = actionSourceServerTicks;
            ActionPlaybackFrame = actionPlaybackFrame;
            NeedsActionCatchUp = needsActionCatchUp;
            ActionCatchUpFrames = actionCatchUpFrames;
            ActionSourcePlayerId = actionSourcePlayerId;
            ActionTargetPlayerId = actionTargetPlayerId;
            ActionBulletId = actionBulletId;
        }

        public ShooterStateSyncPredictionState WithPredictedPose(
            int playerId,
            float x,
            float y,
            float aimX,
            float aimY,
            int frame,
            long serverTicks)
        {
            return new ShooterStateSyncPredictionState(
                playerId,
                true,
                x,
                y,
                aimX,
                aimY,
                frame,
                serverTicks,
                ActionPlayerId,
                Action,
                ActionSourceFrame,
                ActionSourceServerTicks,
                ActionPlaybackFrame,
                NeedsActionCatchUp,
                ActionCatchUpFrames,
                ActionSourcePlayerId,
                ActionTargetPlayerId,
                ActionBulletId);
        }

        public ShooterStateSyncPredictionState WithAction(
            int playerId,
            ShooterStateSyncPredictedAction action,
            int sourceFrame,
            long sourceServerTicks,
            int playbackFrame,
            bool needsCatchUp,
            int catchUpFrames,
            int sourcePlayerId,
            int targetPlayerId,
            int bulletId)
        {
            return new ShooterStateSyncPredictionState(
                PlayerId,
                HasPredictedPose,
                PredictedX,
                PredictedY,
                PredictedAimX,
                PredictedAimY,
                PredictedFrame,
                PredictedServerTicks,
                playerId,
                action,
                sourceFrame,
                sourceServerTicks,
                playbackFrame,
                needsCatchUp,
                catchUpFrames,
                sourcePlayerId,
                targetPlayerId,
                bulletId);
        }
    }
}
