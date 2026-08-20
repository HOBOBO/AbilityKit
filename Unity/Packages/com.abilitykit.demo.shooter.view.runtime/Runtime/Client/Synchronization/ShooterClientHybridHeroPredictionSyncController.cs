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
    /// <see cref="NetworkSyncModel.HybridHeroPrediction"/> 的混合同步控制器。
    /// 本地模拟与权威校正仍委托给预测回滚；已解码的远端 actor 样本会进入缓冲，并通过延迟权威插值播放。
    /// </summary>
    public sealed class ShooterClientHybridHeroPredictionSyncController : IShooterClientSyncController, IShooterClientFrameSyncCapability, IShooterClientInputCapability, IInterpolationDiagnosticsProvider
    {
        private readonly ShooterClientPredictRollbackSyncController _rollback;
        private readonly ShooterPresentationFacade _presentation;
        private readonly ShooterGatewaySnapshotDecoder _decoder;
        private readonly RemoteInterpolationPlayback<ShooterRemoteSnapshotSample> _playback;
        private readonly ShooterRemoteSnapshotProjector _projector = new ShooterRemoteSnapshotProjector();

        public ShooterClientHybridHeroPredictionSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway)
            : this(runtime, presentation, tickRate, decoder, gateway, InterpolationConfig.Default)
        {
        }

        public ShooterClientHybridHeroPredictionSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            InterpolationConfig config,
            ShooterClientPredictionBufferOptions? predictionBufferOptions = null)
        {
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _decoder = decoder ?? new ShooterGatewaySnapshotDecoder();
            _rollback = new ShooterClientPredictRollbackSyncController(
                runtime,
                presentation,
                tickRate,
                decoder,
                gateway,
                predictionBufferOptions);
            _playback = new RemoteInterpolationPlayback<ShooterRemoteSnapshotSample>(config);
        }

        public NetworkSyncModel SyncModel => NetworkSyncModel.HybridHeroPrediction;

        public bool IsStarted => _rollback.IsStarted;

        public int CurrentFrame => _rollback.CurrentFrame;

        public int GatewayInputFrame => _rollback.GatewayInputFrame;

        public ShooterClientFrameSyncController FrameSync => _rollback.FrameSync;

        public ShooterClientInputCoordinator InputCoordinator => _rollback.InputCoordinator;

        public ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics => _rollback.FrameworkSnapshotPipelineDiagnostics;

        public ShooterClientReconciliationResult LastReconciliationResult => _rollback.LastReconciliationResult;

        public bool NeedsFullSnapshotResync => _rollback.NeedsFullSnapshotResync;

        public ShooterClientRecoveryState RecoveryState => _rollback.RecoveryState;

        public FastReconnectPhase FastReconnectPhase => _rollback.FastReconnectPhase;

        public IReadOnlyList<SyncHealthEvent> LastFastReconnectHealthEvents => _rollback.LastFastReconnectHealthEvents;

        public ShooterClientResyncReason LastResyncReason => _rollback.LastResyncReason;

        public int LastResyncClientFrame => _rollback.LastResyncClientFrame;

        public int LastResyncAuthoritativeFrame => _rollback.LastResyncAuthoritativeFrame;

        public uint LastResyncClientStateHash => _rollback.LastResyncClientStateHash;

        public uint LastResyncAuthoritativeStateHash => _rollback.LastResyncAuthoritativeStateHash;

        public bool HasGateway => _rollback.HasGateway;

        /// <summary>当前为插值缓冲的远端权威快照数量。</summary>
        public int BufferedRemoteSnapshotCount => _playback.BufferedSampleCount;

        /// <summary>当前延迟远端播放时间，单位为时间线 tick。</summary>
        public long RemotePlaybackTicks => _playback.PlaybackTicks;

        /// <summary>当前本地估算的权威服务器时间，单位为时间线 tick。</summary>
        public long EstimatedServerTicks => _playback.EstimatedServerTicks;

        /// <summary>是否已经向表现层发布过至少一帧远端插值结果。</summary>
        public bool HasPublishedRemoteFrame => _playback.HasPublished;

        /// <summary>远端插值是否因缓冲饥饿而保持最新样本。</summary>
        public bool IsRemotePlaybackStarved => _playback.IsStarved;

        public bool StartGame(in ShooterStartGamePayload startGame)
        {
            return _rollback.StartGame(in startGame);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            return _rollback.SubmitLocalInput(playerId, moveX, moveY, aimX, aimY, fire);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(in ShooterPlayerCommand command)
        {
            return _rollback.SubmitLocalInput(in command);
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _rollback.SubmitLocalInputToGatewayAsync(context, command, timeout, cancellationToken);
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _rollback.SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken);
        }

        public ShooterClientFrameTickResult Tick(float deltaTime)
        {
            var result = _rollback.Tick(deltaTime);
            _playback.Advance(deltaTime);
            PublishInterpolatedRemoteFrame();
            return result;
        }

        public ShooterClientFrameTickResult CatchUpToFrame(int targetFrame)
        {
            return _rollback.CatchUpToFrame(targetFrame);
        }

        public bool TryEnterCatchUp(int authoritativeFrame)
        {
            return _rollback.TryEnterCatchUp(authoritativeFrame);
        }

        public ShooterSnapshotApplyResult ApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            var rollbackResult = _rollback.ApplyGatewayPush(opCode, payload);
            if (!_decoder.IsSnapshotPush(opCode))
            {
                return rollbackResult;
            }

            var snapshot = _decoder.Decode(payload);
            if (snapshot.PureStateSnapshot.HasValue)
            {
                return rollbackResult;
            }

            var interpolationResult = BufferRemoteSnapshot(in snapshot);
            return rollbackResult == ShooterSnapshotApplyResult.Ignored ? interpolationResult : rollbackResult;
        }

        /// <summary>为延迟插值缓冲一个已经解码的远端权威快照。</summary>
        public ShooterSnapshotApplyResult BufferRemoteSnapshot(in ShooterGatewaySnapshot snapshot)
        {
            var sample = new ShooterRemoteSnapshotSample(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.ServerTicks,
                snapshot.Actors,
                snapshot.PackedSnapshot);

            return _playback.Observe(sample)
                ? ShooterSnapshotApplyResult.AppliedActorSnapshot
                : ShooterSnapshotApplyResult.IgnoredStaleSnapshot;
        }

        public InterpolationDiagnostics GetInterpolationDiagnostics()
        {
            return _playback.GetDiagnostics();
        }

        private void PublishInterpolatedRemoteFrame()
        {
            if (!_playback.TrySample(out var interpolation))
            {
                return;
            }

            var projected = _projector.Project(in interpolation);
            _presentation.ApplyInterpolatedGatewaySnapshot(in projected);
        }

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
            _playback.Observe(sample);
        }

        SyncReconciliationReport IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.GetReconciliationReport()
        {
            return ShooterClientSyncStrategyMapping.ToReconciliationReport(this);
        }
    }
}
