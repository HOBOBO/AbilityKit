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
    public readonly struct ShooterPureStatePlaybackDiagnostics
    {
        public ShooterPureStatePlaybackDiagnostics(
            long renderTickCount,
            long publishedSnapshotCount,
            long starvedRenderTickCount,
            long heldPlaybackRenderTickCount,
            int bufferedSnapshotCount,
            float bufferedFrameSpan,
            float availablePlaybackLeadFrames,
            float playbackFrame,
            float currentDelayFrames,
            float targetDelayFrames,
            int baseDelayFrames,
            int maxDelayFrames,
            bool isStarved,
            long receivedSampleBlockCount,
            long receivedFrameSampleCount,
            long rejectedFrameSampleCount,
            long staleFrameSampleCount,
            long invalidFrameSampleCount,
            long receivedTransformSampleCount,
            int maxTransformSampleCountPerBlock,
            long receivedAuthoritativeTransformCount,
            long observedTransformSampleIntervalCount,
            int transformSampleIntervalP50Frames,
            int transformSampleIntervalP95Frames,
            int transformSampleIntervalP99Frames,
            int transformSampleIntervalMaxFrames)
        {
            RenderTickCount = renderTickCount;
            PublishedSnapshotCount = publishedSnapshotCount;
            StarvedRenderTickCount = starvedRenderTickCount;
            HeldPlaybackRenderTickCount = heldPlaybackRenderTickCount;
            BufferedSnapshotCount = bufferedSnapshotCount;
            BufferedFrameSpan = bufferedFrameSpan;
            AvailablePlaybackLeadFrames = availablePlaybackLeadFrames;
            PlaybackFrame = playbackFrame;
            CurrentDelayFrames = currentDelayFrames;
            TargetDelayFrames = targetDelayFrames;
            BaseDelayFrames = baseDelayFrames;
            MaxDelayFrames = maxDelayFrames;
            IsStarved = isStarved;
            ReceivedSampleBlockCount = receivedSampleBlockCount;
            ReceivedFrameSampleCount = receivedFrameSampleCount;
            RejectedFrameSampleCount = rejectedFrameSampleCount;
            StaleFrameSampleCount = staleFrameSampleCount;
            InvalidFrameSampleCount = invalidFrameSampleCount;
            ReceivedTransformSampleCount = receivedTransformSampleCount;
            MaxTransformSampleCountPerBlock = maxTransformSampleCountPerBlock;
            ReceivedAuthoritativeTransformCount = receivedAuthoritativeTransformCount;
            ObservedTransformSampleIntervalCount = observedTransformSampleIntervalCount;
            TransformSampleIntervalP50Frames = transformSampleIntervalP50Frames;
            TransformSampleIntervalP95Frames = transformSampleIntervalP95Frames;
            TransformSampleIntervalP99Frames = transformSampleIntervalP99Frames;
            TransformSampleIntervalMaxFrames = transformSampleIntervalMaxFrames;
        }

        public long RenderTickCount { get; }
        public long PublishedSnapshotCount { get; }
        public long StarvedRenderTickCount { get; }
        public long HeldPlaybackRenderTickCount { get; }
        public int BufferedSnapshotCount { get; }
        public float BufferedFrameSpan { get; }
        public float AvailablePlaybackLeadFrames { get; }
        public float PlaybackFrame { get; }
        public float CurrentDelayFrames { get; }
        public float TargetDelayFrames { get; }
        public int BaseDelayFrames { get; }
        public int MaxDelayFrames { get; }
        public bool IsStarved { get; }
        public long ReceivedSampleBlockCount { get; }
        public long ReceivedFrameSampleCount { get; }
        public long RejectedFrameSampleCount { get; }
        public long StaleFrameSampleCount { get; }
        public long InvalidFrameSampleCount { get; }
        public long ReceivedTransformSampleCount { get; }
        public int MaxTransformSampleCountPerBlock { get; }
        public long ReceivedAuthoritativeTransformCount { get; }
        public long ObservedTransformSampleIntervalCount { get; }
        public int TransformSampleIntervalP50Frames { get; }
        public int TransformSampleIntervalP95Frames { get; }
        public int TransformSampleIntervalP99Frames { get; }
        public int TransformSampleIntervalMaxFrames { get; }
        public double AverageFrameSamplesPerBlock => ReceivedSampleBlockCount > 0L
            ? ReceivedFrameSampleCount / (double)ReceivedSampleBlockCount
            : 0d;
        public double AverageTransformSamplesPerFrame => ReceivedFrameSampleCount > 0L
            ? ReceivedTransformSampleCount / (double)ReceivedFrameSampleCount
            : 0d;
        public double HistoricalTransformAmplificationRatio => ReceivedAuthoritativeTransformCount > 0L
            ? ReceivedTransformSampleCount / (double)ReceivedAuthoritativeTransformCount
            : 0d;
        public double StarvationRatio => RenderTickCount > 0L
            ? StarvedRenderTickCount / (double)RenderTickCount
            : 0d;
        public double HeldPlaybackRatio => RenderTickCount > 0L
            ? HeldPlaybackRenderTickCount / (double)RenderTickCount
            : 0d;
    }

    public interface IShooterPureStatePlaybackDiagnosticsProvider
    {
        ShooterPureStatePlaybackDiagnostics GetPureStatePlaybackDiagnostics();
    }

    /// <summary>
    /// <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 客户端控制器。
    /// 本地玩家使用权威 pose、输入确认和有界未确认输入重放；远端 actor 只进入服务器时间线插值，
    /// 不导入本地模拟，也不触发整世界回滚。
    /// </summary>
    public sealed class ShooterClientAuthoritativeInterpolationSyncController : IShooterClientSyncController, IShooterClientFrameSyncCapability, IShooterClientInputCapability, IInterpolationDiagnosticsProvider, IShooterPureStatePlaybackDiagnosticsProvider
    {
        private const int MaxPendingInputs = 128;
        private const int MaxReplayFrames = 120;
        private const float PositionQuantizationScale = 1000f;
        private const float SmallErrorTolerance = 0.05f;
        private const float MaxCorrectionPerClientFrame = 0.25f;

        /// <summary>
        /// 直接快照阈值：误差达到或超过此值（重连/真实分歧）直接赋值权威位置。
        /// 纯状态表现路径下低于此值一律信任本地预测（不回拉，另一端按权威自然追上来重合）。
        /// </summary>
        private const float SnapCorrectionDistance = 2.5f;
        private const float PureStateStableRecoverySeconds = 2f;
        private const int TransformSampleIntervalHistogramFrames = 256;

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
        private readonly Dictionary<ShooterViewEntityKey, int> _lastPureStateTransformSampleFrames = new Dictionary<ShooterViewEntityKey, int>();
        private readonly long[] _pureStateTransformSampleIntervalHistogram = new long[TransformSampleIntervalHistogramFrames];
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
        private int _controlledCorrectionRuntimeFrame = -1;
        private float _controlledCorrectionAppliedDistance;
        private long _nextSyntheticSubmissionId;
        private int _pureStateDeltaIntervalFrames = 1;
        private int _pureStateBaseDelayFrames = 2;
        private int _pureStateMaxDelayFrames = 3;
        private float _pureStateStablePlaybackSeconds;
        private long _pureStateRenderTickCount;
        private long _pureStatePublishedSnapshotCount;
        private long _pureStateStarvedRenderTickCount;
        private long _pureStateHeldPlaybackRenderTickCount;
        private long _pureStateReceivedSampleBlockCount;
        private long _pureStateReceivedFrameSampleCount;
        private long _pureStateRejectedFrameSampleCount;
        private long _pureStateStaleFrameSampleCount;
        private long _pureStateInvalidFrameSampleCount;
        private long _pureStateReceivedTransformSampleCount;
        private int _pureStateMaxTransformSampleCountPerBlock;
        private long _pureStateReceivedAuthoritativeTransformCount;
        private long _pureStateObservedTransformSampleIntervalCount;
        private int _pureStateTransformSampleIntervalMaxFrames;
        private int _lastPureStatePublishedFrame = -1;
        private ulong _pureStatePlaybackWorldId;
        private ShooterSnapshotViewBatch _lastPureStatePredictedBatch = ShooterSnapshotViewBatch.Empty;

        /// <summary>调和诊断开关：ABILITYKIT_SHOOTER_RECONCILE_DIAGNOSTICS=1 时逐次打印纠偏明细。</summary>
        private static readonly bool ReconcileDiagnosticsEnabled =
            string.Equals(
                Environment.GetEnvironmentVariable("ABILITYKIT_SHOOTER_RECONCILE_DIAGNOSTICS"),
                "1",
                StringComparison.OrdinalIgnoreCase);

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

        public ShooterPureStatePlaybackDiagnostics PureStatePlaybackDiagnostics =>
            GetPureStatePlaybackDiagnostics();

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
            if (_usesPureStatePresentation && result.Ticks > 0)
            {
                // 每次本地预测发布（PublishControlledPlayerPredictionOnly）后抓取该批，
                // 供纯状态合成使用；ViewModel 随后可能被权威应用批覆盖。
                _lastPureStatePredictedBatch = _presentation.ViewModel.Current;
            }

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

        public ShooterSnapshotApplyResult ApplyGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
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
                    var pureState = snapshot.PureStateSnapshot!.Value;
                    var pureStateWorldChanged = pureState.WorldId != 0UL &&
                        _pureStatePlaybackWorldId != 0UL &&
                        pureState.WorldId != _pureStatePlaybackWorldId;
                    if (pureStateWorldChanged)
                    {
                        _pureStatePlayback.Reset();
                        ResetPureStateDiscreteBatches();
                        ResetPureStatePlaybackAdaptation();
                    }

                    if (pureState.WorldId != 0UL)
                    {
                        _pureStatePlaybackWorldId = pureState.WorldId;
                    }

                    _usesPureStatePresentation = true;
                    _core.FrameSync.PublishControlledPlayerPredictionOnly = true;
                    _playback.Reset();
                    var presentationBatch = _presentation.ViewModel.Current;
                    var pureStateSettings = pureState.Settings;
                    ObservePureStatePresentationBatch(in presentationBatch, in pureStateSettings, in pureState);
                    ObserveGatewaySnapshotFrame(snapshot.Frame);
                    ReconcileControlledPlayer(in snapshot, forceAuthorityReset: pureStateWorldChanged);
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
            ResetPureStatePlaybackAdaptation();
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
            ResetControlledCorrectionBudget();
            _localReconciliationReport = SyncReconciliationReport.None;
            _predictionState = ShooterStateSyncPredictionState.Empty;
            _lastPredictedCommand = default;
            _usesPureStatePresentation = false;
            _core.FrameSync.PublishControlledPlayerPredictionOnly = false;
            _playback.Reset();
            _pureStatePlayback.Reset();
            ResetPureStateDiscreteBatches();
            ResetPureStatePlaybackAdaptation();
        }

        private void ObservePureStatePresentationBatch(
            in ShooterSnapshotViewBatch batch,
            in ShooterPureStateSyncSettings settings,
            in ShooterPureStateSnapshotPayload pureState)
        {
            if (batch.IsFullSnapshot)
            {
                _suppressedPureStateTransforms.Clear();
                _lastPureStateTransformSampleFrames.Clear();
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
                var removed = batch.RemovedEntities[i];
                _suppressedPureStateTransforms.Add(removed);
                _lastPureStateTransformSampleFrames.Remove(removed);
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

            ConfigurePureStatePlaybackDelay(in settings);

            ObservePureStateFrameSamples(in pureState);
            PublishPureStateTransformBatch(in batch);
        }

        private void ObservePureStateFrameSamples(in ShooterPureStateSnapshotPayload pureState)
        {
            var frameCount = pureState.EffectiveFrameSampleCount;
            if (frameCount <= 0)
            {
                return;
            }

            _pureStateReceivedSampleBlockCount++;
            var frames = pureState.FrameSamples;
            var transforms = pureState.TransformSamples;
            var transformCount = pureState.EffectiveTransformSampleCount;
            _pureStateMaxTransformSampleCountPerBlock = Math.Max(
                _pureStateMaxTransformSampleCountPerBlock,
                transformCount);
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var frameSample = frames[frameIndex];
                if (frameSample.Frame <= _lastPureStatePublishedFrame)
                {
                    _pureStateRejectedFrameSampleCount++;
                    _pureStateStaleFrameSampleCount++;
                    continue;
                }

                if (frameSample.Frame >= pureState.Frame ||
                    frameSample.TransformOffset < 0 ||
                    frameSample.TransformCount < 0 ||
                    frameSample.TransformOffset > transformCount - frameSample.TransformCount)
                {
                    _pureStateRejectedFrameSampleCount++;
                    _pureStateInvalidFrameSampleCount++;
                    continue;
                }

                var sampleTransforms = ShooterSnapshotViewModelMapper.RentPooledTransformChanges(frameSample.TransformCount);
                _pureStateReceivedTransformSampleCount += frameSample.TransformCount;
                var end = frameSample.TransformOffset + frameSample.TransformCount;
                for (var transformIndex = frameSample.TransformOffset; transformIndex < end; transformIndex++)
                {
                    var sample = transforms[transformIndex];
                    if (!TryCreateSampleTransform(in sample, out var transform) ||
                        (ShooterClientPredictionMode.LocalPredictionEnabled &&
                         transform.Key.EntityId == _presentation.ControlledPlayerId &&
                         transform.Key.Kind == ShooterViewEntityKind.Player) ||
                        _suppressedPureStateTransforms.Contains(transform.Key))
                    {
                        continue;
                    }

                    sampleTransforms.Add(transform);
                }

                ObservePureStateTransformSampleIntervals(frameSample.Frame, sampleTransforms);

                var playbackBatch = new ShooterSnapshotViewBatch(
                    pureState.WorldId,
                    frameSample.Frame,
                    (ulong)(uint)frameSample.Frame,
                    ShooterViewSnapshotKind.Delta,
                    ShooterViewBatchSource.AuthoritativeCorrection,
                    Array.Empty<ShooterViewEntityChange>(),
                    Array.Empty<ShooterViewEntityKey>(),
                    sampleTransforms,
                    Array.Empty<ShooterViewHealthComponentChange>(),
                    Array.Empty<ShooterViewScoreComponentChange>(),
                    Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
                    Array.Empty<ShooterEventSnapshot>());
                _pureStatePlayback.Publish(in playbackBatch);
                _lastPureStatePublishedFrame = frameSample.Frame;
                _pureStatePublishedSnapshotCount++;
                _pureStateReceivedFrameSampleCount++;
            }
        }

        private static bool TryCreateSampleTransform(
            in ShooterPureStateTransformSample sample,
            out ShooterViewTransformComponentChange transform)
        {
            ShooterViewEntityKind kind;
            switch (sample.EntityKind)
            {
                case ShooterPackedEntityKinds.Player:
                    kind = ShooterViewEntityKind.Player;
                    break;
                case ShooterPackedEntityKinds.Projectile:
                    kind = ShooterViewEntityKind.Bullet;
                    break;
                case ShooterPackedEntityKinds.Enemy:
                    kind = ShooterViewEntityKind.Enemy;
                    break;
                default:
                    transform = default;
                    return false;
            }

            const byte visibleAndAlive = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
            if ((sample.Flags & visibleAndAlive) != visibleAndAlive)
            {
                transform = default;
                return false;
            }

            var velocityX = sample.QuantizedVelocityX / 1000f;
            var velocityY = sample.QuantizedVelocityY / 1000f;
            var facingX = sample.QuantizedFacingX / 1000f;
            var facingY = sample.QuantizedFacingY / 1000f;
            // Historical block entries are sparse trajectory observations even when their
            // current-frame LOD flags describe a near entity.
            var hints = SnapshotDeliveryHints.SparseUpdate;
            transform = new ShooterViewTransformComponentChange(
                new ShooterViewEntityKey(kind, sample.EntityId),
                sample.QuantizedX / 1000f,
                sample.QuantizedY / 1000f,
                facingX == 0f && facingY == 0f ? 0f : facingX,
                facingX == 0f && facingY == 0f ? 1f : facingY,
                velocityX,
                velocityY,
                hints);
            return true;
        }

        private void PublishPureStateTransformBatch(in ShooterSnapshotViewBatch batch)
        {
            if (batch.Frame <= _lastPureStatePublishedFrame)
            {
                _pureStateRejectedFrameSampleCount++;
                _pureStateStaleFrameSampleCount++;
                return;
            }

            _pureStateReceivedAuthoritativeTransformCount += batch.TransformChanges.Count;

            var removed = ShooterSnapshotViewModelMapper.RentPooledRemovedEntities(batch.RemovedEntities.Count);
            for (var i = 0; i < batch.RemovedEntities.Count; i++)
            {
                removed.Add(batch.RemovedEntities[i]);
            }

            var transforms = ShooterSnapshotViewModelMapper.RentPooledTransformChanges(batch.TransformChanges.Count);
            for (var i = 0; i < batch.TransformChanges.Count; i++)
            {
                var transform = batch.TransformChanges[i];
                // 无预测模式：PredictedLocal 标记的服务端自身姿态是渲染真值，照常进播放流。
                if (!transform.IsPredictedLocal || !ShooterClientPredictionMode.LocalPredictionEnabled)
                {
                    transforms.Add(transform);
                }
            }

            ObservePureStateTransformSampleIntervals(batch.Frame, transforms);

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
            _lastPureStatePublishedFrame = batch.Frame;
            _pureStatePublishedSnapshotCount++;
        }

        private void PublishPureStatePresentationFrame(float deltaTime)
        {
            if (!_usesPureStatePresentation)
            {
                return;
            }

            _pureStateRenderTickCount++;
            var playbackBefore = _pureStatePlayback.PlaybackFrame;
            var hasPlayback = _pureStatePlayback.TryAdvancePlaybackTransient(deltaTime, out var remotePlayback);
            var playbackAfter = _pureStatePlayback.PlaybackFrame;
            var isStarved = _pureStatePlayback.IsPlaybackStarved;
            if (isStarved)
            {
                _pureStateStarvedRenderTickCount++;
            }

            if (deltaTime > 0f && playbackAfter <= playbackBefore)
            {
                _pureStateHeldPlaybackRenderTickCount++;
            }

            UpdatePureStatePlaybackDelay(deltaTime, isStarved);
            if (!hasPlayback)
            {
                return;
            }

            // 本地侧只取"最近一次本地预测发布批"。ViewModel.Current 会被权威应用批
            // （含远端实体原始变换、且不含被服务端剔除的己方角色）交替覆盖——直接引用
            // 会让推送帧出现同一远端实体两个位置（拉扯）并丢失己方角色（闪烁）。
            // 无预测模式：己方角色由播放流按权威渲染，本地侧为空。
            var local = ShooterClientPredictionMode.LocalPredictionEnabled
                ? _lastPureStatePredictedBatch
                : ShooterSnapshotViewBatch.Empty;
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
            _lastPureStatePredictedBatch = ShooterSnapshotViewBatch.Empty;
        }

        private void ConfigurePureStatePlaybackDelay(in ShooterPureStateSyncSettings settings)
        {
            var deltaIntervalFrames = Math.Max(1, settings.DeltaIntervalFrames);
            var baseDelayFrames = Math.Max(
                Math.Max(1, settings.InterpolationDelayFrames),
                SaturatingMultiplyFrames(deltaIntervalFrames, 2));
            var maxDelayFrames = Math.Max(
                baseDelayFrames,
                SaturatingMultiplyFrames(deltaIntervalFrames, 3));
            if (_pureStateDeltaIntervalFrames == deltaIntervalFrames
                && _pureStateBaseDelayFrames == baseDelayFrames
                && _pureStateMaxDelayFrames == maxDelayFrames)
            {
                return;
            }

            _pureStateDeltaIntervalFrames = deltaIntervalFrames;
            _pureStateBaseDelayFrames = baseDelayFrames;
            _pureStateMaxDelayFrames = maxDelayFrames;
            _pureStateStablePlaybackSeconds = 0f;
            _pureStatePlayback.TrySetTargetInterpolationDelayFrames(baseDelayFrames);
        }

        private void UpdatePureStatePlaybackDelay(float deltaTime, bool isStarved)
        {
            if (isStarved)
            {
                _pureStateStablePlaybackSeconds = 0f;
                _pureStatePlayback.TrySetTargetInterpolationDelayFrames(_pureStateMaxDelayFrames);
                return;
            }

            if (_pureStatePlayback.TargetInterpolationDelayFrames <= _pureStateBaseDelayFrames)
            {
                _pureStateStablePlaybackSeconds = 0f;
                return;
            }

            _pureStateStablePlaybackSeconds += Math.Max(0f, deltaTime);
            if (_pureStateStablePlaybackSeconds < PureStateStableRecoverySeconds)
            {
                return;
            }

            _pureStateStablePlaybackSeconds = 0f;
            _pureStatePlayback.TrySetTargetInterpolationDelayFrames(_pureStateBaseDelayFrames);
        }

        private static int SaturatingMultiplyFrames(int frames, int multiplier)
        {
            return frames > int.MaxValue / multiplier
                ? int.MaxValue
                : frames * multiplier;
        }

        private void ResetPureStatePlaybackAdaptation()
        {
            _pureStateDeltaIntervalFrames = 1;
            _pureStateBaseDelayFrames = 2;
            _pureStateMaxDelayFrames = 3;
            _pureStateStablePlaybackSeconds = 0f;
            _pureStateRenderTickCount = 0L;
            _pureStatePublishedSnapshotCount = 0L;
            _pureStateStarvedRenderTickCount = 0L;
            _pureStateHeldPlaybackRenderTickCount = 0L;
            _pureStateReceivedSampleBlockCount = 0L;
            _pureStateReceivedFrameSampleCount = 0L;
            _pureStateRejectedFrameSampleCount = 0L;
            _pureStateStaleFrameSampleCount = 0L;
            _pureStateInvalidFrameSampleCount = 0L;
            _pureStateReceivedTransformSampleCount = 0L;
            _pureStateMaxTransformSampleCountPerBlock = 0;
            _pureStateReceivedAuthoritativeTransformCount = 0L;
            _pureStateObservedTransformSampleIntervalCount = 0L;
            _pureStateTransformSampleIntervalMaxFrames = 0;
            _lastPureStateTransformSampleFrames.Clear();
            Array.Clear(_pureStateTransformSampleIntervalHistogram, 0, _pureStateTransformSampleIntervalHistogram.Length);
            _lastPureStatePublishedFrame = -1;
            _pureStatePlaybackWorldId = 0UL;
            _pureStatePlayback.TrySetTargetInterpolationDelayFrames(_pureStateBaseDelayFrames);
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
            // 无预测模式：本地世界不再作为渲染来源，纠偏调和没有意义，跳过。
            if (!ShooterClientPredictionMode.LocalPredictionEnabled)
            {
                return;
            }

            var playerId = _presentation.ControlledPlayerId;
            if (playerId <= 0 || snapshot.Frame < 0 || !_runtime.TryGetPlayer(playerId, out var current))
            {
                return;
            }

            if (forceAuthorityReset)
            {
                lock (_pendingInputLock)
                {
                    _pendingInputs.Clear();
                }

                _authorityWorldId = 0;
                _lastAuthorityFrame = -1;
                ResetControlledCorrectionBudget();
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

            _authorityWorldId = snapshot.WorldId != 0 ? snapshot.WorldId : _authorityWorldId;
            _lastAuthorityFrame = snapshot.Frame;
            var acknowledgedSequence = ResolveAcknowledgedSequence(in snapshot, playerId);
            var replayCount = ReplayPendingInputs(
                ref target,
                playerId,
                snapshot.Frame,
                acknowledgedSequence);

            // Full baselines and authority-override snapshots are normal recovery traffic in the
            // same world. Snapping their replayed pose can place the player outside gameplay
            // constraints and causes the following simulation tick to clamp it back visibly.
            // Only a world transition invalidates the current prediction outright.
            var forceSnap = worldChanged;
            if (_controlledCorrectionRuntimeFrame != CurrentFrame)
            {
                _controlledCorrectionRuntimeFrame = CurrentFrame;
                _controlledCorrectionAppliedDistance = 0f;
            }

            var remainingCorrectionBudget = Math.Max(
                0f,
                MaxCorrectionPerClientFrame - _controlledCorrectionAppliedDistance);
            // 纯状态表现路径：信任本地预测——预测正确时本地位置即最终位置（另一端按权威
            // 自然追上来重合），只有达到快照阈值（重连/真实分歧）才直接赋值权威。
            // 通过把容差设成与快照阈值相同来消除中间"有界收敛"档。
            // 打包路径（帧同步式）保持精确跟踪权威（窄容差、仅世界切换才快照）。
            var retainLocalPrediction = _usesPureStatePresentation;
            var appliedCorrection = ResolveControlledPlayerPosition(
                current.X,
                current.Y,
                target.X,
                target.Y,
                CurrentFrame,
                snapshot.Frame,
                replayCount,
                _fixedDeltaTime,
                remainingCorrectionBudget,
                forceSnap,
                retainLocalPrediction ? SnapCorrectionDistance : SmallErrorTolerance,
                retainLocalPrediction ? SnapCorrectionDistance : float.MaxValue,
                out var resolvedX,
                out var resolvedY);
            target.X = resolvedX;
            target.Y = resolvedY;
            if (forceSnap)
            {
                _controlledCorrectionAppliedDistance = MaxCorrectionPerClientFrame;
            }
            else
            {
                _controlledCorrectionAppliedDistance = Math.Min(
                    MaxCorrectionPerClientFrame,
                    _controlledCorrectionAppliedDistance + appliedCorrection);
            }

            if (PlayersEqual(in current, in target))
            {
                return;
            }

            if (ReconcileDiagnosticsEnabled)
            {
                System.Console.WriteLine(
                    $"[Reconcile] frame={snapshot.Frame} current=({current.X:F2},{current.Y:F2}) target=({target.X:F2},{target.Y:F2}) replayed={replayCount} ack={acknowledgedSequence} budget={remainingCorrectionBudget:F2} clientFrame={CurrentFrame}");
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

        internal static float ResolveControlledPlayerPosition(
            float currentX,
            float currentY,
            float replayedAuthorityX,
            float replayedAuthorityY,
            int currentFrame,
            int authorityFrame,
            int replayedFrames,
            float fixedDeltaTime,
            float correctionBudget,
            bool forceSnap,
            float localPredictionTolerance,
            float snapDistance,
            out float resolvedX,
            out float resolvedY)
        {
            var deltaX = replayedAuthorityX - currentX;
            var deltaY = replayedAuthorityY - currentY;
            var error = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // 三档纠偏：
            // 1) 世界切换或误差达到快照阈值（重连/真实分歧）：直接赋值权威位置，不逐帧爬；
            // 2) 误差在本地预测保留阈值内：视为正常预测提前量，本地位置胜出、不回拉；
            // 3) 中间地带：按预算有界收敛。
            if (forceSnap || error >= snapDistance)
            {
                resolvedX = replayedAuthorityX;
                resolvedY = replayedAuthorityY;
                return error;
            }

            // 合法预测提前量已由"未确认输入重放到目标"精确覆盖：目标=权威+在线输入。
            // 帧号差（网络/管线延迟）不构成合法提前——把它当容差会把真实漂移永久吸收。
            _ = replayedFrames;
            _ = currentFrame;
            _ = authorityFrame;
            if (error <= localPredictionTolerance || error <= 0f || correctionBudget <= 0f)
            {
                resolvedX = currentX;
                resolvedY = currentY;
                return 0f;
            }

            var correction = Math.Min(error, correctionBudget);
            var scale = correction / error;
            resolvedX = currentX + deltaX * scale;
            resolvedY = currentY + deltaY * scale;
            return correction;
        }

        private void ResetControlledCorrectionBudget()
        {
            _controlledCorrectionRuntimeFrame = -1;
            _controlledCorrectionAppliedDistance = 0f;
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
                    // 帧式清除兜底：输入已完成且其接受帧已不晚于权威帧，说明其移动已反映在
                    // 快照里，无论序号匹配是否生效都应清除——否则 pending 无限积压（重放
                    // 只覆盖最近的 MaxReplayFrames 条，本地领先持续暴露、反复被快照拽回）。
                    var frameAcknowledged = pending.GatewayCompleted
                        && pending.AcceptedFrame > 0
                        && pending.AcceptedFrame <= authorityFrame;
                    if (explicitlyAcknowledged || frameAcknowledged)
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
                    player.AimX = entity.QuantizedFacingX / PositionQuantizationScale;
                    player.AimY = entity.QuantizedFacingY / PositionQuantizationScale;
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

        public ShooterPureStatePlaybackDiagnostics GetPureStatePlaybackDiagnostics()
        {
            return new ShooterPureStatePlaybackDiagnostics(
                _pureStateRenderTickCount,
                _pureStatePublishedSnapshotCount,
                _pureStateStarvedRenderTickCount,
                _pureStateHeldPlaybackRenderTickCount,
                _pureStatePlayback.BufferedSnapshotCount,
                _pureStatePlayback.BufferedFrameSpan,
                _pureStatePlayback.AvailablePlaybackLeadFrames,
                _pureStatePlayback.PlaybackFrame,
                _pureStatePlayback.InterpolationDelayFrames,
                _pureStatePlayback.TargetInterpolationDelayFrames,
                _pureStateBaseDelayFrames,
                _pureStateMaxDelayFrames,
                _pureStatePlayback.IsPlaybackStarved,
                _pureStateReceivedSampleBlockCount,
                _pureStateReceivedFrameSampleCount,
                _pureStateRejectedFrameSampleCount,
                _pureStateStaleFrameSampleCount,
                _pureStateInvalidFrameSampleCount,
                _pureStateReceivedTransformSampleCount,
                _pureStateMaxTransformSampleCountPerBlock,
                _pureStateReceivedAuthoritativeTransformCount,
                _pureStateObservedTransformSampleIntervalCount,
                ResolveTransformSampleIntervalPercentile(0.50d),
                ResolveTransformSampleIntervalPercentile(0.95d),
                ResolveTransformSampleIntervalPercentile(0.99d),
                _pureStateTransformSampleIntervalMaxFrames);
        }

        private void ObservePureStateTransformSampleIntervals(
            int frame,
            IReadOnlyList<ShooterViewTransformComponentChange> transforms)
        {
            for (var i = 0; i < transforms.Count; i++)
            {
                var key = transforms[i].Key;
                if (_lastPureStateTransformSampleFrames.TryGetValue(key, out var previousFrame))
                {
                    var interval = frame - previousFrame;
                    if (interval <= 0)
                    {
                        continue;
                    }

                    var bucket = Math.Min(interval, _pureStateTransformSampleIntervalHistogram.Length - 1);
                    _pureStateTransformSampleIntervalHistogram[bucket]++;
                    _pureStateObservedTransformSampleIntervalCount++;
                    _pureStateTransformSampleIntervalMaxFrames = Math.Max(
                        _pureStateTransformSampleIntervalMaxFrames,
                        interval);
                }

                _lastPureStateTransformSampleFrames[key] = frame;
            }
        }

        private int ResolveTransformSampleIntervalPercentile(double percentile)
        {
            if (_pureStateObservedTransformSampleIntervalCount <= 0L)
            {
                return 0;
            }

            var threshold = (long)Math.Ceiling(
                _pureStateObservedTransformSampleIntervalCount * Math.Max(0d, Math.Min(1d, percentile)));
            var cumulative = 0L;
            for (var frame = 0; frame < _pureStateTransformSampleIntervalHistogram.Length; frame++)
            {
                cumulative += _pureStateTransformSampleIntervalHistogram[frame];
                if (cumulative >= threshold)
                {
                    return frame;
                }
            }

            return _pureStateTransformSampleIntervalMaxFrames;
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
