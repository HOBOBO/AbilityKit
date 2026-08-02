using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Moba.GatewayTimeSync;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Room;
using RoomSubscribeStateSyncReq = AbilityKit.Protocol.Room.WireSubscribeStateSyncReq;
using RoomSubscribeStateSyncRes = AbilityKit.Protocol.Room.WireSubscribeStateSyncRes;
using AbilityKit.Game.Flow;
using AbilityKit.Demo.Common.Rooms;
using WireRoomOperationRes = AbilityKit.Protocol.Room.WireRoomOperationRes;
using WireBeginLoadingReq = AbilityKit.Protocol.Room.WireBeginLoadingReq;
using WireReportAssetsLoadedReq = AbilityKit.Protocol.Room.WireReportAssetsLoadedReq;
using WireCancelLoadingReq = AbilityKit.Protocol.Room.WireCancelLoadingReq;
using WireGetSnapshotReq = AbilityKit.Protocol.Room.WireGetSnapshotReq;
using WireRestoreRoomReq = AbilityKit.Protocol.Room.WireRestoreRoomReq;
using WireRestoreRoomRes = AbilityKit.Protocol.Room.WireRestoreRoomRes;
using WireRoomStateChangedPush = AbilityKit.Protocol.Room.WireRoomStateChangedPush;
using WireRoomSnapshotRes = AbilityKit.Protocol.Room.WireRoomSnapshotRes;
using WireRoomJoinKind = AbilityKit.Protocol.Room.WireRoomJoinKind;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed class GatewayRoomClient : IGatewayRoomClient, IDemoRoomDirectoryClient
    {
        private readonly IConnection _connection;
        private readonly RequestClient _request;
        private readonly GatewayRoomOpCodes _opCodes;
        private long _nextBattleInputCommandSequence;

        public GatewayRoomClient(IConnection connection, GatewayRoomOpCodes opCodes)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _opCodes = opCodes;
            _request = new RequestClient(connection);
        }

        public Task<ArraySegment<byte>> SendRawRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return _request.SendRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        public async Task<GatewayTimeSyncResult> TimeSyncAsync(uint timeSyncOpCode, long clientSendTicks, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var req = new WireTimeSyncReq(clientSendTicks);
            var payload = WireTimeSyncBinary.Serialize(in req);
            var resp = await _request.SendRequestAsync(timeSyncOpCode, payload, timeout, cancellationToken);
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
            var resp = await _request.SendRequestAsync(guestLoginOpCode, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginRes>(resp);
            return wire.Success ? wire.SessionToken ?? string.Empty : string.Empty;
        }

        public async Task<DemoRoomDirectoryResult> ListRoomsAsync(
            DemoRoomDirectoryQuery query,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var payload = DemoRoomGatewayDirectoryCodec.SerializeQuery(in query);
            var resp = await _request.SendRequestAsync(
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

            var req = new WireCreateRoomReq
            {
                SessionToken = sessionToken,
                Region = region,
                ServerId = serverId,
                RoomType = roomType,
                Title = title,
                IsPublic = isPublic,
                MaxPlayers = maxPlayers,
                Tags = ToDictionary(tags)
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.CreateRoom, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireCreateRoomRes>(respPayload);
            return new GatewayCreateRoomResult(wire.RoomId ?? string.Empty, wire.NumericRoomId);
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

            var req = new WireJoinRoomReq
            {
                SessionToken = sessionToken,
                Region = region,
                ServerId = serverId,
                RoomId = roomId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.JoinRoom, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireJoinRoomRes>(respPayload);
            var anchor = ToGatewayAnchor(wire.WorldStartAnchor);
            return new GatewayJoinRoomResult(
                wire.Success,
                wire.RoomId,
                wire.NumericRoomId,
                string.Empty,
                in anchor,
                wire.Message,
                wire.Snapshot.BattleId,
                wire.Snapshot.CanStart,
                wire.ServerNowTicks,
                wire.Snapshot.WorldId,
                wire.CurrentPlayerId);
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

            var req = new WireRoomReadyReq
            {
                SessionToken = sessionToken,
                RoomId = roomId,
                Ready = ready
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.SetReady, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomSnapshotRes>(respPayload);
            return new GatewayRoomSnapshotResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message,
                wire.RoomId,
                wire.NumericRoomId);
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

            var req = new WireRoomPickHeroReq
            {
                SessionToken = sessionToken,
                RoomId = roomId,
                HeroId = heroId,
                TeamId = teamId,
                SpawnPointId = spawnPointId,
                Level = level,
                AttributeTemplateId = attributeTemplateId,
                BasicAttackSkillId = basicAttackSkillId,
                SkillIds = ToList(skillIds)
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.PickHero, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomSnapshotRes>(respPayload);
            return new GatewayRoomSnapshotResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message,
                wire.RoomId,
                wire.NumericRoomId);
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

            var req = new WireStartRoomBattleReq
            {
                SessionToken = sessionToken,
                RoomId = roomId,
                GameplayId = gameplayId,
                RuleSetId = ruleSetId,
                ConfigVersion = configVersion,
                ProtocolVersion = protocolVersion,
                WorldType = worldType ?? string.Empty,
                ClientId = clientId ?? string.Empty
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.StartBattle, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireStartRoomBattleRes>(respPayload);
            return new GatewayStartBattleResult(wire.BattleId ?? string.Empty, wire.WorldId, wire.Started);
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

            var req = new RoomSubscribeStateSyncReq
            {
                SessionToken = sessionToken,
                BattleId = battleId,
                RoomId = roomId ?? string.Empty
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.SubscribeStateSync, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<RoomSubscribeStateSyncRes>(respPayload);
            return new GatewayStateSyncSubscriptionResult(wire.Success);
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
            var respPayload = await _request.SendRequestAsync(_opCodes.SubmitBattleInput, payload, timeout, cancellationToken);
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

            var req = new WireBeginLoadingReq
            {
                SessionToken = sessionToken,
                RoomId = roomId,
                ExpectedRevision = expectedRevision,
                CommandId = commandId ?? string.Empty
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.BeginLoading, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToOperationResult(wire);
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

            var req = new WireReportAssetsLoadedReq
            {
                SessionToken = sessionToken,
                RoomId = roomId,
                LaunchGeneration = launchGeneration,
                ManifestVersion = manifestVersion,
                ManifestHash = manifestHash ?? string.Empty,
                CommandId = commandId ?? string.Empty
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.ReportAssetsLoaded, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToOperationResult(wire);
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

            var req = new WireReportLoadingProgressReq
            {
                SessionToken = sessionToken,
                RoomId = roomId,
                LaunchGeneration = launchGeneration,
                ManifestVersion = manifestVersion,
                ManifestHash = manifestHash ?? string.Empty,
                Progress = progress
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.ReportLoadingProgress, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToOperationResult(wire);
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

            var req = new WireCancelLoadingReq
            {
                SessionToken = sessionToken,
                RoomId = roomId,
                ExpectedRevision = expectedRevision,
                CommandId = commandId ?? string.Empty
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.CancelLoading, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToOperationResult(wire);
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

            var req = new WireLeaveRoomReq
            {
                SessionToken = sessionToken,
                RoomId = roomId,
                ExpectedRevision = expectedRevision,
                CommandId = commandId ?? string.Empty
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.LeaveRoom, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToOperationResult(wire);
        }

        public async Task<GatewayGetSnapshotResult> GetSnapshotAsync(
            string sessionToken,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));

            var req = new WireGetSnapshotReq
            {
                SessionToken = sessionToken,
                RoomId = roomId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.GetSnapshot, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomSnapshotRes>(respPayload);
            var wireSnapshot = wire.Snapshot;
            var snapshot = ClientRoomSnapshotMapper.ToClientSnapshot(wireSnapshot);
            return new GatewayGetSnapshotResult(wire.Success, wire.RoomId ?? string.Empty, wire.NumericRoomId, snapshot, wire.Message ?? string.Empty);
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

            var req = new WireRestoreRoomReq
            {
                SessionToken = sessionToken,
                Region = region,
                ServerId = serverId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _request.SendRequestAsync(_opCodes.RestoreRoom, payload, timeout, cancellationToken);
            var wire = WireRoomGatewayBinary.Deserialize<WireRestoreRoomRes>(respPayload);
            var wireSnapshot = wire.Snapshot;
            var snapshot = ClientRoomSnapshotMapper.ToClientSnapshot(wireSnapshot);
            var anchor = ToGatewayAnchor(wire.WorldStartAnchor);
            return new GatewayRestoreRoomResult(
                wire.Success,
                wire.HasActiveRoom,
                wire.IsInBattle,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                snapshot,
                in anchor,
                wire.Message ?? string.Empty,
                ToJoinKind(wire.JoinKind),
                wire.ServerNowTicks,
                wire.CurrentPlayerId);
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

        private static GatewayRoomOperationResult ToOperationResult(in WireRoomOperationRes wire)
        {
            var wireSnapshot = wire.Snapshot;
            var snapshot = ClientRoomSnapshotMapper.ToClientSnapshot(wireSnapshot);
            return new GatewayRoomOperationResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message ?? string.Empty,
                wire.RoomRevision,
                snapshot);
        }

        private static RoomGatewayJoinKind ToJoinKind(WireRoomJoinKind kind)
        {
            switch (kind)
            {
                case WireRoomJoinKind.Reconnect:
                    return RoomGatewayJoinKind.Reconnect;
                case WireRoomJoinKind.LateJoin:
                    return RoomGatewayJoinKind.LateJoin;
                default:
                    return RoomGatewayJoinKind.TeamLobby;
            }
        }

        private static Dictionary<string, string> ToDictionary(IReadOnlyDictionary<string, string> source)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            var result = new Dictionary<string, string>(source.Count);
            foreach (var kv in source)
            {
                result[kv.Key ?? string.Empty] = kv.Value ?? string.Empty;
            }

            return result;
        }

        private static List<int> ToList(IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            var result = new List<int>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(source[i]);
            }

            return result;
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

        private static GatewayWorldStartAnchor ToGatewayAnchor(in WireWorldStartAnchor anchor)
        {
            return new GatewayWorldStartAnchor(
                anchor.StartServerTicks,
                anchor.ServerTickFrequency,
                anchor.StartFrame,
                anchor.FixedDeltaSeconds);
        }

        private static byte[] CopySegment(ArraySegment<byte> segment)
        {
            if (segment.Array == null || segment.Count <= 0)
            {
                return Array.Empty<byte>();
            }

            if (segment.Offset == 0 && segment.Count == segment.Array.Length)
            {
                return segment.Array;
            }

            var bytes = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, bytes, 0, segment.Count);
            return bytes;
        }
    }
}
