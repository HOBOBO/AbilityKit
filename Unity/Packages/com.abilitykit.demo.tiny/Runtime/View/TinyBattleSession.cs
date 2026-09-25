#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
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

    public interface ITinyBattleGateway : IDisposable
    {
        IRoomGatewaySessionClient Rooms { get; }
        RoomGatewayConnectionState ConnectionState { get; }
        long ConnectionGeneration { get; }
        void Tick(float deltaTime);
        bool CompleteRestore(long generation);
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

    /// <summary>Project-specific room policy and battle binding over the shared Gateway session flow.</summary>
    public sealed class TinyBattleSession : IDisposable
    {
        private readonly DemoMultiplayerLaunchRequest _launch;
        private readonly ITinyBattleGateway _gateway;
        private readonly RoomGatewaySessionFlow _flow;
        private readonly RoomGatewayLoadingStage _loading;
        private string _roomId = string.Empty;
        private string _battleId = string.Empty;
        private ulong _worldId;
        private uint _playerId;
        private ulong _inputSequence;
        private readonly RoomGatewayCommandIdLedger _commandIds = new RoomGatewayCommandIdLedger();
        private TinySyncMode? _pendingCreateMode;
        private readonly RoomGatewayFullSnapshotCursor _snapshotCursor = new RoomGatewayFullSnapshotCursor();
        private TinyFrameReplication? _frameReplication;
        private TinySyncMode _syncMode;
        private long _observedConnectionGeneration;
        private long _bindingRevision;
        private bool _awaitingBaseline;
        private int _snapshotRequestCount;
        private bool _disposed;
        internal TimeSpan RequestTimeout => _launch.Timeout > TimeSpan.Zero
            ? _launch.Timeout : TimeSpan.FromSeconds(10);

        public TinyBattleSession(DemoMultiplayerLaunchRequest launch, ITinyBattleGateway gateway)
        {
            _launch = launch ?? throw new ArgumentNullException(nameof(launch));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _flow = new RoomGatewaySessionFlow(gateway.Rooms);
            _loading = new RoomGatewayLoadingStage(_flow, _commandIds, TinyAssetPreparation.PrepareAsync);
        }

        public string RoomId => _roomId;
        public string BattleId => _battleId;
        internal ulong WorldId => _worldId;
        public uint PlayerId => _playerId;
        public bool CanStart => Room?.CanStart == true && Room.OwnerAccountId == _launch.AccountId;
        public RoomGatewaySnapshot? Room { get; private set; }
        public string Status { get; private set; } = "Connected";
        public bool NeedsConnectionRestore =>
            _gateway.ConnectionState == RoomGatewayConnectionState.RestoreRequired &&
            _gateway.ConnectionGeneration != _observedConnectionGeneration;
        public bool ConnectionUnavailable =>
            _gateway.ConnectionState != RoomGatewayConnectionState.Connected;
        public bool AwaitingBaseline => _awaitingBaseline;
        public bool NeedsFullSnapshot => _awaitingBaseline || _frameReplication?.NeedsFullSnapshot == true;
        internal int SnapshotRequestCount => _snapshotRequestCount;
        public TinySyncMode SyncMode => _syncMode;
        public TinySyncTelemetry Telemetry
        {
            get
            {
                var authoritativeFrame = _frameReplication?.AuthoritativeFrame ?? _snapshotCursor.LastFrame;
                return new TinySyncTelemetry(_syncMode, _gateway.ConnectionState,
                    !string.IsNullOrEmpty(_battleId), _awaitingBaseline, NeedsFullSnapshot,
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
        public bool CanSubmitInput => !ConnectionUnavailable &&
            !string.IsNullOrEmpty(_battleId) && _playerId != 0 && !_awaitingBaseline &&
            (Room == null || (Room.Phase == RoomGatewaySessionPhase.InBattle &&
                Room.BattleId == _battleId && Room.WorldId == _worldId)) &&
            (_frameReplication == null ||
             (_frameReplication.HasBaseline && !_frameReplication.NeedsFullSnapshot));

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();
            _gateway.Tick(deltaTime);
            if (_gateway.ConnectionState == RoomGatewayConnectionState.Reconnecting) Status = "Reconnecting";
            if (_gateway.ConnectionState == RoomGatewayConnectionState.Exhausted) Status = "Connection lost";
            if (_gateway.ConnectionState != RoomGatewayConnectionState.Connected || _frameReplication == null)
                return;
            if (_gateway.TryGetFrameOverflow(out var minimumRecoveryFrame))
            {
                _frameReplication.RequireFullSnapshot();
                _snapshotCursor.RequireAtLeast(minimumRecoveryFrame);
            }
            if (_gateway.TryGetLatest(out var baseline) && _snapshotCursor.CanAccept(in baseline))
            {
                try
                {
                    if (baseline.PayloadOpCode != TinyBattleStateCodec.PayloadOpCode)
                        throw new InvalidOperationException("Unexpected Tiny snapshot payload.");
                    _frameReplication.ApplyFullSnapshot(in baseline);
                    _snapshotCursor.TryAccept(in baseline);
                    _awaitingBaseline = false;
                    if (!_frameReplication.NeedsFullSnapshot) _gateway.ResumeFrameStream();
                }
                catch (ArgumentException) { _frameReplication.RequireFullSnapshot(); }
                catch (InvalidOperationException) { _frameReplication.RequireFullSnapshot(); }
            }
            if (!_frameReplication.HasBaseline || _frameReplication.NeedsFullSnapshot) return;
            while (_gateway.TryDequeueFrame(out var frame))
                _frameReplication.ApplyFrame(in frame, _worldId);
        }

        public async Task RestoreAsync(CancellationToken cancellationToken)
        {
            await RestoreCoreAsync(cancellationToken);
        }

        private async Task<bool> RestoreCoreAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            var result = await _flow.RestoreWithoutPlayerIdAsync(
                _launch.SessionToken, _launch.Region, _launch.ServerId,
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation, revision)) return false;
            if (result.RestoreStatus == RoomGatewaySessionRestoreStatus.InvalidSession)
                throw new UnauthorizedAccessException(result.Message);
            if (result.CanRetry)
                throw new TimeoutException(result.Message);
            if (result.RestoreStatus == RoomGatewaySessionRestoreStatus.Failed)
                throw new InvalidOperationException(result.Message);
            if (!string.IsNullOrWhiteSpace(result.RoomId) &&
                result.NextStep != RoomGatewayStagedRestoreNextStep.None)
            {
                if (_roomId != result.RoomId) BindRoom(result.RoomId);
                SetRoom(result.Snapshot);
                revision = _bindingRevision;
                Status = result.Phase.ToString();
                if (result.Phase == RoomGatewaySessionPhase.InBattle && result.Snapshot != null)
                {
                    await SubscribeAsync(result.Snapshot, cancellationToken);
                    if (!ReferenceEquals(Room, result.Snapshot) ||
                        _battleId != result.Snapshot.BattleId || _worldId != result.Snapshot.WorldId)
                        return false;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_roomId)) BindRoom(string.Empty);
                revision = _bindingRevision;
                Status = result.RestoreStatus.ToString();
            }
            return IsCurrent(generation, revision);
        }

        public async Task RecoverConnectionAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!NeedsConnectionRestore) return;
            var generation = _gateway.ConnectionGeneration;
            if (!await RestoreCoreAsync(cancellationToken)) return;
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || generation != _gateway.ConnectionGeneration) return;
            if (_gateway.CompleteRestore(generation)) _observedConnectionGeneration = generation;
        }

        public async Task CreateRoomAsync(CancellationToken cancellationToken)
        {
            await CreateRoomAsync(TinySyncMode.State, cancellationToken);
        }

        public async Task CreateRoomAsync(TinySyncMode mode, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfConnectionUnavailable();
            if (!Enum.IsDefined(typeof(TinySyncMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (!string.IsNullOrEmpty(_roomId))
                throw new InvalidOperationException("Leave the current room before creating another.");
            if (_pendingCreateMode.HasValue && _pendingCreateMode.Value != mode)
                throw new InvalidOperationException("Retry the pending room creation with its original sync mode.");
            _pendingCreateMode = mode;
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            var currentRoomId = _roomId;
            var result = await _gateway.Rooms.CreateRoomAsync(new RoomGatewayCreateRequest(
                _launch.SessionToken, _launch.Region, _launch.ServerId, "tiny", "Tiny Battle", false, 2,
                TinySyncModeConfiguration.CreateTags(mode), _commandIds.GetOrCreate("create-room")),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, currentRoomId)) return;
            if (!result.Success)
            {
                _commandIds.Complete("create-room");
                _pendingCreateMode = null;
                throw new InvalidOperationException(result.Message);
            }
            await JoinRoomAsync(result.RoomId, cancellationToken);
        }

        public async Task JoinRoomAsync(string roomId, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfConnectionUnavailable();
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Room ID is required.", nameof(roomId));
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            var currentRoomId = _roomId;
            var result = await _gateway.Rooms.JoinRoomAsync(new RoomGatewayJoinRequest(
                _launch.SessionToken, _launch.Region, _launch.ServerId, roomId.Trim()),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, currentRoomId)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            BindRoom(result.RoomId ?? roomId.Trim());
            Status = "Joined";
        }

        public async Task SetReadyAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfConnectionUnavailable();
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var result = await _gateway.Rooms.SetReadyAsync(new RoomGatewayReadyRequest(
                _launch.SessionToken, roomId, true), timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, roomId)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            Status = "Ready";
        }

        public async Task BeginLoadingAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfConnectionUnavailable();
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var result = await _flow.BeginLoadingAsync(new RoomGatewayBeginLoadingRequest(
                _launch.SessionToken, roomId, null, _commandIds.GetOrCreate("begin-loading")),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, roomId)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            _commandIds.Complete("begin-loading");
            Status = "Loading";
        }

        public async Task PollAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(_roomId)) return;
            var roomId = _roomId;
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            var result = await _flow.GetSnapshotAsync(_launch.SessionToken, roomId,
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation, revision) || roomId != _roomId) return;
            if (!result.Success || result.Snapshot == null)
                throw new InvalidOperationException(result.Message);
            var room = result.Snapshot;
            SetRoom(room);
            Status = room.Phase.ToString();
            revision = _bindingRevision;
            await _loading.AdvanceAsync(_launch.SessionToken, room, cancellationToken,
                RequestTimeout);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation, revision) || !ReferenceEquals(Room, room)) return;
            if (room.Phase == RoomGatewaySessionPhase.InBattle &&
                (_battleId != room.BattleId || _worldId != room.WorldId))
                await SubscribeAsync(room, cancellationToken);
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
                return _frameReplication.TryGetPresentation(_worldId, out snapshot);
            if (_gateway.ConnectionState == RoomGatewayConnectionState.Connected &&
                !string.IsNullOrEmpty(_battleId) && _gateway.TryGetLatest(out snapshot) &&
                snapshot.Actors != null &&
                _snapshotCursor.TryAccept(in snapshot))
            {
                _awaitingBaseline = false;
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
            if (string.IsNullOrEmpty(_battleId) || _playerId == 0)
                throw new InvalidOperationException("Tiny battle is not subscribed.");
            if (_frameReplication != null)
            {
                var replication = _frameReplication;
                if (!replication.HasBaseline || replication.NeedsFullSnapshot)
                    throw new InvalidOperationException("Tiny frame baseline is pending.");
                var frame = replication.ReserveInputFrame();
                var predicted = _syncMode == TinySyncMode.Hybrid;
                if (predicted)
                    replication.PredictLocalInput(frame, _playerId, input);
                try
                {
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        var roomId = _roomId;
                        var battleId = _battleId;
                        var worldId = _worldId;
                        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        deadline.CancelAfter(RequestTimeout);
                        WireRoomSubmitFrameInputRes response;
                        try
                        {
                            response = await _gateway.SubmitFrameInputAsync(new WireRoomSubmitFrameInputReq
                            {
                                SessionToken = _launch.SessionToken,
                                RoomId = roomId,
                                BattleId = battleId,
                                WorldId = worldId,
                                Frame = frame,
                                PlayerId = _playerId,
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
                            roomId != _roomId || battleId != _battleId || worldId != _worldId)
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
                result = await _gateway.SubmitAsync(_launch.SessionToken, _battleId, _worldId,
                    _playerId, input, ++_inputSequence, stateDeadline.Token);
                stateDeadline.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Tiny state input request timed out.");
            }
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }

        public async Task LeaveLobbyAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(_roomId) || !string.IsNullOrEmpty(_battleId)) return;
            ThrowIfConnectionUnavailable();
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var result = await _flow.LeaveRoomAsync(new RoomGatewayLeaveRequest(
                _launch.SessionToken, roomId, null, _commandIds.GetOrCreate("leave-room")),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, roomId)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            BindRoom(string.Empty);
        }

        public async Task RequestFullSnapshotAsync(string reason, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(_battleId) || _worldId == 0)
                throw new InvalidOperationException("Tiny battle is not subscribed.");
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            _gateway.PrepareFullSnapshotRecovery();
            _snapshotRequestCount++;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(RequestTimeout);
            WireRequestFullStateSyncRes response;
            try
            {
                response = await _gateway.RequestFullSnapshotAsync(new WireRequestFullStateSyncReq
                {
                    SessionToken = _launch.SessionToken,
                    BattleId = _battleId,
                    RoomId = _roomId,
                    WorldId = _worldId,
                    ClientFrame = _snapshotCursor.LastFrame,
                    LastAuthoritativeFrame = _snapshotCursor.LastFrame,
                    Reason = reason ?? string.Empty
                }, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Tiny snapshot request timed out.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation, revision)) return;
            if (!response.Success || !response.Accepted)
                throw new InvalidOperationException(response.Message);
        }

        private async Task SubscribeAsync(RoomGatewaySnapshot room, CancellationToken cancellationToken)
        {
            var generation = _gateway.ConnectionGeneration;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var mode = TinySyncModeConfiguration.FromProfile(room.SyncCapabilities?.ProfileName ?? string.Empty);
            var model = mode == TinySyncMode.State ? NetworkSyncModel.AuthoritativeInterpolation
                : mode == TinySyncMode.Frame ? NetworkSyncModel.Lockstep
                : NetworkSyncModel.HybridHeroPrediction;
            var binding = RoomGatewayNetworkSyncSessionBinding.Create(
                room.SyncCapabilities, model.ToString(),
                NetworkSyncRemoteCapabilityPolicy.Require);
            var profile = mode == TinySyncMode.Hybrid
                ? NetworkSyncProfiles.FrameWithSnapshotRecovery
                : mode == TinySyncMode.Frame ? NetworkSyncProfiles.LockstepWithSnapshotRecovery
                : NetworkSyncProfileRegistry.Resolve(model);
            var options = new NetworkSyncSessionOptions
            {
                RequiredProfile = profile,
                RequiredMinimumSchemaVersion = 1,
                RequiredMaximumSchemaVersion = 1,
                AvailableCapabilities = NetworkSyncCapabilities.FromProfile(in profile, 1, 1)
            };
            if (!binding.Negotiate(options).IsRemoteNegotiated)
                throw new InvalidOperationException("Tiny sync capabilities were not negotiated.");

            uint playerId = 0;
            foreach (var player in room.Players)
                if (player.AccountId == _launch.AccountId) playerId = player.PlayerId;
            if (playerId == 0) throw new InvalidOperationException("Tiny player slot is missing.");
            var result = await _gateway.Rooms.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(_launch.SessionToken, room.BattleId, roomId),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentBattle(generation, revision, roomId, room)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            if (mode != TinySyncMode.State)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(RequestTimeout);
                WireRoomSubscribeFrameSyncRes frameSubscription;
                try
                {
                    frameSubscription = await _gateway.SubscribeFrameSyncAsync(
                        new WireRoomSubscribeFrameSyncReq
                        {
                            SessionToken = _launch.SessionToken,
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
                if (!IsCurrentBattle(generation, revision, roomId, room)) return;
                if (!frameSubscription.Success)
                    throw new InvalidOperationException(frameSubscription.Message);
            }
            if (!IsCurrentBattle(generation, revision, roomId, room)) return;
            if (_battleId != room.BattleId || _worldId != room.WorldId)
                _inputSequence = 0;
            _battleId = room.BattleId;
            _worldId = room.WorldId;
            _playerId = playerId;
            _syncMode = mode;
            _snapshotCursor.Reset(_worldId);
            _frameReplication = _syncMode == TinySyncMode.State ? null : new TinyFrameReplication();
            _awaitingBaseline = true;
            Status = "Battle (" + _syncMode + ")";
            await RequestFullSnapshotAsync("Subscription baseline", cancellationToken);
        }

        private void BindRoom(string roomId)
        {
            _bindingRevision++;
            _roomId = roomId;
            _battleId = string.Empty;
            _worldId = 0;
            _playerId = 0;
            _inputSequence = 0;
            _snapshotCursor.Reset(0);
            _frameReplication = null;
            _awaitingBaseline = false;
            _syncMode = TinySyncMode.State;
            _loading.Reset();
            _commandIds.Clear();
            _pendingCreateMode = null;
            Room = null;
        }

        private void SetRoom(RoomGatewaySnapshot? room)
        {
            _bindingRevision++;
            Room = room;
        }

        private bool IsCurrent(long generation, long revision) =>
            !_disposed && generation == _gateway.ConnectionGeneration && revision == _bindingRevision &&
            _gateway.ConnectionState != RoomGatewayConnectionState.Reconnecting &&
            _gateway.ConnectionState != RoomGatewayConnectionState.Exhausted;

        private bool IsCurrentBattle(long generation, long revision, string roomId,
            RoomGatewaySnapshot room) =>
            IsCurrent(generation, revision) && roomId == _roomId &&
            ReferenceEquals(Room, room);

        private bool IsCurrentCommand(long generation, long revision, string roomId) =>
            IsCurrent(generation, revision) &&
            _gateway.ConnectionState == RoomGatewayConnectionState.Connected &&
            roomId == _roomId;

        private void ThrowIfConnectionUnavailable()
        {
            if (ConnectionUnavailable)
                throw new InvalidOperationException("Room connection is not ready.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TinyBattleSession));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _gateway.Dispose();
        }
    }
}
