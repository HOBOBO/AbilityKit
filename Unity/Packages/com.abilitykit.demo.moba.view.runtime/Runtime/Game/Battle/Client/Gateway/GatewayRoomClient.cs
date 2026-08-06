using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Moba.GatewayTimeSync;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Room;
using AbilityKit.Game.Flow;
using AbilityKit.Demo.Common.Rooms;
using WireRoomStateChangedPush = AbilityKit.Protocol.Room.WireRoomStateChangedPush;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed class GatewayRoomClient :
        IGatewayRoomClient,
        IDemoRoomDirectoryClient,
        IRoomGatewayRequestTransport,
        IRoomGatewayPushSource,
        IDisposable
    {
        private readonly Func<uint, ArraySegment<byte>, TimeSpan?, CancellationToken,
            Task<ArraySegment<byte>>> _sendRequestAsync;
        private readonly Action<Action<uint, ArraySegment<byte>>> _subscribeServerPush;
        private readonly Action<Action<uint, ArraySegment<byte>>> _unsubscribeServerPush;
        private readonly IDisposable _ownedRequestClient;
        private readonly GatewayRoomOpCodes _opCodes;
        private readonly RoomGatewayWireSessionClient _roomSessionClient;
        private long _nextBattleInputCommandSequence;
        private bool _disposed;

        public GatewayRoomClient(IConnection connection, GatewayRoomOpCodes opCodes)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            var requestClient = new RequestClient(connection);
            _sendRequestAsync = requestClient.SendRequestAsync;
            _subscribeServerPush = handler => connection.ServerPushReceived += handler;
            _unsubscribeServerPush = handler => connection.ServerPushReceived -= handler;
            _ownedRequestClient = requestClient;
            _opCodes = opCodes;
            _roomSessionClient = new RoomGatewayWireSessionClient(
                this,
                this,
                ToWireOpCodes(in opCodes));
        }

        public GatewayRoomClient(NetworkSdkClient sdkClient, GatewayRoomOpCodes opCodes)
        {
            if (sdkClient == null) throw new ArgumentNullException(nameof(sdkClient));

            _sendRequestAsync = sdkClient.SendRawRequestAsync;
            _subscribeServerPush = handler => sdkClient.ServerPushReceived += handler;
            _unsubscribeServerPush = handler => sdkClient.ServerPushReceived -= handler;
            _ownedRequestClient = null;
            _opCodes = opCodes;
            _roomSessionClient = new RoomGatewayWireSessionClient(
                this,
                this,
                ToWireOpCodes(in opCodes));
        }

        public event Action<uint, ArraySegment<byte>> ServerPushReceived
        {
            add => _subscribeServerPush(value);
            remove => _unsubscribeServerPush(value);
        }

        public Task<ArraySegment<byte>> SendRawRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return _sendRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        public Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return SendRawRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        public async Task<GatewayTimeSyncResult> TimeSyncAsync(uint timeSyncOpCode, long clientSendTicks, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var req = new WireTimeSyncReq(clientSendTicks);
            var payload = WireTimeSyncBinary.Serialize(in req);
            var resp = await _sendRequestAsync(timeSyncOpCode, payload, timeout, cancellationToken);
            var wire = WireTimeSyncBinary.DeserializeTimeSyncRes(resp);
            return new GatewayTimeSyncResult(wire.ClientSendTicks, wire.ServerNowTicks, wire.ServerTickFrequency);
        }

        public async Task<string> GuestLoginAsync(uint guestLoginOpCode, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var req = new WireRoomGuestLoginReq
            {
                GuestId = Guid.NewGuid().ToString("N")
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var resp = await _sendRequestAsync(guestLoginOpCode, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginRes>(resp);
            return wire.Success ? wire.SessionToken ?? string.Empty : string.Empty;
        }

        public async Task<DemoRoomDirectoryResult> ListRoomsAsync(
            DemoRoomDirectoryQuery query,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var payload = DemoRoomGatewayDirectoryCodec.SerializeQuery(in query);
            var resp = await _sendRequestAsync(
                RoomGatewayOpCodes.ListRooms,
                payload,
                timeout,
                cancellationToken);
            return DemoRoomGatewayDirectoryCodec.DeserializeResult(resp);
        }

        public async Task<GatewayCreateRoomResult> CreateRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomType,
            string title,
            bool isPublic,
            int maxPlayers,
            IReadOnlyDictionary<string, string> tags,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));
            if (string.IsNullOrWhiteSpace(roomType)) roomType = "battle";
            if (title == null) title = string.Empty;

            var result = await _roomSessionClient.CreateRoomAsync(
                new RoomGatewayCreateRequest(
                    sessionToken,
                    region,
                    serverId,
                    roomType,
                    title,
                    isPublic,
                    maxPlayers,
                    tags),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayCreateRoomResult(result.RoomId, result.NumericRoomId);
        }

        public async Task<GatewayJoinRoomResult> JoinRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.JoinRoomAsync(
                new RoomGatewayJoinRequest(sessionToken, region, serverId, roomId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            var anchor = ToGatewayAnchor(in result.WorldStartAnchor);
            return new GatewayJoinRoomResult(
                result.Success,
                result.RoomId,
                result.NumericRoomId,
                string.Empty,
                in anchor,
                result.Message,
                result.BattleId,
                result.CanStart,
                result.ServerNowTicks,
                result.WorldId,
                result.CurrentPlayerId);
        }

        public async Task<GatewayRoomSnapshotResult> SetReadyAsync(
            string sessionToken,
            string roomId,
            bool ready,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.SetReadyAsync(
                new RoomGatewayReadyRequest(sessionToken, roomId, ready),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayRoomSnapshotResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomId,
                result.NumericRoomId);
        }

        public async Task<GatewayRoomSnapshotResult> PickHeroAsync(
            string sessionToken,
            string roomId,
            int heroId,
            int teamId,
            int spawnPointId,
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            IReadOnlyList<int> skillIds,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.PickHeroAsync(
                new RoomGatewayPickHeroRequest(
                    sessionToken,
                    roomId,
                    heroId,
                    teamId,
                    spawnPointId,
                    level,
                    attributeTemplateId,
                    basicAttackSkillId,
                    skillIds),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayRoomSnapshotResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomId,
                result.NumericRoomId);
        }

        public async Task<GatewayStartBattleResult> StartBattleAsync(
            string sessionToken,
            string roomId,
            int gameplayId,
            int ruleSetId,
            int configVersion,
            int protocolVersion,
            string worldType,
            string clientId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.StartBattleAsync(
                new RoomGatewayStartBattleRequest(
                    sessionToken,
                    roomId,
                    gameplayId,
                    ruleSetId,
                    configVersion,
                    protocolVersion,
                    worldType,
                    clientId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayStartBattleResult(result.BattleId, result.WorldId, result.Started);
        }

        public async Task<GatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            string sessionToken,
            string battleId,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(battleId)) throw new ArgumentException("battleId is required.", nameof(battleId));

            var result = await _roomSessionClient.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(
                    sessionToken,
                    battleId,
                    roomId ?? string.Empty),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new GatewayStateSyncSubscriptionResult(result.Success);
        }

        public GatewayStateSyncSnapshot DeserializeStateSyncSnapshotPush(ArraySegment<byte> payload)
        {
            // FIXED (2026-07-20): Use WireRoomGatewayBinary + WireStateSyncSnapshotPush (MemoryPack)
            // instead of MobaWorldSnapshotCodec (BinaryObjectCodec). The server encodes via
            // WireRoomGatewayBinary.Serialize(WireStateSyncSnapshotPush) in StateSyncObserverGrain,
            // so the matching deserializer is WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>.
            // The old MobaWorldSnapshotCodec path used an incompatible BinaryObjectCodec and a
            // different struct shape (5 fields, long Timestamp) — it would silently produce
            // default/empty snapshots instead of throwing, masking the real data.
            var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
            return ToGatewaySnapshot(in wire);
        }

        public bool IsStateSyncSnapshotPush(uint opCode)
        {
            return opCode == _opCodes.SnapshotPushed || opCode == _opCodes.DeltaSnapshotPushed;
        }

        public async Task<GatewayBattleInputResult> SubmitBattleInputAsync(
            string sessionToken,
            string battleId,
            ulong worldId,
            int frame,
            uint playerId,
            int inputOpCode,
            byte[] inputPayload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(battleId)) throw new ArgumentException("battleId is required.", nameof(battleId));
            if (worldId == 0) throw new ArgumentOutOfRangeException(nameof(worldId));
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            if (playerId == 0) throw new ArgumentOutOfRangeException(nameof(playerId));

            var commandSequence = unchecked((ulong)Interlocked.Increment(ref _nextBattleInputCommandSequence));
            var req = new WireSubmitBattleInputReq
            {
                SessionToken = sessionToken,
                BattleId = battleId,
                WorldId = worldId,
                Frame = frame,
                PlayerId = playerId,
                InputOpCode = inputOpCode,
                Payload = inputPayload ?? Array.Empty<byte>(),
                CommandSequence = commandSequence
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _sendRequestAsync(_opCodes.SubmitBattleInput, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputRes>(respPayload);
            return new GatewayBattleInputResult(
                wire.AcceptedFrame,
                wire.Success,
                wire.CurrentFrame,
                wire.Status,
                wire.Message,
                wire.ShouldResync,
                wire.ServerTicks,
                commandSequence);
        }

        // ===== 阶段 5：资源加载屏障 / 状态查询 / 恢复 / 状态变更推送 =====

        public async Task<GatewayRoomOperationResult> BeginLoadingAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.BeginLoadingAsync(
                new RoomGatewayBeginLoadingRequest(
                    sessionToken,
                    roomId,
                    expectedRevision,
                    commandId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayRoomOperationResult> ReportAssetsLoadedAsync(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.ReportAssetsLoadedAsync(
                new RoomGatewayReportAssetsLoadedRequest(
                    sessionToken,
                    roomId,
                    launchGeneration,
                    manifestVersion,
                    manifestHash,
                    commandId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayRoomOperationResult> ReportLoadingProgressAsync(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            int progress,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            if (progress < 0 || progress > 100) throw new ArgumentOutOfRangeException(nameof(progress));

            var result = await _roomSessionClient.ReportLoadingProgressAsync(
                new RoomGatewayReportLoadingProgressRequest(
                    sessionToken,
                    roomId,
                    launchGeneration,
                    manifestVersion,
                    manifestHash,
                    progress),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayRoomOperationResult> CancelLoadingAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.CancelLoadingAsync(
                new RoomGatewayCancelLoadingRequest(
                    sessionToken,
                    roomId,
                    expectedRevision,
                    commandId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayRoomOperationResult> LeaveRoomAsync(
            string sessionToken,
            string roomId,
            long? expectedRevision,
            string commandId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.LeaveRoomAsync(
                new RoomGatewayLeaveRequest(
                    sessionToken,
                    roomId,
                    expectedRevision,
                    commandId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToOperationResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.Message,
                result.RoomRevision,
                result.Snapshot);
        }

        public async Task<GatewayGetSnapshotResult> GetSnapshotAsync(
            string sessionToken,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var result = await _roomSessionClient.GetSnapshotAsync(
                new RoomGatewayGetSnapshotRequest(sessionToken, roomId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            var snapshot = ToClientSnapshot(result.Snapshot);
            return new GatewayGetSnapshotResult(
                result.Success,
                result.RoomId,
                result.NumericRoomId,
                snapshot,
                result.Message);
        }

        public async Task<GatewayRestoreRoomResult> RestoreRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));

            var result = await _roomSessionClient.RestoreRoomAsync(
                new RoomGatewayRestoreRoomRequest(sessionToken, region, serverId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            var snapshot = ToClientSnapshot(result.Snapshot);
            var anchor = ToGatewayAnchor(in result.WorldStartAnchor);
            return new GatewayRestoreRoomResult(
                result.Success,
                result.HasActiveRoom,
                result.IsInBattle,
                result.RoomId,
                result.NumericRoomId,
                snapshot,
                in anchor,
                result.Message,
                ToJoinKind(result.JoinKind),
                result.ServerNowTicks,
                result.CurrentPlayerId);
        }

        public ClientRoomSnapshot DeserializeRoomStateChangedPush(ArraySegment<byte> payload)
        {
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomStateChangedPush>(payload);
            var wireSnapshot = wire.Snapshot;
            return ClientRoomSnapshotMapper.ToClientSnapshot(wireSnapshot);
        }

        public bool IsRoomStateChangedPush(uint opCode)
        {
            return opCode == _opCodes.RoomStateChanged;
        }

        private static GatewayRoomOperationResult ToOperationResult(
            bool success,
            bool applied,
            int errorCode,
            string message,
            long roomRevision,
            RoomGatewaySnapshot snapshot)
        {
            return new GatewayRoomOperationResult(
                success,
                applied,
                errorCode,
                message,
                roomRevision,
                ToClientSnapshot(snapshot));
        }

        private static ClientRoomSnapshot ToClientSnapshot(RoomGatewaySnapshot snapshot)
        {
            return snapshot == null
                ? new ClientRoomSnapshot()
                : ClientRoomSnapshotMapper.ToClientSnapshot(snapshot);
        }

        private static RoomGatewayJoinKind ToJoinKind(RoomGatewaySessionEntryKind kind)
        {
            switch (kind)
            {
                case RoomGatewaySessionEntryKind.Reconnect:
                    return RoomGatewayJoinKind.Reconnect;
                case RoomGatewaySessionEntryKind.LateJoin:
                    return RoomGatewayJoinKind.LateJoin;
                default:
                    return RoomGatewayJoinKind.TeamLobby;
            }
        }

        public static GatewayStateSyncSnapshot ToGatewaySnapshot(in WireStateSyncSnapshotPush push)
        {
            var source = push.Actors;
            var actors = source == null || source.Count == 0
                ? Array.Empty<GatewayStateSyncActorSnapshot>()
                : new GatewayStateSyncActorSnapshot[source.Count];

            for (int i = 0; i < actors.Length; i++)
            {
                var actor = source[i];
                actors[i] = new GatewayStateSyncActorSnapshot(
                    actor.ActorId,
                    actor.X,
                    actor.Y,
                    actor.Z,
                    actor.Rotation,
                    actor.VelocityX,
                    actor.VelocityZ,
                    actor.Hp,
                    actor.HpMax,
                    actor.TeamId,
                    actor.Kind,
                    actor.Code,
                    actor.OwnerNetId);
            }

            var removedSource = push.RemovedActorIds;
            var removedActorIds = removedSource == null || removedSource.Count == 0
                ? Array.Empty<int>()
                : removedSource.ToArray();

            return new GatewayStateSyncSnapshot(
                push.WorldId,
                push.Frame,
                push.Timestamp,
                push.IsFullSnapshot,
                actors,
                push.SchemaVersion,
                removedActorIds,
                push.EventWatermark,
                push.EventEpoch);
        }

        private static GatewayWorldStartAnchor ToGatewayAnchor(in RoomGatewayWorldStartAnchor anchor)
        {
            return new GatewayWorldStartAnchor(
                anchor.StartServerTicks,
                anchor.ServerTickFrequency,
                anchor.StartFrame,
                anchor.FixedDeltaSeconds);
        }

        private static RoomGatewayWireOpCodes ToWireOpCodes(in GatewayRoomOpCodes opCodes)
        {
            return new RoomGatewayWireOpCodes(
                opCodes.CreateRoom,
                opCodes.JoinRoom,
                opCodes.LeaveRoom,
                opCodes.SetReady,
                opCodes.StartBattle,
                opCodes.SubscribeStateSync,
                opCodes.RestoreRoom,
                opCodes.PickHero,
                opCodes.BeginLoading,
                opCodes.ReportLoadingProgress,
                opCodes.ReportAssetsLoaded,
                opCodes.CancelLoading,
                opCodes.GetSnapshot,
                opCodes.RoomStateChanged);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _roomSessionClient.Dispose();
            _ownedRequestClient?.Dispose();
        }
    }
}
