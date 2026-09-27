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

namespace AbilityKit.Demo.Tiny.Turn.View
{
    public sealed class TinyTurnSession : IDisposable
    {
        private readonly DemoMultiplayerLaunchRequest _launch;
        private readonly RoomGatewayConnectionSession _connection;
        private readonly RoomGatewaySessionFlow _flow;
        private readonly RoomGatewayCommandIdLedger _commands = new RoomGatewayCommandIdLedger();
        private readonly RoomGatewayFullSnapshotCursor _cursor = new RoomGatewayFullSnapshotCursor();
        private readonly RoomGatewayLoadingStage _loading;
        private readonly object _snapshotGate = new object();
        private WireStateSyncSnapshotPush? _latest;
        private string _roomId = string.Empty;
        private string _battleId = string.Empty;
        private ulong _worldId;
        private uint _playerId;
        private ulong _commandSequence;
        private long _connectionGeneration;
        private long _bindingRevision;
        private bool _awaitingBaseline;
        private bool _connectionUnavailable;
        private bool _disposed;
        private int _pendingTurn = -1;
        private ulong _pendingSequence;
        private float _pendingElapsed;

        private TinyTurnSession(DemoMultiplayerLaunchRequest launch,
            RoomGatewayConnectionSession connection)
        {
            _launch = launch;
            _connection = connection;
            _flow = new RoomGatewaySessionFlow(connection.RoomClient);
            _loading = new RoomGatewayLoadingStage(_flow, _commands, PrepareAssetsAsync);
            connection.RoomClient.StateSyncSnapshotReceived += OnSnapshot;
        }

        public string RoomId => _roomId;
        public string BattleId => _battleId;
        public uint PlayerId => _playerId;
        public RoomGatewaySnapshot? Room { get; private set; }
        public TinyTurnState? State { get; private set; }
        public string Status { get; private set; } = "Connected";
        public bool CanStart => Room?.CanStart == true && Room.OwnerAccountId == _launch.AccountId;
        public bool CanAct => State.HasValue && !_awaitingBaseline &&
            State.Value.WinnerId == 0 && State.Value.CurrentPlayerId == _playerId &&
            _pendingTurn < 0 &&
            _connection.Recovery.State == RoomGatewayConnectionState.Connected;
        public bool ActionPending => _pendingTurn >= 0;
        public int SnapshotRequestCount { get; private set; }
        public bool NeedsFullSnapshot => _battleId.Length != 0 &&
            (_awaitingBaseline || ActionPending && _pendingElapsed >= 2f) &&
            !ConnectionUnavailable;
        public bool NeedsConnectionRestore =>
            _connection.Recovery.State == RoomGatewayConnectionState.RestoreRequired &&
            _connection.Recovery.Generation != _connectionGeneration;
        public bool ConnectionUnavailable =>
            _connection.Recovery.State != RoomGatewayConnectionState.Connected;

        public static async Task<TinyTurnSession> ConnectAsync(
            DemoMultiplayerLaunchRequest launch, CancellationToken token)
        {
            var connection = await RoomGatewayConnectionSession.ConnectAsync(
                launch.Host, launch.Port, timeout: launch.Timeout, cancellationToken: token);
            return new TinyTurnSession(launch, connection);
        }

        public void Tick(float deltaTime)
        {
            _connection.Tick(deltaTime);
            if (ConnectionUnavailable)
            {
                if (!_connectionUnavailable) _bindingRevision++;
                _connectionUnavailable = true;
                lock (_snapshotGate) _latest = null;
                State = null;
                _awaitingBaseline = true;
                return;
            }
            _connectionUnavailable = false;
            if (ActionPending)
            {
                _pendingElapsed += Math.Max(0f, deltaTime);
                if (_pendingElapsed >= 5f) Status = "Action status unknown - retry or wait";
            }
            WireStateSyncSnapshotPush snapshot;
            lock (_snapshotGate)
            {
                if (!_latest.HasValue) return;
                snapshot = _latest.Value;
                _latest = null;
            }
            if (_battleId.Length == 0 || snapshot.WorldId != _worldId ||
                snapshot.PayloadOpCode != TinyTurnBattle.SnapshotOpCode ||
                !snapshot.IsFullSnapshot || snapshot.Payload == null ||
                !_cursor.TryAccept(in snapshot)) return;
            var state = TinyTurnStateCodec.Decode(snapshot.Payload);
            if (state.Frame != snapshot.Frame) throw new InvalidOperationException("Tiny Turn snapshot frame mismatch.");
            State = state;
            _awaitingBaseline = false;
            if (_pendingTurn >= 0 && (state.Turn > _pendingTurn ||
                state.CurrentPlayerId != _playerId || state.WinnerId != 0))
            {
                _pendingTurn = -1;
                _pendingSequence = 0;
                _pendingElapsed = 0f;
            }
            Status = state.WinnerId != 0 ? "Finished" :
                ActionPending ? PendingStatus() : "Battle";
        }

        public async Task RestoreAsync(CancellationToken token) =>
            await RestoreCoreAsync(token);

