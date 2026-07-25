#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterClientSession
    {
        private readonly ShooterPresentationSessionContext _presentationSession;
        private readonly ShooterPresentationFacade _presentation;
        private readonly IShooterClientSyncController _syncController;
        private readonly ShooterReliableBattleEventConsumer _reliableEvents = new ShooterReliableBattleEventConsumer();

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
            _syncController = ShooterClientSyncControllerFactory.Create(in assemblyOptions, runtime, _presentation, tickRate, gateway);
        }

        public event Action<WireReliableBattleEvent, ShooterEventSnapshot>? ReliableBattleEventCommitted;

        public NetworkSyncModel SyncModel => _syncController.SyncModel;

        public IShooterClientSyncController SyncController => _syncController;

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
            return _syncController.StartGame(in startGame);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            return _syncController.SubmitLocalInput(playerId, moveX, moveY, aimX, aimY, fire);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(in ShooterPlayerCommand command)
        {
            return _syncController.SubmitLocalInput(in command);
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _syncController.SubmitLocalInputToGatewayAsync(context, command, timeout, cancellationToken);
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _syncController.SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken);
        }

        public ShooterClientFrameTickResult Tick(float deltaTime)
        {
            return _syncController.Tick(deltaTime);
        }

        public ShooterClientFrameTickResult CatchUpToFrame(int targetFrame)
        {
            return _syncController.CatchUpToFrame(targetFrame);
        }

        public bool TryEnterCatchUp(int authoritativeFrame)
        {
            return _syncController.TryEnterCatchUp(authoritativeFrame);
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

                return applyResult;
            }

            try
            {
                var push = WireRoomGatewayBinary.Deserialize<WireReliableBattleEventPush>(payload);
                var result = _reliableEvents.Consume(in push);
                foreach (var envelope in result.CommittedEvents)
                {
                    var eventPayload = envelope.Payload ?? Array.Empty<byte>();
                    var battleEvent = ShooterStateSnapshotCodec.DeserializeEvent(eventPayload);
                    ReliableBattleEventCommitted?.Invoke(envelope, battleEvent);
                }
            }
            catch
            {
                _reliableEvents.Invalidate();
            }

            return ShooterSnapshotApplyResult.Ignored;
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
