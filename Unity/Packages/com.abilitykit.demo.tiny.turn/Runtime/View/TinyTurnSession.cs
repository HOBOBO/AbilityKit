#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.Turn.View
{
    public interface ITinyTurnGateway : IRoomGatewayBattleConnection
    {
        event Action<WireStateSyncSnapshotPush> SnapshotReceived;
        Task<WireSubmitBattleInputRes> SubmitActionAsync(WireSubmitBattleInputReq request,
            TimeSpan timeout, CancellationToken token);
        Task<WireRequestFullStateSyncRes> RequestFullSnapshotAsync(WireRequestFullStateSyncReq request,
            TimeSpan timeout, CancellationToken token);
    }

    internal sealed class TinyTurnGateway : ITinyTurnGateway
    {
        private readonly RoomGatewayConnectionSession _connection;

        public TinyTurnGateway(RoomGatewayConnectionSession connection) => _connection = connection;
        public IRoomGatewaySessionClient Rooms => _connection.RoomClient;
        public RoomGatewayConnectionState ConnectionState => _connection.Recovery.State;
        public long ConnectionGeneration => _connection.Recovery.Generation;
        public event Action<WireStateSyncSnapshotPush> SnapshotReceived
        {
            add => _connection.RoomClient.StateSyncSnapshotReceived += value;
            remove => _connection.RoomClient.StateSyncSnapshotReceived -= value;
        }
        public void Tick(float deltaTime) => _connection.Tick(deltaTime);
        public bool CompleteRestore(long generation) => _connection.Recovery.CompleteRestore(generation);
        public Task<WireSubmitBattleInputRes> SubmitActionAsync(WireSubmitBattleInputReq request,
            TimeSpan timeout, CancellationToken token) =>
            _connection.RoomClient.SubmitBattleInputAsync(request, timeout, token);
        public Task<WireRequestFullStateSyncRes> RequestFullSnapshotAsync(WireRequestFullStateSyncReq request,
            TimeSpan timeout, CancellationToken token) =>
            _connection.RoomClient.RequestFullStateSyncAsync(request, timeout, token);
        public void Dispose() => _connection.Dispose();
    }

    /// <summary>
    /// Tiny 回合制会话：房间生命周期与恢复纪律由 <see cref="RoomGatewayBattleSessionBase"/> 提供，
    /// 本类只保留回合所有权（行动确认、序号续号）与权威快照到 <see cref="TinyTurnState"/> 的投影。
    /// </summary>
    public sealed class TinyTurnSession : RoomGatewayBattleSessionBase
    {
        private const int CommandSequenceResumeAttempts = 3;
        private const float ActionStatusUnknownSeconds = 5f;
        private readonly ITinyTurnGateway _connection;
        private readonly object _snapshotGate = new object();
        private WireStateSyncSnapshotPush? _latest;
        private ulong _commandSequence;
        private bool _connectionUnavailable;
        private int _pendingTurn = -1;
        private ulong _pendingSequence;
        private float _pendingElapsed;

        public TinyTurnSession(DemoMultiplayerLaunchRequest launch, ITinyTurnGateway connection)
            : base(ToIdentity(launch), connection, PrepareAssetsAsync)
        {
            _connection = connection;
            connection.SnapshotReceived += OnSnapshot;
        }

        public TinyTurnState? State { get; private set; }

        public bool CanAct => State.HasValue && !WaitingForBaseline &&
            State.Value.WinnerId == 0 && State.Value.CurrentPlayerId == PlayerId &&
            _pendingTurn < 0 &&
            _connection.ConnectionState == RoomGatewayConnectionState.Connected;

        public bool ActionPending => _pendingTurn >= 0;

        public int SnapshotRequestCount => FullSnapshotRequestCount;

        public bool NeedsFullSnapshot => BattleId.Length != 0 &&
            (WaitingForBaseline || ActionPending && _pendingElapsed >= 2f) &&
            !ConnectionUnavailable;

        public static async Task<TinyTurnSession> ConnectAsync(
            DemoMultiplayerLaunchRequest launch, CancellationToken token)
        {
            var connection = await RoomGatewayConnectionSession.ConnectAsync(
                launch.Host, launch.Port, timeout: launch.Timeout, cancellationToken: token);
            return new TinyTurnSession(launch, new TinyTurnGateway(connection));
        }

        protected override void OnTick(float deltaTime)
        {
            if (ConnectionUnavailable)
            {
                if (!_connectionUnavailable) InvalidatePendingCommands();
                _connectionUnavailable = true;
                lock (_snapshotGate) _latest = null;
                State = null;
                BeginBaselineWaiting();
                return;
            }
            _connectionUnavailable = false;
            if (ActionPending)
            {
                _pendingElapsed += Math.Max(0f, deltaTime);
                if (_pendingElapsed >= ActionStatusUnknownSeconds) Status = "Action status unknown - retry or wait";
            }
            WireStateSyncSnapshotPush snapshot;
            lock (_snapshotGate)
            {
                if (!_latest.HasValue) return;
                snapshot = _latest.Value;
                _latest = null;
            }
            if (BattleId.Length == 0 || snapshot.WorldId != BattleWorldId ||
                snapshot.PayloadOpCode != TinyTurnBattle.SnapshotOpCode ||
                !snapshot.IsFullSnapshot || snapshot.Payload == null ||
                !SnapshotCursor.TryAccept(in snapshot)) return;
            var state = TinyTurnStateCodec.Decode(snapshot.Payload);
            if (state.Frame != snapshot.Frame) throw new InvalidOperationException("Tiny Turn snapshot frame mismatch.");
            State = state;
            ClearBaselineWaiting();
            if (_pendingTurn >= 0 && (state.Turn > _pendingTurn ||
                state.CurrentPlayerId != PlayerId || state.WinnerId != 0))
            {
                _pendingTurn = -1;
                _pendingSequence = 0;
                _pendingElapsed = 0f;
            }
            Status = state.WinnerId != 0 ? "Finished" :
                ActionPending ? PendingStatus() : "Battle";
        }

        public Task CreateRoomAsync(CancellationToken token) => CreateRoomCoreAsync(token);

        public async Task SubmitActionAsync(CancellationToken token)
        {
            if (!CanAct) throw new InvalidOperationException("It is not your turn.");
            _pendingTurn = State!.Value.Turn;
            _pendingElapsed = 0f;
            try
            {
                var generation = _connection.ConnectionGeneration;
                var revision = CurrentBindingRevision;
                var roomId = RoomId;
                var battleId = BattleId;
                for (var attempt = 0; attempt < CommandSequenceResumeAttempts; attempt++)
                {
                    _pendingSequence = ++_commandSequence;
                    var result = await SendActionAsync(_pendingSequence, token);
                    token.ThrowIfCancellationRequested();
                    if (!IsCurrentCommand(generation, revision, roomId) || battleId != BattleId)
                        return;
                    if (result.Success && result.Status == "Deduplicated") continue;
                    if (!result.Success)
                    {
                        _pendingTurn = -1;
                        _pendingSequence = 0;
                        throw new InvalidOperationException(result.Message);
                    }
                    Status = "Action queued";
                    return;
                }
                _pendingTurn = -1;
                _pendingSequence = 0;
                throw new InvalidOperationException("Tiny Turn command sequence could not be resumed.");
            }
            catch
            {
                if (_pendingTurn >= 0 && !IsDisposed) Status = "Action status unknown";
                throw;
            }
        }

        public async Task RetryPendingActionAsync(CancellationToken token)
        {
            if (!ActionPending || ConnectionUnavailable || _pendingSequence == 0)
                throw new InvalidOperationException("No pending action can be retried.");
            var generation = _connection.ConnectionGeneration;
            var revision = CurrentBindingRevision;
            var roomId = RoomId;
            var battleId = BattleId;
            var result = await SendActionAsync(_pendingSequence, token);
            token.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, roomId) || battleId != BattleId) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            Status = PendingStatus();
        }

        public Task RequestFullSnapshotAsync(string reason, CancellationToken token)
        {
            if (_connection.ConnectionState == RoomGatewayConnectionState.Reconnecting ||
                _connection.ConnectionState == RoomGatewayConnectionState.Exhausted)
                throw new InvalidOperationException(BattleNotSubscribedMessage);
            return RequestFullSnapshotCoreAsync(reason, token);
        }

        private string PendingStatus() => _pendingElapsed >= ActionStatusUnknownSeconds ?
            "Action status unknown - retry or wait" : "Waiting for authoritative turn";

        private Task<WireSubmitBattleInputRes> SendActionAsync(ulong sequence, CancellationToken token) =>
            _connection.SubmitActionAsync(new WireSubmitBattleInputReq
            {
                SessionToken = Identity.SessionToken, BattleId = BattleId,
                WorldId = BattleWorldId, Frame = 0, PlayerId = PlayerId,
                InputOpCode = TinyTurnBattle.InputOpCode,
                Payload = new byte[] { 1 }, CommandSequence = sequence
            }, RequestTimeout, token);

        protected override RoomGatewayCreateRequest CreateRoomRequest(string commandId) => new(
            Identity.SessionToken, Identity.Region, Identity.ServerId,
            TinyTurnBattle.RoomType, "Tiny Turn", false, 2, commandId: commandId);

        protected override void ValidateLaunchManifest(RoomGatewaySnapshot room) =>
            RoomGatewayLaunchManifestCompatibility.Require(room, 1,
                new[] { TinyTurnBattle.AssetKey, TinyTurnBattle.RulesKey },
                new System.Collections.Generic.Dictionary<string, string>
                { ["players"] = room.Players.Count.ToString() });

        protected override string ResolveSubscriptionModelName(RoomGatewaySnapshot room) =>
            nameof(NetworkSyncModel.AuthoritativeInterpolation);

        protected override NetworkSyncProfile ResolveSubscriptionProfile(RoomGatewaySnapshot room) =>
            NetworkSyncProfiles.AuthoritativeInterpolation;

        protected override void OnBattleBound(RoomGatewaySnapshot room, uint playerId,
            string previousBattleId, ulong previousWorldId) => State = null;

        protected override void OnRoomUnbound()
        {
            _commandSequence = 0;
            _pendingTurn = -1;
            _pendingSequence = 0;
            _pendingElapsed = 0f;
            State = null;
            lock (_snapshotGate) _latest = null;
        }

        protected override void OnDisposing() =>
            _connection.SnapshotReceived -= OnSnapshot;

        protected override Task<WireRequestFullStateSyncRes> SendFullSnapshotRequestAsync(
            WireRequestFullStateSyncReq request, CancellationToken cancellationToken) =>
            _connection.RequestFullSnapshotAsync(request, RequestTimeout, cancellationToken);

        protected override string CreatePendingRoomMessage => "Leave the current room first.";

        protected override string SyncNotNegotiatedMessage => "Tiny Turn State capabilities were not negotiated.";

        protected override string PlayerSlotMissingMessage => "Tiny Turn player slot is missing.";

        protected override string BattleNotSubscribedMessage => "Tiny Turn battle is not ready for a snapshot.";

        protected override string FullSnapshotTimeoutMessage => "Tiny Turn snapshot request timed out.";

        protected override string BaselineRequestReason => "Tiny Turn subscription baseline";

        protected override string DescribeRoomStatus(RoomGatewaySnapshot? room, RoomGatewaySessionPhase phase) =>
            ActionPending ? PendingStatus() : phase.ToString();

        protected override string DescribeRestoreStatus(RoomGatewaySessionRestoreStatus restoreStatus) =>
            "Connected";

        private static RoomGatewaySessionIdentity ToIdentity(DemoMultiplayerLaunchRequest launch) =>
            new(launch.SessionToken, launch.Region, launch.ServerId, launch.AccountId, launch.Timeout);

        private static Task PrepareAssetsAsync(RoomGatewaySnapshot room, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            RoomGatewayLaunchManifestCompatibility.Require(room, 1,
                new[] { TinyTurnBattle.AssetKey, TinyTurnBattle.RulesKey },
                new System.Collections.Generic.Dictionary<string, string>
                { ["players"] = room.Players.Count.ToString() });
            return Task.CompletedTask;
        }

        private void OnSnapshot(WireStateSyncSnapshotPush snapshot)
        {
            lock (_snapshotGate)
            {
                if (!_latest.HasValue || snapshot.WorldId != _latest.Value.WorldId ||
                    snapshot.Frame > _latest.Value.Frame)
                    _latest = snapshot;
            }
        }
    }
}