        private async Task<bool> RestoreCoreAsync(CancellationToken token)
        {
            var generation = _connection.Recovery.Generation;
            var revision = _bindingRevision;
            var restored = await _flow.RestoreWithoutPlayerIdAsync(
                _launch.SessionToken, _launch.Region, _launch.ServerId,
                timeout: _launch.Timeout, cancellationToken: token);
            if (!IsCurrent(generation, revision)) return false;
            if (restored.RestoreStatus == RoomGatewaySessionRestoreStatus.InvalidSession)
                throw new UnauthorizedAccessException(restored.Message);
            if (restored.CanRetry) throw new TimeoutException(restored.Message);
            if (restored.RestoreStatus == RoomGatewaySessionRestoreStatus.Failed)
                throw new InvalidOperationException(restored.Message);
            if (string.IsNullOrEmpty(restored.RoomId) || restored.Snapshot == null)
            {
                BindRoom(string.Empty);
                Status = "Connected";
                return true;
            }
            if (_roomId != restored.RoomId) BindRoom(restored.RoomId);
            revision = _bindingRevision;
            Room = restored.Snapshot;
            Status = ActionPending ? PendingStatus() : Room.Phase.ToString();
            if (Room.Phase == RoomGatewaySessionPhase.InBattle)
                await SubscribeAsync(Room, token);
            return IsCurrent(generation, revision);
        }

        public async Task RecoverConnectionAsync(CancellationToken token)
        {
            if (!NeedsConnectionRestore) return;
            var generation = _connection.Recovery.Generation;
            if (await RestoreCoreAsync(token) &&
                _connection.Recovery.CompleteRestore(generation))
                _connectionGeneration = generation;
        }

        public async Task CreateRoomAsync(CancellationToken token)
        {
            if (_roomId.Length != 0) throw new InvalidOperationException("Leave the current room first.");
            var result = await _connection.RoomClient.CreateRoomAsync(new RoomGatewayCreateRequest(
                _launch.SessionToken, _launch.Region, _launch.ServerId,
                TinyTurnBattle.RoomType, "Tiny Turn", false, 2,
                commandId: _commands.GetOrCreate("create-room")), cancellationToken: token);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            _commands.Complete("create-room");
            await JoinRoomAsync(result.RoomId, token);
        }

        public async Task JoinRoomAsync(string roomId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Room ID is required.", nameof(roomId));
            var result = await _connection.RoomClient.JoinRoomAsync(new RoomGatewayJoinRequest(
                _launch.SessionToken, _launch.Region, _launch.ServerId, roomId.Trim()),
                cancellationToken: token);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            BindRoom(result.RoomId ?? roomId.Trim());
            Status = "Joined";
        }

        public async Task SetReadyAsync(CancellationToken token)
        {
            var result = await _connection.RoomClient.SetReadyAsync(new RoomGatewayReadyRequest(
                _launch.SessionToken, _roomId, true), cancellationToken: token);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            Status = "Ready";
        }

        public async Task BeginLoadingAsync(CancellationToken token)
        {
            var result = await _flow.BeginLoadingAsync(new RoomGatewayBeginLoadingRequest(
                _launch.SessionToken, _roomId, null, _commands.GetOrCreate("begin-loading")),
                cancellationToken: token);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            _commands.Complete("begin-loading");
            Status = "Loading";
        }

        public async Task PollAsync(CancellationToken token)
        {
            if (_roomId.Length == 0) return;
            var roomId = _roomId;
            var generation = _connection.Recovery.Generation;
            var revision = _bindingRevision;
            var result = await _flow.GetSnapshotAsync(_launch.SessionToken, roomId,
                cancellationToken: token);
            if (!IsCurrent(generation, revision) || roomId != _roomId) return;
            if (!result.Success || result.Snapshot == null)
                throw new InvalidOperationException(result.Message);
            Room = result.Snapshot;
            Status = ActionPending ? PendingStatus() : Room.Phase.ToString();
            await _loading.AdvanceAsync(_launch.SessionToken, Room, token);
            if (!IsCurrent(generation, revision) || roomId != _roomId) return;
            if (Room.Phase == RoomGatewaySessionPhase.InBattle &&
                (_battleId != Room.BattleId || _worldId != Room.WorldId))
                await SubscribeAsync(Room, token);
        }

