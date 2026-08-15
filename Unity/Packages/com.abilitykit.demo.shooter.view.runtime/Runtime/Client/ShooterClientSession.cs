#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterClientSession : INetworkSessionRecoverySignalSink
    {
        private readonly ShooterPresentationSessionContext _presentationSession;
        private readonly ShooterPresentationFacade _presentation;
        private readonly IShooterClientSyncController _syncController;
        private readonly NetworkSyncSessionDescriptor _syncSession;
        private readonly ShooterReliableBattleEventConsumer _reliableEvents;
        private readonly NetworkSessionRecoveryCoordinator _sessionRecovery;

        public ShooterClientSession(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation, int tickRate)
            : this(runtime, presentation, tickRate, (ShooterGatewaySnapshotDecoder?)null)
        {
        }

        public ShooterClientSession(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation, int tickRate, ShooterGatewaySnapshotDecoder? decoder)
            : this(runtime, presentation, tickRate, decoder, null)
        {
        }

        public ShooterClientSession(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation, int tickRate, ShooterGatewaySnapshotDecoder? decoder, IShooterRoomGatewayClient? gateway)
            : this(runtime, ShooterPresentationSessionContext.CreateFromFacade(presentation), tickRate, decoder, gateway)
        {
        }

        public ShooterClientSession(IShooterBattleRuntimePort runtime, ShooterPresentationSessionContext presentationSession, int tickRate)
            : this(runtime, presentationSession, tickRate, (ShooterGatewaySnapshotDecoder?)null)
        {
        }

        public ShooterClientSession(IShooterBattleRuntimePort runtime, ShooterPresentationSessionContext presentationSession, int tickRate, ShooterGatewaySnapshotDecoder? decoder)
            : this(runtime, presentationSession, tickRate, decoder, null)
        {
        }

        public ShooterClientSession(IShooterBattleRuntimePort runtime, ShooterPresentationSessionContext presentationSession, int tickRate, ShooterGatewaySnapshotDecoder? decoder, IShooterRoomGatewayClient? gateway)
            : this(runtime, presentationSession, tickRate, decoder, gateway, ShooterClientSyncControllerFactory.DefaultSyncModel)
        {
        }

        public ShooterClientSession(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            NetworkSyncModel syncModel)
            : this(runtime, presentationSession, tickRate, decoder, gateway, syncModel, interpolationConfig: null)
        {
        }

        /// <summary>
        /// 创建客户端会话，并可选为 <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 模型提供
        /// <see cref="InterpolationConfig"/>。该配置会被不做插值的模型忽略；省略时插值模型会回退到
        /// <see cref="InterpolationConfig.Default"/>。
        /// </summary>
        public ShooterClientSession(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            NetworkSyncModel syncModel,
            InterpolationConfig? interpolationConfig)
            : this(runtime, presentationSession, tickRate, new ShooterClientSyncAssemblyOptions(syncModel, decoder, interpolationConfig), gateway)
        {
        }

        public ShooterClientSession(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationSessionContext presentationSession,
            int tickRate,
            in ShooterClientSyncAssemblyOptions assemblyOptions,
            IShooterRoomGatewayClient? gateway)
        {
            _presentationSession = presentationSession ?? throw new ArgumentNullException(nameof(presentationSession));
            _presentation = _presentationSession.Presentation;
            var syncSession = ShooterClientSyncControllerFactory.CreateSession(
                in assemblyOptions,
                runtime,
                _presentation,
                tickRate,
                gateway);
            _syncController = syncSession.Controller;
            _syncSession = syncSession.Descriptor;
            _reliableEvents = new ShooterReliableBattleEventConsumer(
                _syncSession,
                checkpointStore: assemblyOptions.ReliableEventCheckpointStore,
                lifecycleOptions: assemblyOptions.ReliableEventCheckpointLifecycleOptions);
            _sessionRecovery = new NetworkSessionRecoveryCoordinator(
                assemblyOptions.SessionRecoveryOptions);
            _reliableEvents.CheckpointLifecycleFailure += HandleCheckpointLifecycleFailure;
        }

        public event Action<WireReliableBattleEvent, ShooterEventSnapshot>? ReliableBattleEventCommitted;

        public NetworkSyncModel SyncModel => _syncController.SyncModel;

        public IShooterClientSyncController SyncController => _syncController;

        /// <summary>本次客户端同步会话经过本地预检和远端能力协商后的诊断描述。</summary>
        public NetworkSyncSessionDescriptor SyncSession => _syncSession;

        public bool IsStarted => _syncController.IsStarted;

        public int CurrentFrame => _syncController.CurrentFrame;

        public int GatewayInputFrame => _syncController.GatewayInputFrame;

        public ShooterPresentationSessionContext PresentationSession => _presentationSession;

        public ShooterPresentationFacade Presentation => _presentation;

        public ShooterClientFrameSyncController FrameSync => _syncController.FrameSync;

        public ShooterClientInputCoordinator InputCoordinator => _syncController.InputCoordinator;

        public ShooterClientReconciliationResult LastReconciliationResult => _syncController.LastReconciliationResult;

        public ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics => _syncController.FrameworkSnapshotPipelineDiagnostics;

        public bool NeedsFullSnapshotResync => _syncController.NeedsFullSnapshotResync;

        public ShooterClientRecoveryState RecoveryState => _syncController.RecoveryState;

        public AbilityKit.Network.Runtime.Sync.FastReconnectPhase FastReconnectPhase => _syncController.FastReconnectPhase;

        public System.Collections.Generic.IReadOnlyList<AbilityKit.Network.Runtime.Sync.SyncHealthEvent> LastFastReconnectHealthEvents
            => _syncController.LastFastReconnectHealthEvents;

        public ShooterClientResyncReason LastResyncReason => _syncController.LastResyncReason;

        public int LastResyncClientFrame => _syncController.LastResyncClientFrame;

        public int LastResyncAuthoritativeFrame => _syncController.LastResyncAuthoritativeFrame;

        public uint LastResyncClientStateHash => _syncController.LastResyncClientStateHash;

        public uint LastResyncAuthoritativeStateHash => _syncController.LastResyncAuthoritativeStateHash;

        public bool HasGateway => _syncController.HasGateway;

        public string ReliableEventEpoch => _reliableEvents.Epoch;

        public long LastReliableEventAck => _reliableEvents.LastAcknowledgedSequence;

        public bool NeedsReliableEventResync => _reliableEvents.RequiresResync;

        /// <summary>当前统一会话恢复决策；业务层根据该值执行请求快照、重建会话或退出等动作。</summary>
        public NetworkSessionRecoveryDecision RecoveryDecision => _sessionRecovery.CurrentDecision;

        /// <summary>统一会话恢复协调器采纳新决策时触发。</summary>
        public event Action<NetworkSessionRecoveryDecision>? RecoveryDecisionPublished
        {
            add => _sessionRecovery.DecisionPublished += value;
            remove => _sessionRecovery.DecisionPublished -= value;
        }

        /// <summary>获取统一会话恢复信号与决策的累计诊断。</summary>
        public NetworkSessionRecoveryDiagnostics SessionRecoveryDiagnostics =>
            _sessionRecovery.GetDiagnostics();

        /// <summary>供同一会话的业务执行层复用统一恢复协调器。</summary>
        internal NetworkSessionRecoveryCoordinator SessionRecoveryCoordinator => _sessionRecovery;

        /// <summary>获取可靠事件检查点生命周期的累计诊断。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics ReliableEventCheckpointLifecycleDiagnostics =>
            _reliableEvents.CheckpointLifecycleDiagnostics;

        /// <summary>可靠事件检查点 flush 失败且完成诊断记录后触发。</summary>
        public event Action<ReliableEventCheckpointLifecycleFailure>? ReliableEventCheckpointLifecycleFailure
        {
            add => _reliableEvents.CheckpointLifecycleFailure += value;
            remove => _reliableEvents.CheckpointLifecycleFailure -= value;
        }

        /// <summary>等待可靠事件检查点写入完成，供断线、暂停和退出生命周期调用。</summary>
        public Task FlushReliableEventCheckpointsAsync(CancellationToken cancellationToken = default)
        {
            return _reliableEvents.FlushCheckpointStoreAsync(cancellationToken);
        }

        /// <summary>按指定生命周期原因等待可靠事件检查点写入完成。</summary>
        public Task<ReliableEventCheckpointFlushResult> FlushReliableEventCheckpointsAsync(
            ReliableEventCheckpointFlushTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            return _reliableEvents.FlushCheckpointStoreAsync(trigger, cancellationToken);
        }

        /// <summary>
        /// 将连接层或项目扩展模块产生的恢复信号交给框架协调器，不在 SDK 内直接执行业务动作。
        /// </summary>
        public bool TryReportRecoverySignal(
            in NetworkSessionRecoverySignal signal,
            out NetworkSessionRecoveryDecision decision)
        {
            return TryReport(in signal, out decision);
        }

        public bool TryReport(
            in NetworkSessionRecoverySignal signal,
            out NetworkSessionRecoveryDecision decision)
        {
            return _sessionRecovery.TryReport(in signal, out decision);
        }

        /// <summary>检查当前快照与可靠事件状态，并返回协调后的最高优先级恢复决策。</summary>
        public NetworkSessionRecoveryDecision EvaluateRecoveryDecision()
        {
            EvaluateRecoverySignals(publishRecovered: false);
            return _sessionRecovery.CurrentDecision;
        }

        /// <summary>
        /// 当当前同步模型会插值远端状态（即 <see cref="NetworkSyncModel.AuthoritativeInterpolation"/>）时，
        /// 读取插值播放健康状态。对于不做插值的模型返回 <c>false</c>，并保持 <paramref name="diagnostics"/> 为默认值。
        /// </summary>
        public bool TryGetInterpolationDiagnostics(out InterpolationDiagnostics diagnostics)
        {
            if (_syncController is IInterpolationDiagnosticsProvider provider)
            {
                diagnostics = provider.GetInterpolationDiagnostics();
                return true;
            }

            diagnostics = default;
            return false;
        }

        public bool StartGame(in ShooterStartGamePayload startGame)
        {
            var started = _syncController.StartGame(in startGame);
            if (started) _sessionRecovery.Reset();
            return started;
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            return _syncController.SubmitLocalInput(playerId, moveX, moveY, aimX, aimY, fire);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(in ShooterPlayerCommand command)
        {
            return _syncController.SubmitLocalInput(in command);
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _syncController.SubmitLocalInputToGatewayAsync(
                context,
                command,
                timeout,
                cancellationToken);
            EvaluateRecoverySignals(publishRecovered: false);
            return result;
        }

        public async Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _syncController.SubmitAcceptedInputToGatewayAsync(
                context,
                local,
                timeout,
                cancellationToken);
            EvaluateRecoverySignals(publishRecovered: false);
            return result;
        }

        public ShooterClientFrameTickResult Tick(float deltaTime)
        {
            var result = _syncController.Tick(deltaTime);
            EvaluateRecoverySignals(publishRecovered: false);
            return result;
        }

        public ShooterClientFrameTickResult CatchUpToFrame(int targetFrame)
        {
            var result = _syncController.CatchUpToFrame(targetFrame);
            EvaluateRecoverySignals(publishRecovered: false);
            return result;
        }

        public bool TryEnterCatchUp(int authoritativeFrame)
        {
            var entered = _syncController.TryEnterCatchUp(authoritativeFrame);
            EvaluateRecoverySignals(publishRecovered: false);
            return entered;
        }

        public Task<ShooterGatewayFullStateSyncRequestResult> RequestFullSnapshotResyncAsync(
            IShooterRoomGatewayRoomClient roomClient,
            ShooterGatewayFullStateSyncRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (roomClient == null) throw new ArgumentNullException(nameof(roomClient));
            return roomClient.RequestFullStateSyncAsync(request, timeout, cancellationToken);
        }

        public ShooterSnapshotApplyResult ApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            if (opCode != RoomGatewayOpCodes.ReliableBattleEventsPushed)
            {
                WireStateSyncSnapshotPush snapshot = default;
                var isSnapshotPush = opCode == RoomGatewayOpCodes.SnapshotPushed
                    || opCode == RoomGatewayOpCodes.DeltaSnapshotPushed;
                if (isSnapshotPush)
                {
                    snapshot = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
                }

                var applyResult = _syncController.ApplyGatewayPush(opCode, payload);
                if (isSnapshotPush
                    && snapshot.IsFullSnapshot
                    && IsReliableEventBaseline(applyResult))
                {
                    _reliableEvents.TryApplyFullSnapshotBaseline(snapshot.EventWatermark);
                }

                EvaluateRecoverySignals(
                    publishRecovered: isSnapshotPush && snapshot.IsFullSnapshot);

                return applyResult;
            }

            try
            {
                var push = WireRoomGatewayBinary.Deserialize<WireReliableBattleEventPush>(payload);
                _reliableEvents.Consume(in push, envelope =>
                {
                    var eventPayload = envelope.Payload ?? Array.Empty<byte>();
                    var battleEvent = ShooterStateSnapshotCodec.DeserializeEvent(eventPayload);
                    ReliableBattleEventCommitted?.Invoke(envelope, battleEvent);
                });
            }
            catch
            {
                _reliableEvents.Invalidate();
            }

            EvaluateRecoverySignals(publishRecovered: false);

            return ShooterSnapshotApplyResult.Ignored;
        }

        private void EvaluateRecoverySignals(bool publishRecovered)
        {
            if (NeedsFullSnapshotResync)
            {
                var signal = new NetworkSessionRecoverySignal(
                    NetworkSessionRecoverySignalKind.SnapshotResyncRequired,
                    SyncHealthSeverity.Error,
                    CurrentFrame,
                    correlationContext: _reliableEvents.BattleId,
                    detail: LastResyncReason.ToString());
                _sessionRecovery.TryReport(in signal, out _);
            }

            if (NeedsReliableEventResync)
            {
                var signal = new NetworkSessionRecoverySignal(
                    NetworkSessionRecoverySignalKind.ReliableEventResyncRequired,
                    SyncHealthSeverity.Error,
                    CurrentFrame,
                    correlationContext: _reliableEvents.BattleId,
                    detail: "可靠事件游标要求权威基线。");
                _sessionRecovery.TryReport(in signal, out _);
            }

            if (!publishRecovered || NeedsFullSnapshotResync || NeedsReliableEventResync)
            {
                return;
            }

            var current = _sessionRecovery.CurrentDecision;
            if (!current.HasAction ||
                (current.Signal.Kind != NetworkSessionRecoverySignalKind.SnapshotResyncRequired &&
                 current.Signal.Kind != NetworkSessionRecoverySignalKind.ReliableEventResyncRequired))
            {
                return;
            }

            var recovered = new NetworkSessionRecoverySignal(
                NetworkSessionRecoverySignalKind.Recovered,
                SyncHealthSeverity.Info,
                CurrentFrame,
                correlationContext: _reliableEvents.BattleId,
                detail: "完整权威快照已经恢复同步与可靠事件基线。");
            _sessionRecovery.TryReport(in recovered, out _);
        }

        private void HandleCheckpointLifecycleFailure(
            ReliableEventCheckpointLifecycleFailure failure)
        {
            var diagnostics = _reliableEvents.CheckpointLifecycleDiagnostics;
            var kind = diagnostics.CircuitState == ReliableEventCheckpointCircuitState.Open ||
                       failure.Exception is ReliableEventCheckpointCircuitOpenException
                ? NetworkSessionRecoverySignalKind.CheckpointCircuitOpen
                : NetworkSessionRecoverySignalKind.CheckpointFlushFailed;
            var signal = new NetworkSessionRecoverySignal(
                kind,
                SyncHealthSeverity.Error,
                CurrentFrame,
                failure.Exception,
                failure.Trigger.ToString(),
                "可靠事件检查点生命周期 flush 失败。");
            _sessionRecovery.TryReport(in signal, out _);
        }

        private static bool IsReliableEventBaseline(ShooterSnapshotApplyResult result)
        {
            return result == ShooterSnapshotApplyResult.AppliedActorSnapshot
                || result == ShooterSnapshotApplyResult.AppliedPackedSnapshot
                || result == ShooterSnapshotApplyResult.IgnoredStaleSnapshot;
        }
    }

    public readonly struct ShooterClientGatewayInputSubmitResult
    {
        public readonly ShooterClientInputSubmitResult Local;
        public readonly ShooterGatewayBattleInputResult Remote;

        public ShooterClientGatewayInputSubmitResult(in ShooterClientInputSubmitResult local, in ShooterGatewayBattleInputResult remote)
        {
            Local = local;
            Remote = remote;
        }
    }

    public readonly struct ShooterClientInputSubmitResult
    {
        public readonly int AcceptedInputs;
        public readonly int RequestedFrame;
        public readonly ShooterInputPacket Packet;
        public readonly long SubmissionId;

        public ShooterClientInputSubmitResult(int acceptedInputs, int requestedFrame, in ShooterInputPacket packet)
            : this(acceptedInputs, requestedFrame, in packet, 0L)
        {
        }

        public ShooterClientInputSubmitResult(
            int acceptedInputs,
            int requestedFrame,
            in ShooterInputPacket packet,
            long submissionId)
        {
            AcceptedInputs = acceptedInputs;
            RequestedFrame = requestedFrame;
            Packet = packet;
            SubmissionId = submissionId;
        }

        public ShooterClientInputSubmitResult WithRequestedFrame(int requestedFrame)
        {
            return new ShooterClientInputSubmitResult(AcceptedInputs, requestedFrame, in Packet, SubmissionId);
        }
    }
}
