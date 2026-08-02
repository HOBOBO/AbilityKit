#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Ability.Host.Extensions.Client.FrameSync;
using System.Threading.Tasks;
using AbilityKit.Protocol.Room;
using AbilityKit.Demo.Common.Rooms;

namespace AbilityKit.Demo.Shooter.View
{
    public interface IShooterRoomGatewayRoomClient
    {
        Task<ShooterGatewayGuestLoginResult> GuestLoginAsync(
            ShooterGatewayGuestLoginRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayAccountLoginResult> AccountLoginAsync(
            ShooterGatewayAccountLoginRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayListRoomsResult> ListRoomsAsync(
            ShooterGatewayListRoomsRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayCreateRoomResult> CreateRoomAsync(
            ShooterGatewayCreateRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayJoinRoomResult> JoinRoomAsync(
            ShooterGatewayJoinRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayRoomSnapshotResult> SetReadyAsync(
            ShooterGatewayReadyRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayStartBattleResult> StartBattleAsync(
            ShooterGatewayStartBattleRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayRoomOperationResult> BeginLoadingAsync(
            ShooterGatewayBeginLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayRoomOperationResult> ReportAssetsLoadedAsync(
            ShooterGatewayReportAssetsLoadedRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayRoomOperationResult> ReportLoadingProgressAsync(
            ShooterGatewayReportLoadingProgressRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayRoomOperationResult> CancelLoadingAsync(
            ShooterGatewayCancelLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayRoomOperationResult> LeaveRoomAsync(
            ShooterGatewayLeaveRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayGetRoomSnapshotResult> GetSnapshotAsync(
            ShooterGatewayGetRoomSnapshotRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            ShooterGatewayStateSyncSubscriptionRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayReliableBattleEventAckResult> AcknowledgeReliableBattleEventsAsync(
            ShooterGatewayReliableBattleEventAckRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayFullStateSyncRequestResult> RequestFullStateSyncAsync(
            ShooterGatewayFullStateSyncRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterGatewayRestoreRoomResult> RestoreRoomAsync(
            ShooterGatewayRestoreRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public interface IShooterRoomGatewaySnapshotFeed
    {
        ShooterGatewayStagedRoomSnapshot? Current { get; }

        event Action<ShooterGatewayStagedRoomSnapshot>? SnapshotChanged;
    }

    public sealed class ShooterRoomGatewayRoomClient :
        IShooterRoomGatewayRoomClient,
        IShooterRoomGatewaySnapshotFeed,
        IDemoRoomDirectoryClient,
        IDisposable
    {
        private readonly IShooterRoomGatewayRequestTransport _transport;
        private readonly ShooterRoomGatewayRoomOpCodes _opCodes;
        private readonly object _snapshotGate = new object();
        private ShooterGatewayStagedRoomSnapshot? _current;

        public ShooterRoomGatewayRoomClient(IShooterRoomGatewayRequestTransport transport)
            : this(transport, ShooterRoomGatewayRoomOpCodes.Default)
        {
        }

        public ShooterRoomGatewayRoomClient(IShooterRoomGatewayRequestTransport transport, ShooterRoomGatewayRoomOpCodes opCodes)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _opCodes = opCodes;
            if (_transport is IShooterRoomGatewayPushTransport pushTransport)
            {
                pushTransport.ServerPushReceived += HandleServerPush;
            }
        }

        public ShooterGatewayStagedRoomSnapshot? Current
        {
            get { lock (_snapshotGate) return _current; }
        }

        public event Action<ShooterGatewayStagedRoomSnapshot>? SnapshotChanged;

        public async Task<ShooterGatewayGuestLoginResult> GuestLoginAsync(
            ShooterGatewayGuestLoginRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateGuestLogin(in request);

            var req = new WireRoomGuestLoginReq
            {
                GuestId = request.GuestId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.GuestLogin, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginRes>(respPayload);
            return new ShooterGatewayGuestLoginResult(wire.Success, wire.SessionToken ?? string.Empty, wire.AccountId ?? string.Empty, wire.Message ?? string.Empty);
        }

        public async Task<ShooterGatewayAccountLoginResult> AccountLoginAsync(
            ShooterGatewayAccountLoginRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateAccountLogin(in request);

            var req = new WireRoomAccountLoginReq
            {
                AccountId = request.AccountId,
                ExpireSeconds = request.ExpireSeconds,
                KickExisting = request.KickExisting
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.AccountLogin, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomAccountLoginRes>(respPayload);
            return new ShooterGatewayAccountLoginResult(
                wire.Success,
                wire.SessionToken ?? string.Empty,
                wire.AccountId ?? string.Empty,
                wire.ExpireAtUnixMs,
                wire.KickedSessionToken ?? string.Empty,
                wire.Message ?? string.Empty);
        }

        public async Task<ShooterGatewayListRoomsResult> ListRoomsAsync(
            ShooterGatewayListRoomsRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateListRooms(in request);

            var req = new WireListRoomsReq
            {
                SessionToken = request.SessionToken,
                Region = request.Region,
                ServerId = request.ServerId,
                Offset = request.Offset,
                Limit = request.Limit,
                RoomType = request.RoomType
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.ListRooms, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireListRoomsRes>(respPayload);
            return new ShooterGatewayListRoomsResult(wire.Success, ToRoomSummaries(wire.Rooms), wire.NextOffset, wire.Message ?? string.Empty);
        }

        async Task<DemoRoomDirectoryResult> IDemoRoomDirectoryClient.ListRoomsAsync(
            DemoRoomDirectoryQuery query,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            var payload = DemoRoomGatewayDirectoryCodec.SerializeQuery(in query);
            var response = await _transport.SendRequestAsync(
                _opCodes.ListRooms,
                payload,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return DemoRoomGatewayDirectoryCodec.DeserializeResult(response);
        }

        public async Task<ShooterGatewayCreateRoomResult> CreateRoomAsync(
            ShooterGatewayCreateRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateCreateRoom(in request);

            var req = new WireCreateRoomReq
            {
                SessionToken = request.SessionToken,
                Region = request.Region,
                ServerId = request.ServerId,
                RoomType = request.RoomType,
                Title = request.Title,
                IsPublic = request.IsPublic,
                MaxPlayers = request.MaxPlayers,
                Tags = ToDictionary(request.Tags)
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.CreateRoom, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireCreateRoomRes>(respPayload);
            return new ShooterGatewayCreateRoomResult(wire.Success, wire.RoomId ?? string.Empty, wire.NumericRoomId, wire.Message ?? string.Empty);
        }

        public async Task<ShooterGatewayJoinRoomResult> JoinRoomAsync(
            ShooterGatewayJoinRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateJoinRoom(in request);

            var req = new WireJoinRoomReq
            {
                SessionToken = request.SessionToken,
                Region = request.Region,
                ServerId = request.ServerId,
                RoomId = request.RoomId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.JoinRoom, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireJoinRoomRes>(respPayload);
            var worldStartAnchor = wire.WorldStartAnchor;
            var anchor = ToAnchor(in worldStartAnchor);
            return new ShooterGatewayJoinRoomResult(
                wire.Success,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                in anchor,
                wire.Message ?? string.Empty,
                wire.Snapshot.BattleId ?? string.Empty,
                wire.Snapshot.CanStart,
                ToJoinKind(wire.JoinKind),
                wire.ServerNowTicks,
                wire.Snapshot.WorldId,
                wire.CurrentPlayerId);
        }

        public async Task<ShooterGatewayRoomSnapshotResult> SetReadyAsync(
            ShooterGatewayReadyRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateReady(in request);

            var req = new WireRoomReadyReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                Ready = request.Ready
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.SetReady, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomSnapshotRes>(respPayload);
            return new ShooterGatewayRoomSnapshotResult(wire.Success, wire.RoomId ?? string.Empty, wire.NumericRoomId, wire.Message ?? string.Empty, wire.Snapshot.BattleId ?? string.Empty, wire.Snapshot.CanStart);
        }

        public async Task<ShooterGatewayStartBattleResult> StartBattleAsync(
            ShooterGatewayStartBattleRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateStartBattle(in request);

            var req = new WireStartRoomBattleReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                GameplayId = request.GameplayId,
                RuleSetId = request.RuleSetId,
                ConfigVersion = request.ConfigVersion,
                ProtocolVersion = request.ProtocolVersion,
                WorldType = request.WorldType,
                ClientId = request.ClientId,
                SyncTemplateId = request.SyncTemplateId,
                SyncModel = request.SyncModel,
                NetworkEnvironmentId = request.NetworkEnvironmentId,
                CarrierName = request.CarrierName,
                EnableAuthoritativeWorld = request.EnableAuthoritativeWorld,
                InterpolationEnabled = request.InterpolationEnabled,
                InputDelayFrames = request.InputDelayFrames
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.StartBattle, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireStartRoomBattleRes>(respPayload);
            var worldStartAnchor = wire.WorldStartAnchor;
            return new ShooterGatewayStartBattleResult(
                wire.Success,
                wire.BattleId ?? string.Empty,
                wire.WorldId,
                wire.Started,
                ToAnchor(in worldStartAnchor),
                wire.ServerNowTicks,
                wire.Message ?? string.Empty);
        }

        public async Task<ShooterGatewayRoomOperationResult> BeginLoadingAsync(
            ShooterGatewayBeginLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRoomOperation(request.SessionToken, request.RoomId);

            var req = new WireBeginLoadingReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                ExpectedRevision = request.ExpectedRevision,
                CommandId = request.CommandId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.BeginLoading, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToRoomOperationResult(in wire);
        }

        public async Task<ShooterGatewayRoomOperationResult> ReportAssetsLoadedAsync(
            ShooterGatewayReportAssetsLoadedRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRoomOperation(request.SessionToken, request.RoomId);

            var req = new WireReportAssetsLoadedReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                LaunchGeneration = request.LaunchGeneration,
                ManifestVersion = request.ManifestVersion,
                ManifestHash = request.ManifestHash,
                CommandId = request.CommandId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.ReportAssetsLoaded, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToRoomOperationResult(in wire);
        }

        public async Task<ShooterGatewayRoomOperationResult> ReportLoadingProgressAsync(
            ShooterGatewayReportLoadingProgressRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRoomOperation(request.SessionToken, request.RoomId);
            if (request.Progress < 0 || request.Progress > 100) throw new ArgumentOutOfRangeException(nameof(request));

            var req = new WireReportLoadingProgressReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                LaunchGeneration = request.LaunchGeneration,
                ManifestVersion = request.ManifestVersion,
                ManifestHash = request.ManifestHash,
                Progress = request.Progress
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.ReportLoadingProgress, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToRoomOperationResult(in wire);
        }

        public async Task<ShooterGatewayRoomOperationResult> CancelLoadingAsync(
            ShooterGatewayCancelLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRoomOperation(request.SessionToken, request.RoomId);
            var req = new WireCancelLoadingReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                ExpectedRevision = request.ExpectedRevision,
                CommandId = request.CommandId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(
                _opCodes.CancelLoading,
                payload,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToRoomOperationResult(in wire);
        }

        public async Task<ShooterGatewayRoomOperationResult> LeaveRoomAsync(
            ShooterGatewayLeaveRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRoomOperation(request.SessionToken, request.RoomId);
            var req = new WireLeaveRoomReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                ExpectedRevision = request.ExpectedRevision,
                CommandId = request.CommandId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.LeaveRoom, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(respPayload);
            return ToRoomOperationResult(in wire);
        }

        public async Task<ShooterGatewayGetRoomSnapshotResult> GetSnapshotAsync(
            ShooterGatewayGetRoomSnapshotRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRoomOperation(request.SessionToken, request.RoomId);

            var req = new WireGetSnapshotReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.GetSnapshot, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRoomSnapshotRes>(respPayload);
            var wireSnapshot = wire.Snapshot;
            var snapshot = ToStagedSnapshot(in wireSnapshot);
            if (wire.Success) PublishSnapshot(snapshot);
            return new ShooterGatewayGetRoomSnapshotResult(
                wire.Success,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                snapshot,
                wire.Message ?? string.Empty,
                wire.ServerNowTicks);
        }

        public async Task<ShooterGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            ShooterGatewayStateSyncSubscriptionRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateStateSyncSubscription(in request);

            var req = new WireSubscribeStateSyncReq
            {
                SessionToken = request.SessionToken,
                BattleId = request.BattleId,
                RoomId = request.RoomId,
                EventEpoch = request.EventEpoch,
                LastEventAck = request.LastEventAck
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.SubscribeStateSync, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireSubscribeStateSyncRes>(respPayload);
            return new ShooterGatewayStateSyncSubscriptionResult(wire.Success, wire.Message ?? string.Empty);
        }

        public async Task<ShooterGatewayReliableBattleEventAckResult> AcknowledgeReliableBattleEventsAsync(
            ShooterGatewayReliableBattleEventAckRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateReliableBattleEventAck(in request);

            var req = new WireAckReliableBattleEventsReq
            {
                SessionToken = request.SessionToken,
                BattleId = request.BattleId,
                RoomId = request.RoomId,
                Epoch = request.Epoch,
                AckSequence = request.AckSequence
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.AckReliableBattleEvents, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireAckReliableBattleEventsRes>(respPayload);
            return new ShooterGatewayReliableBattleEventAckResult(
                wire.Success,
                wire.AcceptedAckSequence,
                wire.Message ?? string.Empty);
        }

        public async Task<ShooterGatewayFullStateSyncRequestResult> RequestFullStateSyncAsync(
            ShooterGatewayFullStateSyncRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateFullStateSyncRequest(in request);

            var req = new WireRequestFullStateSyncReq
            {
                SessionToken = request.SessionToken,
                BattleId = request.BattleId,
                RoomId = request.RoomId,
                WorldId = request.WorldId,
                ClientFrame = request.ClientFrame,
                LastAuthoritativeFrame = request.LastAuthoritativeFrame,
                ClientStateHash = request.ClientStateHash,
                AuthoritativeStateHash = request.AuthoritativeStateHash,
                Reason = request.Reason
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.RequestFullStateSync, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRequestFullStateSyncRes>(respPayload);
            return new ShooterGatewayFullStateSyncRequestResult(wire.Success, wire.Accepted, wire.Message ?? string.Empty, wire.ServerTicks);
        }

        public async Task<ShooterGatewayRestoreRoomResult> RestoreRoomAsync(
            ShooterGatewayRestoreRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRestoreRoom(in request);

            var req = new WireRestoreRoomReq
            {
                SessionToken = request.SessionToken,
                Region = request.Region,
                ServerId = request.ServerId
            };
            var payload = WireRoomGatewayBinary.Serialize(in req);
            var respPayload = await _transport.SendRequestAsync(_opCodes.RestoreRoom, payload, timeout, cancellationToken).ConfigureAwait(false);
            var wire = WireRoomGatewayBinary.Deserialize<WireRestoreRoomRes>(respPayload);
            var worldStartAnchor = wire.WorldStartAnchor;
            var anchor = ToAnchor(in worldStartAnchor);
            return new ShooterGatewayRestoreRoomResult(
                wire.Success,
                wire.HasActiveRoom,
                wire.IsInBattle,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                in anchor,
                wire.Message ?? string.Empty,
                wire.Snapshot.BattleId ?? string.Empty,
                wire.Snapshot.CanStart,
                ToJoinKind(wire.JoinKind),
                wire.ServerNowTicks,
                wire.Snapshot.WorldId,
                ToRestoreStatus(wire.Status),
                ToRestoreErrorCode(wire.ErrorCode),
                wire.CurrentPlayerId);
        }

        private static void ValidateGuestLogin(in ShooterGatewayGuestLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.GuestId)) throw new ArgumentException("guestId is required.", nameof(request));
        }

        private static void ValidateAccountLogin(in ShooterGatewayAccountLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccountId)) throw new ArgumentException("accountId is required.", nameof(request));
            if (request.ExpireSeconds < 0) throw new ArgumentOutOfRangeException(nameof(request));
        }

        private static void ValidateListRooms(in ShooterGatewayListRoomsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Region)) throw new ArgumentException("region is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.ServerId)) throw new ArgumentException("serverId is required.", nameof(request));
            if (request.Offset < 0) throw new ArgumentOutOfRangeException(nameof(request));
            if (request.Limit <= 0) throw new ArgumentOutOfRangeException(nameof(request));
        }

        private static void ValidateCreateRoom(in ShooterGatewayCreateRoomRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Region)) throw new ArgumentException("region is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.ServerId)) throw new ArgumentException("serverId is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.RoomType)) throw new ArgumentException("roomType is required.", nameof(request));
            if (request.MaxPlayers <= 0) throw new ArgumentOutOfRangeException(nameof(request));
        }

        private static void ValidateJoinRoom(in ShooterGatewayJoinRoomRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Region)) throw new ArgumentException("region is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.ServerId)) throw new ArgumentException("serverId is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
        }

        private static void ValidateReady(in ShooterGatewayReadyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
        }

        private static void ValidateStartBattle(in ShooterGatewayStartBattleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
            if (request.GameplayId <= 0) throw new ArgumentOutOfRangeException(nameof(request));
            if (request.ProtocolVersion <= 0) throw new ArgumentOutOfRangeException(nameof(request));
        }

        private static void ValidateRoomOperation(string sessionToken, string roomId)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
        }

        private static void ValidateStateSyncSubscription(in ShooterGatewayStateSyncSubscriptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.BattleId)) throw new ArgumentException("battleId is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
            if (request.LastEventAck < 0) throw new ArgumentOutOfRangeException(nameof(request));
        }

        private static void ValidateReliableBattleEventAck(in ShooterGatewayReliableBattleEventAckRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.BattleId)) throw new ArgumentException("battleId is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Epoch)) throw new ArgumentException("epoch is required.", nameof(request));
            if (request.AckSequence < 0) throw new ArgumentOutOfRangeException(nameof(request));
        }

        private static void ValidateFullStateSyncRequest(in ShooterGatewayFullStateSyncRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.BattleId)) throw new ArgumentException("battleId is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
        }

        private static void ValidateRestoreRoom(in ShooterGatewayRestoreRoomRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Region)) throw new ArgumentException("region is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.ServerId)) throw new ArgumentException("serverId is required.", nameof(request));
        }

        private static IReadOnlyList<ShooterGatewayRoomSummary> ToRoomSummaries(List<WireRoomSummary>? rooms)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return Array.Empty<ShooterGatewayRoomSummary>();
            }

            var result = new ShooterGatewayRoomSummary[rooms.Count];
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                result[i] = new ShooterGatewayRoomSummary(
                    room.Region ?? string.Empty,
                    room.ServerId ?? string.Empty,
                    room.RoomId ?? string.Empty,
                    room.RoomType ?? string.Empty,
                    room.Title ?? string.Empty,
                    room.IsPublic,
                    room.MaxPlayers,
                    room.PlayerCount,
                    room.OwnerAccountId ?? string.Empty,
                    room.CreatedAtUnixMs,
                    ToDictionary(room.Tags));
            }

            return result;
        }

        private static Dictionary<string, string>? ToDictionary(IReadOnlyDictionary<string, string>? source)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            var result = new Dictionary<string, string>(source.Count);
            foreach (var kv in source)
            {
                result[kv.Key] = kv.Value ?? string.Empty;
            }

            return result;
        }

        private static ShooterGatewayRoomJoinKind ToJoinKind(WireRoomJoinKind joinKind)
        {
            return joinKind switch
            {
                WireRoomJoinKind.Reconnect => ShooterGatewayRoomJoinKind.Reconnect,
                WireRoomJoinKind.LateJoin => ShooterGatewayRoomJoinKind.LateJoin,
                _ => ShooterGatewayRoomJoinKind.TeamLobby
            };
        }

        private static ShooterGatewayRoomRestoreStatus ToRestoreStatus(WireRoomRestoreStatus status)
        {
            switch (status)
            {
                case WireRoomRestoreStatus.NoActiveRoom:
                    return ShooterGatewayRoomRestoreStatus.NoActiveRoom;
                case WireRoomRestoreStatus.NotMember:
                    return ShooterGatewayRoomRestoreStatus.NotMember;
                case WireRoomRestoreStatus.RoomClosed:
                    return ShooterGatewayRoomRestoreStatus.RoomClosed;
                case WireRoomRestoreStatus.RoomExpired:
                    return ShooterGatewayRoomRestoreStatus.RoomExpired;
                case WireRoomRestoreStatus.InvalidSession:
                    return ShooterGatewayRoomRestoreStatus.InvalidSession;
                case WireRoomRestoreStatus.Failed:
                    return ShooterGatewayRoomRestoreStatus.Failed;
                default:
                    return ShooterGatewayRoomRestoreStatus.Restored;
            }
        }

        private static ShooterGatewayRoomRestoreErrorCode ToRestoreErrorCode(WireRoomRestoreErrorCode errorCode)
        {
            switch (errorCode)
            {
                case WireRoomRestoreErrorCode.NoAccountRoomMapping:
                    return ShooterGatewayRoomRestoreErrorCode.NoAccountRoomMapping;
                case WireRoomRestoreErrorCode.AccountNotInRoom:
                    return ShooterGatewayRoomRestoreErrorCode.AccountNotInRoom;
                case WireRoomRestoreErrorCode.RoomClosed:
                    return ShooterGatewayRoomRestoreErrorCode.RoomClosed;
                case WireRoomRestoreErrorCode.RoomExpired:
                    return ShooterGatewayRoomRestoreErrorCode.RoomExpired;
                case WireRoomRestoreErrorCode.InvalidSession:
                    return ShooterGatewayRoomRestoreErrorCode.InvalidSession;
                case WireRoomRestoreErrorCode.InternalError:
                    return ShooterGatewayRoomRestoreErrorCode.InternalError;
                default:
                    return ShooterGatewayRoomRestoreErrorCode.None;
            }
        }

        private ShooterGatewayRoomOperationResult ToRoomOperationResult(in WireRoomOperationRes wire)
        {
            var wireSnapshot = wire.Snapshot;
            var snapshot = string.IsNullOrWhiteSpace(wireSnapshot.Summary.RoomId)
                ? null
                : ToStagedSnapshot(in wireSnapshot);
            if (wire.Success && snapshot != null) PublishSnapshot(snapshot);
            return new ShooterGatewayRoomOperationResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message ?? string.Empty,
                wire.RoomRevision,
                snapshot);
        }

        private void HandleServerPush(uint opCode, ArraySegment<byte> payload)
        {
            if (opCode != RoomGatewayOpCodes.RoomStateChanged) return;

            try
            {
                var push = WireRoomGatewayBinary.Deserialize<WireRoomStateChangedPush>(payload);
                var wireSnapshot = push.Snapshot;
                PublishSnapshot(ToStagedSnapshot(in wireSnapshot));
            }
            catch (Exception)
            {
                // A malformed or incompatible push must not break request/response traffic.
            }
        }

        private void PublishSnapshot(ShooterGatewayStagedRoomSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.RoomId)) return;

            lock (_snapshotGate)
            {
                if (_current != null &&
                    string.Equals(_current.RoomId, snapshot.RoomId, StringComparison.Ordinal) &&
                    snapshot.RoomRevision <= _current.RoomRevision)
                {
                    return;
                }

                _current = snapshot;
            }

            SnapshotChanged?.Invoke(snapshot);
        }

        private static ShooterGatewayStagedRoomSnapshot ToStagedSnapshot(in WireRoomSnapshot wire)
        {
            var wireAnchor = wire.WorldStartAnchor;
            var anchor = ToAnchor(in wireAnchor);
            var snapshot = new ShooterGatewayStagedRoomSnapshot(
                wire.Summary.RoomId ?? string.Empty,
                wire.Phase,
                wire.PhaseReason ?? string.Empty,
                wire.LaunchGeneration,
                wire.LoadingDeadlineUnixMs,
                wire.LaunchManifestHash ?? string.Empty,
                wire.LaunchManifestVersion,
                wire.LastStartFailureCode ?? string.Empty,
                wire.RoomRevision,
                wire.LastEventSequence,
                wire.CanStart,
                wire.BattleId ?? string.Empty,
                wire.WorldId,
                in anchor)
            {
                OwnerAccountId = wire.Summary.OwnerAccountId ?? string.Empty,
                Players = ToStagedPlayers(wire.Players)
            };
            return snapshot;
        }

        private static IReadOnlyList<ShooterGatewayStagedRoomPlayerSnapshot> ToStagedPlayers(
            List<WireRoomPlayerSnapshot> players)
        {
            if (players == null || players.Count == 0)
            {
                return Array.Empty<ShooterGatewayStagedRoomPlayerSnapshot>();
            }

            var result = new ShooterGatewayStagedRoomPlayerSnapshot[players.Count];
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                result[i] = new ShooterGatewayStagedRoomPlayerSnapshot(
                    player.AccountId ?? string.Empty,
                    player.PlayerId,
                    player.IsOnline,
                    player.LobbyReady,
                    player.AssetsLoaded,
                    player.LoadingProgress);
            }

            return result;
        }

        private static ShooterGatewayWorldStartAnchor ToAnchor(in WireWorldStartAnchor anchor)
        {
            return new ShooterGatewayWorldStartAnchor(anchor.StartServerTicks, anchor.ServerTickFrequency, anchor.StartFrame, anchor.FixedDeltaSeconds);
        }

        public void Dispose()
        {
            if (_transport is IShooterRoomGatewayPushTransport pushTransport)
            {
                pushTransport.ServerPushReceived -= HandleServerPush;
            }

            SnapshotChanged = null;
        }
    }

    public readonly struct ShooterRoomGatewayRoomOpCodes
    {
        public static ShooterRoomGatewayRoomOpCodes Default => new ShooterRoomGatewayRoomOpCodes(
            RoomGatewayOpCodes.GuestLogin,
            RoomGatewayOpCodes.AccountLogin,
            RoomGatewayOpCodes.ListRooms,
            RoomGatewayOpCodes.CreateRoom,
            RoomGatewayOpCodes.JoinRoom,
            RoomGatewayOpCodes.SubscribeStateSync,
            RoomGatewayOpCodes.SetReady,
            RoomGatewayOpCodes.StartBattle,
            RoomGatewayOpCodes.RequestFullStateSync,
            RoomGatewayOpCodes.RestoreRoom,
            RoomGatewayOpCodes.AckReliableBattleEvents);

        public readonly uint GuestLogin;
        public readonly uint AccountLogin;
        public readonly uint ListRooms;
        public readonly uint CreateRoom;
        public readonly uint JoinRoom;
        public readonly uint SubscribeStateSync;
        public readonly uint SetReady;
        public readonly uint StartBattle;
        public readonly uint RequestFullStateSync;
        public readonly uint RestoreRoom;
        public readonly uint AckReliableBattleEvents;
        public readonly uint BeginLoading;
        public readonly uint ReportAssetsLoaded;
        public readonly uint ReportLoadingProgress;
        public readonly uint GetSnapshot;
        public readonly uint LeaveRoom;
        public readonly uint CancelLoading;

        public ShooterRoomGatewayRoomOpCodes(uint createRoom, uint joinRoom, uint subscribeStateSync, uint setReady, uint startBattle)
            : this(RoomGatewayOpCodes.GuestLogin, RoomGatewayOpCodes.ListRooms, createRoom, joinRoom, subscribeStateSync, setReady, startBattle, RoomGatewayOpCodes.RequestFullStateSync, RoomGatewayOpCodes.RestoreRoom)
        {
        }

        public ShooterRoomGatewayRoomOpCodes(uint createRoom, uint joinRoom, uint subscribeStateSync, uint setReady, uint startBattle, uint requestFullStateSync)
            : this(RoomGatewayOpCodes.GuestLogin, RoomGatewayOpCodes.ListRooms, createRoom, joinRoom, subscribeStateSync, setReady, startBattle, requestFullStateSync, RoomGatewayOpCodes.RestoreRoom)
        {
        }

        public ShooterRoomGatewayRoomOpCodes(uint createRoom, uint joinRoom, uint subscribeStateSync, uint setReady, uint startBattle, uint requestFullStateSync, uint restoreRoom)
            : this(RoomGatewayOpCodes.GuestLogin, RoomGatewayOpCodes.ListRooms, createRoom, joinRoom, subscribeStateSync, setReady, startBattle, requestFullStateSync, restoreRoom)
        {
        }

        public ShooterRoomGatewayRoomOpCodes(uint guestLogin, uint listRooms, uint createRoom, uint joinRoom, uint subscribeStateSync, uint setReady, uint startBattle, uint requestFullStateSync, uint restoreRoom)
            : this(guestLogin, RoomGatewayOpCodes.AccountLogin, listRooms, createRoom, joinRoom, subscribeStateSync, setReady, startBattle, requestFullStateSync, restoreRoom)
        {
        }

        public ShooterRoomGatewayRoomOpCodes(uint guestLogin, uint accountLogin, uint listRooms, uint createRoom, uint joinRoom, uint subscribeStateSync, uint setReady, uint startBattle, uint requestFullStateSync, uint restoreRoom)
            : this(guestLogin, accountLogin, listRooms, createRoom, joinRoom, subscribeStateSync, setReady, startBattle, requestFullStateSync, restoreRoom, RoomGatewayOpCodes.AckReliableBattleEvents)
        {
        }

        public ShooterRoomGatewayRoomOpCodes(uint guestLogin, uint accountLogin, uint listRooms, uint createRoom, uint joinRoom, uint subscribeStateSync, uint setReady, uint startBattle, uint requestFullStateSync, uint restoreRoom, uint ackReliableBattleEvents)
        {
            GuestLogin = guestLogin;
            AccountLogin = accountLogin;
            ListRooms = listRooms;
            CreateRoom = createRoom;
            JoinRoom = joinRoom;
            SubscribeStateSync = subscribeStateSync;
            SetReady = setReady;
            StartBattle = startBattle;
            RequestFullStateSync = requestFullStateSync;
            RestoreRoom = restoreRoom;
            AckReliableBattleEvents = ackReliableBattleEvents;
            BeginLoading = RoomGatewayOpCodes.BeginLoading;
            ReportAssetsLoaded = RoomGatewayOpCodes.ReportAssetsLoaded;
            ReportLoadingProgress = RoomGatewayOpCodes.ReportLoadingProgress;
            GetSnapshot = RoomGatewayOpCodes.GetSnapshot;
            LeaveRoom = RoomGatewayOpCodes.LeaveRoom;
            CancelLoading = RoomGatewayOpCodes.CancelLoading;
        }
    }

    public readonly struct ShooterGatewayGuestLoginRequest
    {
        public readonly string GuestId;

        public ShooterGatewayGuestLoginRequest(string guestId)
        {
            GuestId = guestId ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayAccountLoginRequest
    {
        public readonly string AccountId;
        public readonly int ExpireSeconds;
        public readonly bool KickExisting;

        public ShooterGatewayAccountLoginRequest(string accountId, int expireSeconds = 0, bool kickExisting = true)
        {
            AccountId = accountId ?? string.Empty;
            ExpireSeconds = expireSeconds;
            KickExisting = kickExisting;
        }
    }

    public readonly struct ShooterGatewayListRoomsRequest
    {
        public readonly string SessionToken;
        public readonly string Region;
        public readonly string ServerId;
        public readonly int Offset;
        public readonly int Limit;
        public readonly string RoomType;

        public ShooterGatewayListRoomsRequest(string sessionToken, string region, string serverId, int offset = 0, int limit = 20, string roomType = ShooterGameplay.RoomType)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            Offset = offset;
            Limit = limit;
            RoomType = roomType ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayCreateRoomRequest
    {
        public readonly string SessionToken;
        public readonly string Region;
        public readonly string ServerId;
        public readonly string RoomType;
        public readonly string Title;
        public readonly bool IsPublic;
        public readonly int MaxPlayers;
        public readonly IReadOnlyDictionary<string, string>? Tags;

        public ShooterGatewayCreateRoomRequest(string sessionToken, string region, string serverId, string roomType, string title, bool isPublic, int maxPlayers, IReadOnlyDictionary<string, string>? tags = null)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            RoomType = roomType ?? string.Empty;
            Title = title ?? string.Empty;
            IsPublic = isPublic;
            MaxPlayers = maxPlayers;
            Tags = tags;
        }
    }

    public readonly struct ShooterGatewayJoinRoomRequest
    {
        public readonly string SessionToken;
        public readonly string Region;
        public readonly string ServerId;
        public readonly string RoomId;

        public ShooterGatewayJoinRoomRequest(string sessionToken, string region, string serverId, string roomId)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayRestoreRoomRequest
    {
        public readonly string SessionToken;
        public readonly string Region;
        public readonly string ServerId;

        public ShooterGatewayRestoreRoomRequest(string sessionToken, string region, string serverId)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayReadyRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly bool Ready;

        public ShooterGatewayReadyRequest(string sessionToken, string roomId, bool ready)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            Ready = ready;
        }
    }

    public readonly struct ShooterGatewayStartBattleRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly int GameplayId;
        public readonly int RuleSetId;
        public readonly int ConfigVersion;
        public readonly int ProtocolVersion;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly string SyncTemplateId;
        public readonly int SyncModel;
        public readonly string NetworkEnvironmentId;
        public readonly string CarrierName;
        public readonly bool EnableAuthoritativeWorld;
        public readonly bool InterpolationEnabled;
        public readonly int InputDelayFrames;

        public ShooterGatewayStartBattleRequest(
            string sessionToken,
            string roomId,
            int gameplayId,
            int ruleSetId,
            int configVersion,
            int protocolVersion,
            string worldType,
            string clientId,
            string syncTemplateId = "",
            int syncModel = 0,
            string networkEnvironmentId = "",
            string carrierName = "",
            bool enableAuthoritativeWorld = true,
            bool interpolationEnabled = false,
            int inputDelayFrames = 0)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            GameplayId = gameplayId;
            RuleSetId = ruleSetId;
            ConfigVersion = configVersion;
            ProtocolVersion = protocolVersion;
            WorldType = worldType ?? string.Empty;
            ClientId = clientId ?? string.Empty;
            SyncTemplateId = syncTemplateId ?? string.Empty;
            SyncModel = syncModel;
            NetworkEnvironmentId = networkEnvironmentId ?? string.Empty;
            CarrierName = carrierName ?? string.Empty;
            EnableAuthoritativeWorld = enableAuthoritativeWorld;
            InterpolationEnabled = interpolationEnabled;
            InputDelayFrames = inputDelayFrames < 0 ? 0 : inputDelayFrames;
        }
    }

    public readonly struct ShooterGatewayBeginLoadingRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long? ExpectedRevision;
        public readonly string CommandId;

        public ShooterGatewayBeginLoadingRequest(string sessionToken, string roomId, long? expectedRevision, string commandId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ExpectedRevision = expectedRevision;
            CommandId = commandId ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayReportAssetsLoadedRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long LaunchGeneration;
        public readonly int ManifestVersion;
        public readonly string ManifestHash;
        public readonly string CommandId;

        public ShooterGatewayReportAssetsLoadedRequest(string sessionToken, string roomId, long launchGeneration, int manifestVersion, string manifestHash, string commandId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            LaunchGeneration = launchGeneration;
            ManifestVersion = manifestVersion;
            ManifestHash = manifestHash ?? string.Empty;
            CommandId = commandId ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayReportLoadingProgressRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long LaunchGeneration;
        public readonly int ManifestVersion;
        public readonly string ManifestHash;
        public readonly int Progress;

        public ShooterGatewayReportLoadingProgressRequest(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            int progress)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            LaunchGeneration = launchGeneration;
            ManifestVersion = manifestVersion;
            ManifestHash = manifestHash ?? string.Empty;
            Progress = progress;
        }
    }

    public readonly struct ShooterGatewayLeaveRoomRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long? ExpectedRevision;
        public readonly string CommandId;

        public ShooterGatewayLeaveRoomRequest(string sessionToken, string roomId, long? expectedRevision, string commandId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ExpectedRevision = expectedRevision;
            CommandId = commandId ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayCancelLoadingRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long? ExpectedRevision;
        public readonly string CommandId;

        public ShooterGatewayCancelLoadingRequest(string sessionToken, string roomId, long? expectedRevision, string commandId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ExpectedRevision = expectedRevision;
            CommandId = commandId ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayGetRoomSnapshotRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;

        public ShooterGatewayGetRoomSnapshotRequest(string sessionToken, string roomId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayStateSyncSubscriptionRequest
    {
        public readonly string SessionToken;
        public readonly string BattleId;
        public readonly string RoomId;
        public readonly string EventEpoch;
        public readonly long LastEventAck;

        public ShooterGatewayStateSyncSubscriptionRequest(string sessionToken, string battleId, string roomId)
            : this(sessionToken, battleId, roomId, string.Empty, 0L)
        {
        }

        public ShooterGatewayStateSyncSubscriptionRequest(
            string sessionToken,
            string battleId,
            string roomId,
            string eventEpoch,
            long lastEventAck)
        {
            SessionToken = sessionToken ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            EventEpoch = eventEpoch ?? string.Empty;
            LastEventAck = lastEventAck;
        }
    }

    public readonly struct ShooterGatewayReliableBattleEventAckRequest
    {
        public readonly string SessionToken;
        public readonly string BattleId;
        public readonly string RoomId;
        public readonly string Epoch;
        public readonly long AckSequence;

        public ShooterGatewayReliableBattleEventAckRequest(
            string sessionToken,
            string battleId,
            string roomId,
            string epoch,
            long ackSequence)
        {
            SessionToken = sessionToken ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            Epoch = epoch ?? string.Empty;
            AckSequence = ackSequence;
        }
    }

    public readonly struct ShooterGatewayFullStateSyncRequest
    {
        public readonly string SessionToken;
        public readonly string BattleId;
        public readonly string RoomId;
        public readonly ulong WorldId;
        public readonly int ClientFrame;
        public readonly int LastAuthoritativeFrame;
        public readonly uint ClientStateHash;
        public readonly uint AuthoritativeStateHash;
        public readonly string Reason;

        public ShooterGatewayFullStateSyncRequest(
            string sessionToken,
            string battleId,
            string roomId,
            ulong worldId,
            int clientFrame,
            int lastAuthoritativeFrame,
            uint clientStateHash,
            uint authoritativeStateHash,
            string reason)
        {
            SessionToken = sessionToken ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            WorldId = worldId;
            ClientFrame = clientFrame;
            LastAuthoritativeFrame = lastAuthoritativeFrame;
            ClientStateHash = clientStateHash;
            AuthoritativeStateHash = authoritativeStateHash;
            Reason = reason ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayGuestLoginResult
    {
        public readonly bool Success;
        public readonly string SessionToken;
        public readonly string AccountId;
        public readonly string Message;

        public ShooterGatewayGuestLoginResult(bool success, string sessionToken, string accountId, string message)
        {
            Success = success;
            SessionToken = sessionToken ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayAccountLoginResult
    {
        public readonly bool Success;
        public readonly string SessionToken;
        public readonly string AccountId;
        public readonly long ExpireAtUnixMs;
        public readonly string KickedSessionToken;
        public readonly string Message;

        public ShooterGatewayAccountLoginResult(bool success, string sessionToken, string accountId, long expireAtUnixMs, string kickedSessionToken, string message)
        {
            Success = success;
            SessionToken = sessionToken ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            ExpireAtUnixMs = expireAtUnixMs;
            KickedSessionToken = kickedSessionToken ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayListRoomsResult
    {
        public readonly bool Success;
        public readonly IReadOnlyList<ShooterGatewayRoomSummary> Rooms;
        public readonly int NextOffset;
        public readonly string Message;

        public ShooterGatewayListRoomsResult(bool success, IReadOnlyList<ShooterGatewayRoomSummary>? rooms, int nextOffset, string message)
        {
            Success = success;
            Rooms = rooms ?? Array.Empty<ShooterGatewayRoomSummary>();
            NextOffset = nextOffset;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayRoomSummary
    {
        public readonly string Region;
        public readonly string ServerId;
        public readonly string RoomId;
        public readonly string RoomType;
        public readonly string Title;
        public readonly bool IsPublic;
        public readonly int MaxPlayers;
        public readonly int PlayerCount;
        public readonly string OwnerAccountId;
        public readonly long CreatedAtUnixMs;
        public readonly IReadOnlyDictionary<string, string>? Tags;

        public ShooterGatewayRoomSummary(string region, string serverId, string roomId, string roomType, string title, bool isPublic, int maxPlayers, int playerCount, string ownerAccountId, long createdAtUnixMs, IReadOnlyDictionary<string, string>? tags)
        {
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            RoomType = roomType ?? string.Empty;
            Title = title ?? string.Empty;
            IsPublic = isPublic;
            MaxPlayers = maxPlayers;
            PlayerCount = playerCount;
            OwnerAccountId = ownerAccountId ?? string.Empty;
            CreatedAtUnixMs = createdAtUnixMs;
            Tags = tags;
        }

        public bool HasOpenSlot => MaxPlayers <= 0 || PlayerCount < MaxPlayers;
        public string DisplayName => string.IsNullOrWhiteSpace(Title) ? RoomId : $"{Title} ({RoomId})";
    }

    public readonly struct ShooterGatewayCreateRoomResult
    {
        public readonly bool Success;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly string Message;

        public ShooterGatewayCreateRoomResult(bool success, string roomId, ulong numericRoomId, string message)
        {
            Success = success;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Message = message ?? string.Empty;
        }
    }

    public enum ShooterGatewayRoomJoinKind
    {
        TeamLobby = 0,
        Reconnect = 1,
        LateJoin = 2
    }

    public enum ShooterGatewayRoomRestoreStatus
    {
        Restored = 0,
        NoActiveRoom = 1,
        NotMember = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        Timeout = 6,
        Failed = 100
    }

    public enum ShooterGatewayRoomRestoreErrorCode
    {
        None = 0,
        NoAccountRoomMapping = 1,
        AccountNotInRoom = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        Timeout = 6,
        InternalError = 100
    }

    public readonly struct ShooterGatewayJoinRoomResult
    {
        public readonly bool Success;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly ShooterGatewayWorldStartAnchor WorldStartAnchor;
        public readonly string Message;
        public readonly string BattleId;
        public readonly bool CanStart;
        public readonly ShooterGatewayRoomJoinKind JoinKind;
        public readonly long ServerNowTicks;
        public readonly ulong WorldId;
        public readonly uint CurrentPlayerId;

        public ShooterGatewayJoinRoomResult(bool success, string roomId, ulong numericRoomId, in ShooterGatewayWorldStartAnchor worldStartAnchor, string message, string battleId, bool canStart)
            : this(success, roomId, numericRoomId, in worldStartAnchor, message, battleId, canStart, ShooterGatewayRoomJoinKind.TeamLobby, 0L, 0ul, 0u)
        {
        }

        public ShooterGatewayJoinRoomResult(bool success, string roomId, ulong numericRoomId, in ShooterGatewayWorldStartAnchor worldStartAnchor, string message, string battleId, bool canStart, ShooterGatewayRoomJoinKind joinKind, long serverNowTicks, ulong worldId, uint currentPlayerId = 0u)
        {
            Success = success;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            WorldStartAnchor = worldStartAnchor;
            Message = message ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            CanStart = canStart;
            JoinKind = joinKind;
            ServerNowTicks = serverNowTicks;
            WorldId = worldId;
            CurrentPlayerId = currentPlayerId;
        }
    }

    public readonly struct ShooterGatewayRestoreRoomResult
    {
        public readonly bool Success;
        public readonly bool HasActiveRoom;
        public readonly bool IsInBattle;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly ShooterGatewayWorldStartAnchor WorldStartAnchor;
        public readonly string Message;
        public readonly string BattleId;
        public readonly bool CanStart;
        public readonly ShooterGatewayRoomJoinKind JoinKind;
        public readonly long ServerNowTicks;
        public readonly ulong WorldId;
        public readonly ShooterGatewayRoomRestoreStatus Status;
        public readonly ShooterGatewayRoomRestoreErrorCode ErrorCode;
        public readonly uint CurrentPlayerId;

        public ShooterGatewayRestoreRoomResult(
            bool success,
            bool hasActiveRoom,
            bool isInBattle,
            string roomId,
            ulong numericRoomId,
            in ShooterGatewayWorldStartAnchor worldStartAnchor,
            string message,
            string battleId,
            bool canStart,
            ShooterGatewayRoomJoinKind joinKind,
            long serverNowTicks,
            ulong worldId)
            : this(success, hasActiveRoom, isInBattle, roomId, numericRoomId, in worldStartAnchor, message, battleId, canStart, joinKind, serverNowTicks, worldId, ShooterGatewayRoomRestoreStatus.Restored, ShooterGatewayRoomRestoreErrorCode.None, 0u)
        {
        }

        public ShooterGatewayRestoreRoomResult(
            bool success,
            bool hasActiveRoom,
            bool isInBattle,
            string roomId,
            ulong numericRoomId,
            in ShooterGatewayWorldStartAnchor worldStartAnchor,
            string message,
            string battleId,
            bool canStart,
            ShooterGatewayRoomJoinKind joinKind,
            long serverNowTicks,
            ulong worldId,
            ShooterGatewayRoomRestoreStatus status,
            ShooterGatewayRoomRestoreErrorCode errorCode,
            uint currentPlayerId = 0u)
        {
            Success = success;
            HasActiveRoom = hasActiveRoom;
            IsInBattle = isInBattle;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            WorldStartAnchor = worldStartAnchor;
            Message = message ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            CanStart = canStart;
            JoinKind = joinKind;
            ServerNowTicks = serverNowTicks;
            WorldId = worldId;
            Status = status;
            ErrorCode = errorCode;
            CurrentPlayerId = currentPlayerId;
        }
    }

    public readonly struct ShooterGatewayRoomSnapshotResult
    {
        public readonly bool Success;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly string Message;
        public readonly string BattleId;
        public readonly bool CanStart;

        public ShooterGatewayRoomSnapshotResult(bool success, string roomId, ulong numericRoomId, string message, string battleId, bool canStart)
        {
            Success = success;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Message = message ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            CanStart = canStart;
        }
    }

    public readonly struct ShooterGatewayStartBattleResult
    {
        public readonly bool Success;
        public readonly string BattleId;
        public readonly ulong WorldId;
        public readonly bool Started;
        public readonly ShooterGatewayWorldStartAnchor WorldStartAnchor;
        public readonly long ServerNowTicks;
        public readonly string Message;

        public ShooterGatewayStartBattleResult(bool success, string battleId, ulong worldId, bool started, string message)
            : this(success, battleId, worldId, started, default, 0L, message)
        {
        }

        public ShooterGatewayStartBattleResult(bool success, string battleId, ulong worldId, bool started, in ShooterGatewayWorldStartAnchor worldStartAnchor, long serverNowTicks, string message)
        {
            Success = success;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId;
            Started = started;
            WorldStartAnchor = worldStartAnchor;
            ServerNowTicks = serverNowTicks;
            Message = message ?? string.Empty;
        }
    }

    public sealed class ShooterGatewayStagedRoomSnapshot
    {
        public ShooterGatewayStagedRoomSnapshot(
            string roomId,
            int phase,
            string phaseReason,
            long launchGeneration,
            long loadingDeadlineUnixMs,
            string launchManifestHash,
            int launchManifestVersion,
            string lastStartFailureCode,
            long roomRevision,
            long lastEventSequence,
            bool canStart,
            string battleId,
            ulong worldId,
            in ShooterGatewayWorldStartAnchor worldStartAnchor,
            string ownerAccountId = "",
            IReadOnlyList<ShooterGatewayStagedRoomPlayerSnapshot>? players = null)
        {
            RoomId = roomId ?? string.Empty;
            Phase = phase;
            PhaseReason = phaseReason ?? string.Empty;
            LaunchGeneration = launchGeneration;
            LoadingDeadlineUnixMs = loadingDeadlineUnixMs;
            LaunchManifestHash = launchManifestHash ?? string.Empty;
            LaunchManifestVersion = launchManifestVersion;
            LastStartFailureCode = lastStartFailureCode ?? string.Empty;
            RoomRevision = roomRevision;
            LastEventSequence = lastEventSequence;
            CanStart = canStart;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId;
            WorldStartAnchor = worldStartAnchor;
            OwnerAccountId = ownerAccountId ?? string.Empty;
            Players = players ?? Array.Empty<ShooterGatewayStagedRoomPlayerSnapshot>();
        }

        public string RoomId { get; }
        public string OwnerAccountId { get; internal set; } = string.Empty;
        public IReadOnlyList<ShooterGatewayStagedRoomPlayerSnapshot> Players { get; internal set; } = Array.Empty<ShooterGatewayStagedRoomPlayerSnapshot>();
        public int Phase { get; }
        public string PhaseReason { get; }
        public long LaunchGeneration { get; }
        public long LoadingDeadlineUnixMs { get; }
        public string LaunchManifestHash { get; }
        public int LaunchManifestVersion { get; }
        public string LastStartFailureCode { get; }
        public long RoomRevision { get; }
        public long LastEventSequence { get; }
        public bool CanStart { get; }
        public string BattleId { get; }
        public ulong WorldId { get; }
        public ShooterGatewayWorldStartAnchor WorldStartAnchor { get; }
    }

    public sealed class ShooterGatewayStagedRoomPlayerSnapshot
    {
        public ShooterGatewayStagedRoomPlayerSnapshot(
            string accountId,
            uint playerId,
            bool isOnline,
            bool lobbyReady,
            bool assetsLoaded,
            int loadingProgress)
        {
            AccountId = accountId ?? string.Empty;
            PlayerId = playerId;
            IsOnline = isOnline;
            LobbyReady = lobbyReady;
            AssetsLoaded = assetsLoaded;
            LoadingProgress = Math.Max(0, Math.Min(100, loadingProgress));
        }

        public string AccountId { get; }
        public uint PlayerId { get; }
        public bool IsOnline { get; }
        public bool LobbyReady { get; }
        public bool AssetsLoaded { get; }
        public int LoadingProgress { get; }
    }

    public readonly struct ShooterGatewayRoomOperationResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string Message;
        public readonly long RoomRevision;
        public readonly ShooterGatewayStagedRoomSnapshot? Snapshot;

        public ShooterGatewayRoomOperationResult(bool success, bool applied, int errorCode, string message, long roomRevision, ShooterGatewayStagedRoomSnapshot? snapshot)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            RoomRevision = roomRevision;
            Snapshot = snapshot;
        }
    }

    public readonly struct ShooterGatewayGetRoomSnapshotResult
    {
        public readonly bool Success;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly ShooterGatewayStagedRoomSnapshot Snapshot;
        public readonly string Message;
        public readonly long ServerNowTicks;

        public ShooterGatewayGetRoomSnapshotResult(bool success, string roomId, ulong numericRoomId, ShooterGatewayStagedRoomSnapshot snapshot, string message, long serverNowTicks)
        {
            Success = success;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
            ServerNowTicks = serverNowTicks;
        }
    }

    public readonly struct ShooterGatewayStateSyncSubscriptionResult
    {
        public readonly bool Success;
        public readonly string Message;

        public ShooterGatewayStateSyncSubscriptionResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayReliableBattleEventAckResult
    {
        public readonly bool Success;
        public readonly long AcceptedAckSequence;
        public readonly string Message;

        public ShooterGatewayReliableBattleEventAckResult(bool success, long acceptedAckSequence, string message)
        {
            Success = success;
            AcceptedAckSequence = acceptedAckSequence;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct ShooterGatewayFullStateSyncRequestResult
    {
        public static readonly ShooterGatewayFullStateSyncRequestResult NotRequested = new ShooterGatewayFullStateSyncRequestResult(false, false, "not requested", 0L);

        public readonly bool Success;
        public readonly bool Accepted;
        public readonly string Message;
        public readonly long ServerTicks;

        public ShooterGatewayFullStateSyncRequestResult(bool success, bool accepted, string message, long serverTicks)
        {
            Success = success;
            Accepted = accepted;
            Message = message ?? string.Empty;
            ServerTicks = serverTicks;
        }
    }

    public readonly struct ShooterGatewayWorldStartAnchor
    {
        public readonly long StartServerTicks;
        public readonly long ServerTickFrequency;
        public readonly int StartFrame;
        public readonly double FixedDeltaSeconds;

        public ShooterGatewayWorldStartAnchor(long startServerTicks, long serverTickFrequency, int startFrame, double fixedDeltaSeconds)
        {
            StartServerTicks = startServerTicks;
            ServerTickFrequency = serverTickFrequency;
            StartFrame = startFrame;
            FixedDeltaSeconds = fixedDeltaSeconds;
        }

        public bool IsValid => StartServerTicks > 0L && ServerTickFrequency > 0L && FixedDeltaSeconds > 0d;

        public WorldStartFrameAnchor ToFrameStartAnchor()
        {
            return new WorldStartFrameAnchor(StartServerTicks, ServerTickFrequency, StartFrame, FixedDeltaSeconds);
        }

        public int CalculateTargetFrame(long serverNowTicks)
        {
            return WorldStartFrameCatchUpCalculator.Calculate(ToFrameStartAnchor(), serverNowTicks).TargetFrame;
        }
    }
}
