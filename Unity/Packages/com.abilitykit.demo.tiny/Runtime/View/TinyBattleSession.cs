#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.View
{
    // Values are carried by WireRoomSubmitFrameInputRes.Reason.
    internal enum TinyFrameInputSubmitReason
    {
        None = 0,
        WorldMismatch = 1,
        NegativeFrame = 2,
        FrameAlreadyProcessed = 3,
        FrameTooFarAhead = 4,
        RateLimited = 100
    }

    public interface ITinyBattleGateway : IRoomGatewayBattleConnection
    {
        bool TryGetLatest(out WireStateSyncSnapshotPush snapshot);
        Task<WireRequestFullStateSyncRes> RequestFullSnapshotAsync(
            WireRequestFullStateSyncReq request, CancellationToken cancellationToken);
        bool TryDequeueFrame(out WireRoomFramePush frame);
        bool TryGetFrameOverflow(out int minimumRecoveryFrame);
        int FrameOverflowCount { get; }
        void PrepareFullSnapshotRecovery();
        void ResumeFrameStream();
        Task<WireRoomSubscribeFrameSyncRes> SubscribeFrameSyncAsync(
            WireRoomSubscribeFrameSyncReq request, CancellationToken cancellationToken);
        Task<WireRoomSubmitFrameInputRes> SubmitFrameInputAsync(
            WireRoomSubmitFrameInputReq request, CancellationToken cancellationToken);
        Task<WireSubmitBattleInputRes> SubmitAsync(
            string sessionToken, string battleId, ulong worldId, uint playerId,
            TinyInput input, ulong sequence, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Tiny 实时会话：房间生命周期与恢复纪律由 <see cref="RoomGatewayBattleSessionBase"/> 提供，
    /// 本类保留三模式同步策略（State 权威快照 / Frame 权威帧 / Hybrid 本地预测）与输入提交。
    /// </summary>
    public sealed class TinyBattleSession : RoomGatewayBattleSessionBase
    {
        private readonly ITinyBattleGateway _gateway;
        private ulong _inputSequence;
        private TinySyncMode? _pendingCreateMode;
        private TinyFrameReplication? _frameReplication;
        private TinySyncMode _syncMode;
        private TinySyncMode _subscribingMode;

        public TinyBattleSession(DemoMultiplayerLaunchRequest launch, ITinyBattleGateway gateway)
            : base(ToIdentity(launch), gateway, TinyAssetPreparation.PrepareAsync)
        {
            _gateway = gateway;
        }

        internal ulong WorldId => BattleWorldId;

        internal int SnapshotRequestCount => FullSnapshotRequestCount;

        public bool AwaitingBaseline => WaitingForBaseline;

        public TinySyncMode SyncMode => _syncMode;

        public TinySyncTelemetry Telemetry
        {
            get
            {
                var authoritativeFrame = _frameReplication?.AuthoritativeFrame ?? SnapshotCursor.LastFrame;
                return new TinySyncTelemetry(_syncMode, _gateway.ConnectionState,
                    !string.IsNullOrEmpty(BattleId), WaitingForBaseline, NeedsFullSnapshot,
                    authoritativeFrame,
                    _frameReplication?.PredictedFrame ?? authoritativeFrame,
                    _frameReplication?.LocalPredictionCount ?? 0,
                    _frameReplication?.RollbackCount ?? 0,
                    _frameReplication?.SnapshotCorrectionCount ?? 0,
                    _frameReplication?.RecoveryRequestCount ?? 0,
                    _gateway.FrameOverflowCount,
                    _frameReplication?.StateHash ?? 0);
            }
        }

        public bool NeedsFullSnapshot => WaitingForBaseline || _frameReplication?.NeedsFullSnapshot == true;

        public bool CanSubmitInput => !ConnectionUnavailable &&
            !string.IsNullOrEmpty(BattleId) && PlayerId != 0 && !WaitingForBaseline &&
            (Room == null || (Room.Phase == RoomGatewaySessionPhase.InBattle &&
                Room.BattleId == BattleId && Room.WorldId == BattleWorldId)) &&
            (_frameReplication == null ||
             (_frameReplication.HasBaseline && !_frameReplication.NeedsFullSnapshot));

        protected override void OnTick(float deltaTime)
        {
            if (_gateway.ConnectionState == RoomGatewayConnectionState.Reconnecting) Status = "Reconnecting";
            if (_gateway.ConnectionState == RoomGatewayConnectionState.Exhausted) Status = "Connection lost";
            if (_gateway.ConnectionState != RoomGatewayConnectionState.Connected || _frameReplication == null)
                return;
            if (_gateway.TryGetFrameOverflow(out var minimumRecoveryFrame))
            {
                _frameReplication.RequireFullSnapshot();
                SnapshotCursor.RequireAtLeast(minimumRecoveryFrame);
            }
            if (_gateway.TryGetLatest(out var baseline) && SnapshotCursor.CanAccept(in baseline))
            {
                try
                {
                    if (baseline.PayloadOpCode != TinyBattleStateCodec.PayloadOpCode)
                        throw new InvalidOperationException("Unexpected Tiny snapshot payload.");
                    _frameReplication.ApplyFullSnapshot(in baseline);
                    SnapshotCursor.TryAccept(in baseline);
                    ClearBaselineWaiting();
                    if (!_frameReplication.NeedsFullSnapshot) _gateway.ResumeFrameStream();
                }
                catch (ArgumentException) { _frameReplication.RequireFullSnapshot(); }
                catch (InvalidOperationException) { _frameReplication.RequireFullSnapshot(); }
            }
            if (!_frameReplication.HasBaseline || _frameReplication.NeedsFullSnapshot) return;
            while (_gateway.TryDequeueFrame(out var frame))
                _frameReplication.ApplyFrame(in frame, BattleWorldId);
        }

        public Task CreateRoomAsync(CancellationToken cancellationToken) =>
            CreateRoomAsync(TinySyncMode.State, cancellationToken);

        public async Task CreateRoomAsync(TinySyncMode mode, CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(typeof(TinySyncMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (_pendingCreateMode.HasValue && _pendingCreateMode.Value != mode)
                throw new InvalidOperationException("Retry the pending room creation with its original sync mode.");
            _pendingCreateMode = mode;
            await CreateRoomCoreAsync(cancellationToken);
        }

        public bool TryGetNewSnapshot(out WireStateSyncSnapshotPush snapshot)
        {
            ThrowIfDisposed();
            if (ConnectionUnavailable)
            {
                snapshot = default;
                return false;
            }
            if (_frameReplication != null)
                return _frameReplication.TryGetPresentation(BattleWorldId, out snapshot);
            if (_gateway.ConnectionState == RoomGatewayConnectionState.Connected &&
                !string.IsNullOrEmpty(BattleId) && _gateway.TryGetLatest(out snapshot) &&
                snapshot.Actors != null &&
                SnapshotCursor.TryAccept(in snapshot))
            {
                ClearBaselineWaiting();
                return true;
            }
            snapshot = default;
            return false;
        }

        public async Task SubmitInputAsync(TinyInput input, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (ConnectionUnavailable)
                throw new InvalidOperationException("Room connection is not ready.");
            if (string.IsNullOrEmpty(BattleId) || PlayerId == 0)
                throw new InvalidOperationException("Tiny battle is not subscribed.");
            if (_frameReplication != null)
            {
                var replication = _frameReplication;
                if (!replication.HasBaseline || replication.NeedsFullSnapshot)
                    throw new InvalidOperationException("Tiny frame baseline is pending.");
                var frame = replication.ReserveInputFrame();
                var predicted = _syncMode == TinySyncMode.Hybrid;
                if (predicted)
                    replication.PredictLocalInput(frame, PlayerId, input);
                try
                {
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        var roomId = RoomId;
                        var battleId = BattleId;
                        var worldId = BattleWorldId;
                        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        deadline.CancelAfter(RequestTimeout);
                        WireRoomSubmitFrameInputRes response;
                        try
                        {
                            response = await _gateway.SubmitFrameInputAsync(new WireRoomSubmitFrameInputReq
                            {
                                SessionToken = Identity.SessionToken,
                                RoomId = roomId,
                                BattleId = battleId,
                                WorldId = worldId,
                                Frame = frame,
                                PlayerId = PlayerId,
                                InputOpCode = TinyBattle.InputOpCode,
                                Payload = input.Encode()
                            }, deadline.Token);
                            deadline.Token.ThrowIfCancellationRequested();
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            throw new TimeoutException("Tiny frame input request timed out.");
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!ReferenceEquals(replication, _frameReplication) ||
                            roomId != RoomId || battleId != BattleId || worldId != BattleWorldId)
                            throw new InvalidOperationException("Tiny battle changed during input submission.");

                        var reason = (TinyFrameInputSubmitReason)response.Reason;
                        if (response.Accepted || reason == TinyFrameInputSubmitReason.FrameAlreadyProcessed)
                            replication.ObserveServerFrame(response.ServerFrame);
                        if (response.Accepted) return;
                        if (!predicted && reason == TinyFrameInputSubmitReason.FrameAlreadyProcessed &&
                            attempt < 2)
                        {
                            frame = replication.ReserveInputFrame();
                            continue;
                        }
                        if (reason == TinyFrameInputSubmitReason.FrameTooFarAhead)
                            replication.RequireFullSnapshot();
                        throw new InvalidOperationException("Tiny frame input rejected: " + reason +
                            " (server frame " + response.ServerFrame + ").");
                    }
                }
                catch
                {
                    if (predicted && ReferenceEquals(replication, _frameReplication))
                        replication.RequireFullSnapshot();
                    throw;
                }
                return;
            }
            using var stateDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stateDeadline.CancelAfter(RequestTimeout);
            WireSubmitBattleInputRes result;
            try
            {
                result = await _gateway.SubmitAsync(Identity.SessionToken, BattleId, BattleWorldId,
                    PlayerId, input, ++_inputSequence, stateDeadline.Token);
                stateDeadline.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Tiny state input request timed out.");
            }
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }

        public Task RequestFullSnapshotAsync(string reason, CancellationToken cancellationToken) =>
            RequestFullSnapshotCoreAsync(reason, cancellationToken);

        protected override RoomGatewayCreateRequest CreateRoomRequest(string commandId) => new(
            Identity.SessionToken, Identity.Region, Identity.ServerId,
            "tiny", "Tiny Battle", false, 2,
            TinySyncModeConfiguration.CreateTags(_pendingCreateMode ?? TinySyncMode.State), commandId);

        protected override void OnCreateRoomRejected() => _pendingCreateMode = null;

        protected override void ValidateLaunchManifest(RoomGatewaySnapshot room) =>
            TinyAssetPreparation.Validate(room);

        protected override string ResolveSubscriptionModelName(RoomGatewaySnapshot room)
        {
            _subscribingMode = TinySyncModeConfiguration.FromProfile(room.SyncCapabilities?.ProfileName ?? string.Empty);
            return _subscribingMode == TinySyncMode.State ? NetworkSyncModel.AuthoritativeInterpolation.ToString()
                : _subscribingMode == TinySyncMode.Frame ? NetworkSyncModel.Lockstep.ToString()
                : NetworkSyncModel.HybridHeroPrediction.ToString();
        }

        protected override NetworkSyncProfile ResolveSubscriptionProfile(RoomGatewaySnapshot room) =>
            _subscribingMode == TinySyncMode.Hybrid
                ? NetworkSyncProfiles.FrameWithSnapshotRecovery
                : _subscribingMode == TinySyncMode.Frame ? NetworkSyncProfiles.LockstepWithSnapshotRecovery
                : NetworkSyncProfileRegistry.Resolve(_subscribingMode == TinySyncMode.State
                    ? NetworkSyncModel.AuthoritativeInterpolation
                    : NetworkSyncModel.HybridHeroPrediction);

        protected override async Task SubscribeBattleChannelsAsync(
            RoomGatewaySnapshot room, string roomId, CancellationToken cancellationToken)
        {
            if (_subscribingMode == TinySyncMode.State) return;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(RequestTimeout);
            WireRoomSubscribeFrameSyncRes frameSubscription;
            try
            {
                frameSubscription = await _gateway.SubscribeFrameSyncAsync(
                    new WireRoomSubscribeFrameSyncReq
                    {
                        SessionToken = Identity.SessionToken,
                        RoomId = roomId,
                        BattleId = room.BattleId,
                        WorldId = room.WorldId
                    }, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Tiny frame subscription timed out.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!frameSubscription.Success)
                throw new InvalidOperationException(frameSubscription.Message);
        }

        protected override void OnBattleBound(RoomGatewaySnapshot room, uint playerId,
            string previousBattleId, ulong previousWorldId)
        {
            if (previousBattleId != room.BattleId || previousWorldId != room.WorldId)
                _inputSequence = 0;
            _syncMode = _subscribingMode;
            _frameReplication = _syncMode == TinySyncMode.State ? null : new TinyFrameReplication();
            Status = "Battle (" + _syncMode + ")";
        }

        protected override void OnRoomUnbound()
        {
            _inputSequence = 0;
            _frameReplication = null;
            _syncMode = TinySyncMode.State;
            _pendingCreateMode = null;
        }

        protected override Task<WireRequestFullStateSyncRes> SendFullSnapshotRequestAsync(
            WireRequestFullStateSyncReq request, CancellationToken cancellationToken) =>
            _gateway.RequestFullSnapshotAsync(request, cancellationToken);

        protected override void OnBeforeFullSnapshotRequest() => _gateway.PrepareFullSnapshotRecovery();

        protected override string SyncNotNegotiatedMessage => "Tiny sync capabilities were not negotiated.";

        protected override string PlayerSlotMissingMessage => "Tiny player slot is missing.";

        protected override string BattleNotSubscribedMessage => "Tiny battle is not subscribed.";

        protected override string FullSnapshotTimeoutMessage => "Tiny snapshot request timed out.";

        private static RoomGatewaySessionIdentity ToIdentity(DemoMultiplayerLaunchRequest launch) =>
            new(launch.SessionToken, launch.Region, launch.ServerId, launch.AccountId, launch.Timeout);
    }
}