        public async Task SubmitActionAsync(CancellationToken token)
        {
            if (!CanAct) throw new InvalidOperationException("It is not your turn.");
            _pendingTurn = State!.Value.Turn;
            _pendingElapsed = 0f;
            try
            {
                for (var attempt = 0; attempt <= TinyTurnBattle.MaxHp; attempt++)
                {
                    _pendingSequence = ++_commandSequence;
                    var result = await SendActionAsync(_pendingSequence, token);
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
                if (_pendingTurn >= 0) Status = "Action status unknown";
                throw;
            }
        }

        public async Task RetryPendingActionAsync(CancellationToken token)
        {
            if (!ActionPending || ConnectionUnavailable || _pendingSequence == 0)
                throw new InvalidOperationException("No pending action can be retried.");
            var result = await SendActionAsync(_pendingSequence, token);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            Status = PendingStatus();
        }

        public async Task RequestFullSnapshotAsync(string reason, CancellationToken token)
        {
            if (_battleId.Length == 0 || _disposed ||
                _connection.Recovery.State == RoomGatewayConnectionState.Reconnecting ||
                _connection.Recovery.State == RoomGatewayConnectionState.Exhausted)
                throw new InvalidOperationException("Tiny Turn battle is not ready for a snapshot.");
            var generation = _connection.Recovery.Generation;
            var revision = _bindingRevision;
            var roomId = _roomId;
            SnapshotRequestCount++;
            var response = await _connection.RoomClient.RequestFullStateSyncAsync(
                new WireRequestFullStateSyncReq
                {
                    SessionToken = _launch.SessionToken, BattleId = _battleId,
                    RoomId = roomId, WorldId = _worldId, Reason = reason
                }, _launch.Timeout, token);
            if (!IsCurrent(generation, revision) || roomId != _roomId) return;
            if (!response.Success || !response.Accepted)
                throw new InvalidOperationException(response.Message);
        }

        private string PendingStatus() => _pendingElapsed >= 5f ?
            "Action status unknown - retry or wait" : "Waiting for authoritative turn";

        private Task<WireSubmitBattleInputRes> SendActionAsync(ulong sequence, CancellationToken token) =>
            _connection.RoomClient.SubmitBattleInputAsync(new WireSubmitBattleInputReq
            {
                SessionToken = _launch.SessionToken, BattleId = _battleId,
                WorldId = _worldId, Frame = 0, PlayerId = _playerId,
                InputOpCode = TinyTurnBattle.InputOpCode,
                Payload = new byte[] { 1 }, CommandSequence = sequence
            }, _launch.Timeout, token);

        public async Task LeaveLobbyAsync(CancellationToken token)
        {
            if (_roomId.Length == 0 || _battleId.Length != 0) return;
            var result = await _flow.LeaveRoomAsync(new RoomGatewayLeaveRequest(
                _launch.SessionToken, _roomId, null, _commands.GetOrCreate("leave-room")),
                cancellationToken: token);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            BindRoom(string.Empty);
        }

        private async Task SubscribeAsync(RoomGatewaySnapshot room, CancellationToken token)
        {
            var generation = _connection.Recovery.Generation;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
            var binding = RoomGatewayNetworkSyncSessionBinding.Create(
                room.SyncCapabilities, nameof(NetworkSyncModel.AuthoritativeInterpolation),
                NetworkSyncRemoteCapabilityPolicy.Require);
            var options = new NetworkSyncSessionOptions
            {
                RequiredProfile = profile,
                RequiredMinimumSchemaVersion = 1,
                RequiredMaximumSchemaVersion = 1,
                AvailableCapabilities = NetworkSyncCapabilities.FromProfile(in profile, 1, 1)
            };
            if (!binding.Negotiate(options).IsRemoteNegotiated)
                throw new InvalidOperationException("Tiny Turn State capabilities were not negotiated.");
            var playerId = 0u;
            foreach (var player in room.Players)
                if (player.AccountId == _launch.AccountId) playerId = player.PlayerId;
            if (playerId == 0) throw new InvalidOperationException("Tiny Turn player slot is missing.");
            var result = await _connection.RoomClient.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(
                    _launch.SessionToken, room.BattleId, room.RoomId), cancellationToken: token);
            if (!IsCurrent(generation, revision) || roomId != _roomId) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            _battleId = room.BattleId;
            _worldId = room.WorldId;
            _playerId = playerId;
            _cursor.Reset(_worldId);
            State = null;
            _awaitingBaseline = true;
            await RequestFullSnapshotAsync("Tiny Turn subscription baseline", token);
        }

        private bool IsCurrent(long generation, long revision) =>
            !_disposed && generation == _connection.Recovery.Generation &&
            revision == _bindingRevision &&
            _connection.Recovery.State != RoomGatewayConnectionState.Reconnecting &&
            _connection.Recovery.State != RoomGatewayConnectionState.Exhausted;

        private static Task PrepareAssetsAsync(RoomGatewaySnapshot room, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (room.LaunchManifestVersion <= 0 || string.IsNullOrEmpty(room.LaunchManifestHash))
                throw new InvalidOperationException("Tiny Turn launch manifest is incomplete.");
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

        private void BindRoom(string roomId)
        {
            _bindingRevision++;
            _roomId = roomId;
            _battleId = string.Empty;
            _worldId = 0;
            _playerId = 0;
            _commandSequence = 0;
            _pendingTurn = -1;
            _pendingSequence = 0;
            _pendingElapsed = 0f;
            Room = null;
            State = null;
            _awaitingBaseline = false;
            _loading.Reset();
            _commands.Clear();
            _cursor.Reset(0);
            lock (_snapshotGate) _latest = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bindingRevision++;
            _connection.RoomClient.StateSyncSnapshotReceived -= OnSnapshot;
            _connection.Dispose();
        }
    }
}
