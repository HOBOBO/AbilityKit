using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.View;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

internal sealed class ScriptedShooterRoomClient : IShooterRoomGatewayRoomClient
{
    public readonly List<string> Calls = new List<string>();

    public ShooterGatewayCreateRoomRequest LastCreateRequest { get; private set; }

    public ShooterGatewayJoinRoomRequest LastJoinRequest { get; private set; }

    public ShooterGatewayReadyRequest LastReadyRequest { get; private set; }

    public ShooterGatewayStartBattleRequest LastStartBattleRequest { get; private set; }

    public ShooterGatewayReportAssetsLoadedRequest LastReportAssetsLoadedRequest { get; private set; }

    public ShooterGatewayReportLoadingProgressRequest LastReportLoadingProgressRequest { get; private set; }

    public ShooterGatewayStateSyncSubscriptionRequest LastSubscribeRequest { get; private set; }

    public ShooterGatewayFullStateSyncRequest LastFullStateSyncRequest { get; private set; }

    public ShooterGatewayReliableBattleEventAckRequest LastReliableBattleEventAckRequest { get; private set; }

    public ShooterGatewayReliableBattleEventAckResult ReliableBattleEventAckResult { get; set; }
        = new ShooterGatewayReliableBattleEventAckResult(true, 0L, "acknowledged");

    public ShooterGatewayRoomJoinKind JoinKind { get; set; } = ShooterGatewayRoomJoinKind.TeamLobby;

    public string JoinBattleId { get; set; } = "battle-prestart";

    public ulong JoinWorldId { get; set; } = 0ul;

    public long JoinServerNowTicks { get; set; } = 223456L;
 
    public uint JoinCurrentPlayerId { get; set; } = 121u;
 
    public bool JoinCanStart { get; set; } = true;

    public bool ReadyCanStart { get; set; } = true;

    public int PreAssetsSnapshotPhase { get; set; } = 1;

    public bool PreAssetsSnapshotCanStart { get; set; } = true;

    public bool SnapshotLocalIsOwner { get; set; }

    public bool RestoreIsInBattle { get; set; }

    public bool RestoreSuccess { get; set; } = true;

    public bool RestoreHasActiveRoom { get; set; } = true;

    public Exception? RestoreException { get; set; }

    public ShooterGatewayRoomRestoreStatus RestoreStatus { get; set; } = ShooterGatewayRoomRestoreStatus.Restored;

    public ShooterGatewayRoomRestoreErrorCode RestoreErrorCode { get; set; } = ShooterGatewayRoomRestoreErrorCode.None;

    public int RestoreSnapshotPhase { get; set; } = 3;

    public string RestoreSnapshotBattleId { get; set; } = string.Empty;

    public ulong RestoreSnapshotWorldId { get; set; }

    private bool _returnRestoreSnapshot;
    private bool _assetsReported;

    public ShooterGatewayWorldStartAnchor JoinWorldStartAnchor { get; set; } = new ShooterGatewayWorldStartAnchor(123456L, 10000000L, 12, 1d / 30d);

    public Task<ShooterGatewayGuestLoginResult> GuestLoginAsync(ShooterGatewayGuestLoginRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Calls.Add("guest-login:" + request.GuestId);
        return Task.FromResult(new ShooterGatewayGuestLoginResult(true, "session-token", "account-1", "guest-login-ok"));
    }

    public Task<ShooterGatewayAccountLoginResult> AccountLoginAsync(ShooterGatewayAccountLoginRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Calls.Add("account-login:" + request.AccountId);
        return Task.FromResult(new ShooterGatewayAccountLoginResult(true, "session-token", request.AccountId, 3600000L, string.Empty, "account-login-ok"));
    }

    public Task<ShooterGatewayListRoomsResult> ListRoomsAsync(ShooterGatewayListRoomsRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Calls.Add("list-rooms:" + request.Region + ":" + request.ServerId + ":" + request.RoomType);
        return Task.FromResult(new ShooterGatewayListRoomsResult(true, Array.Empty<ShooterGatewayRoomSummary>(), 0, "list-rooms-ok"));
    }

    public Task<ShooterGatewayCreateRoomResult> CreateRoomAsync(ShooterGatewayCreateRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        Calls.Add("create:" + request.RoomType);
        return Task.FromResult(new ShooterGatewayCreateRoomResult(true, "room-1", 1001ul, "created"));
    }

    public Task<ShooterGatewayJoinRoomResult> JoinRoomAsync(ShooterGatewayJoinRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastJoinRequest = request;
        Calls.Add("join:" + request.RoomId);
        var anchor = JoinWorldStartAnchor;
        return Task.FromResult(new ShooterGatewayJoinRoomResult(
            true,
            request.RoomId,
            1001ul,
            in anchor,
            "joined",
            JoinBattleId,
            JoinCanStart,
            JoinKind,
            JoinServerNowTicks,
            JoinWorldId,
            JoinCurrentPlayerId));
    }

    public Task<ShooterGatewayRoomSnapshotResult> SetReadyAsync(ShooterGatewayReadyRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastReadyRequest = request;
        Calls.Add("ready:" + request.RoomId + ":" + request.Ready);
        return Task.FromResult(new ShooterGatewayRoomSnapshotResult(true, request.RoomId, 1001ul, "ready", "battle-ready", ReadyCanStart));
    }

    public Task<ShooterGatewayStartBattleResult> StartBattleAsync(ShooterGatewayStartBattleRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastStartBattleRequest = request;
        Calls.Add("start:" + request.RoomId + ":" + request.GameplayId);
        var anchor = new ShooterGatewayWorldStartAnchor(200000L, 10000000L, 30, 1d / 30d);
        return Task.FromResult(new ShooterGatewayStartBattleResult(true, "battle-1", 9001ul, started: true, in anchor, 1200000L, "started"));
    }

    public Task<ShooterGatewayRoomOperationResult> BeginLoadingAsync(ShooterGatewayBeginLoadingRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Calls.Add("begin-loading:" + request.RoomId);
        return Task.FromResult(new ShooterGatewayRoomOperationResult(
            true,
            true,
            0,
            "loading",
            3L,
            CreateStagedSnapshot(request.RoomId, phase: 1, battleId: string.Empty, worldId: 0ul)));
    }

    public Task<ShooterGatewayRoomOperationResult> ReportAssetsLoadedAsync(ShooterGatewayReportAssetsLoadedRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastReportAssetsLoadedRequest = request;
        Calls.Add("assets-loaded:" + request.RoomId);
        _assetsReported = true;
        return Task.FromResult(new ShooterGatewayRoomOperationResult(
            true,
            true,
            0,
            "loaded",
            4L,
            CreateStagedSnapshot(request.RoomId, phase: 3, battleId: "battle-1", worldId: 9001ul)));
    }

    public Task<ShooterGatewayRoomOperationResult> ReportLoadingProgressAsync(ShooterGatewayReportLoadingProgressRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastReportLoadingProgressRequest = request;
        Calls.Add("loading-progress:" + request.RoomId + ":" + request.Progress);
        return Task.FromResult(new ShooterGatewayRoomOperationResult(
            true,
            true,
            0,
            "progress",
            3L,
            CreateStagedSnapshot(request.RoomId, phase: 1, battleId: string.Empty, worldId: 0ul)));
    }

    public Task<ShooterGatewayRoomOperationResult> CancelLoadingAsync(ShooterGatewayCancelLoadingRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Calls.Add("cancel-loading:" + request.RoomId);
        _assetsReported = false;
        return Task.FromResult(new ShooterGatewayRoomOperationResult(
            true,
            true,
            0,
            "cancelled",
            (request.ExpectedRevision ?? 0L) + 1L,
            CreateStagedSnapshot(request.RoomId, phase: 0, battleId: string.Empty, worldId: 0ul)));
    }

    public Task<ShooterGatewayRoomOperationResult> LeaveRoomAsync(ShooterGatewayLeaveRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Calls.Add("leave:" + request.RoomId);
        return Task.FromResult(new ShooterGatewayRoomOperationResult(
            true,
            true,
            0,
            "left",
            (request.ExpectedRevision ?? 0L) + 1L,
            null));
    }

    public Task<ShooterGatewayGetRoomSnapshotResult> GetSnapshotAsync(ShooterGatewayGetRoomSnapshotRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Calls.Add("get-snapshot:" + request.RoomId);
        var isRestoreSnapshot = _returnRestoreSnapshot;
        _returnRestoreSnapshot = false;
        var phase = isRestoreSnapshot
            ? RestoreSnapshotPhase
            : (_assetsReported ? 3 : PreAssetsSnapshotPhase);
        var battleId = isRestoreSnapshot
            ? (string.IsNullOrWhiteSpace(RestoreSnapshotBattleId) ? JoinBattleId : RestoreSnapshotBattleId)
            : (phase == 3 ? "battle-1" : string.Empty);
        var worldId = isRestoreSnapshot
            ? (RestoreSnapshotWorldId == 0ul ? JoinWorldId : RestoreSnapshotWorldId)
            : (phase == 3 ? 9001ul : 0ul);
        return Task.FromResult(new ShooterGatewayGetRoomSnapshotResult(
            true,
            request.RoomId,
            1001ul,
            CreateStagedSnapshot(request.RoomId, phase, battleId, worldId),
            "running",
            1200000L));
    }

    public Task<ShooterGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(ShooterGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastSubscribeRequest = request;
        Calls.Add("subscribe:" + request.RoomId + ":" + request.BattleId);
        return Task.FromResult(new ShooterGatewayStateSyncSubscriptionResult(true, "subscribed"));
    }

    public Task<ShooterGatewayReliableBattleEventAckResult> AcknowledgeReliableBattleEventsAsync(ShooterGatewayReliableBattleEventAckRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastReliableBattleEventAckRequest = request;
        Calls.Add("ack-reliable-events:" + request.RoomId + ":" + request.BattleId + ":" + request.AckSequence);
        var result = ReliableBattleEventAckResult;
        if (result.Success && result.AcceptedAckSequence == 0L)
        {
            result = new ShooterGatewayReliableBattleEventAckResult(true, request.AckSequence, result.Message);
        }

        return Task.FromResult(result);
    }

    public Task<ShooterGatewayFullStateSyncRequestResult> RequestFullStateSyncAsync(ShooterGatewayFullStateSyncRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        LastFullStateSyncRequest = request;
        Calls.Add("request-full-state:" + request.RoomId + ":" + request.BattleId + ":" + request.Reason);
        return Task.FromResult(new ShooterGatewayFullStateSyncRequestResult(true, true, "accepted", 123456789L));
    }

    public Task<ShooterGatewayRestoreRoomResult> RestoreRoomAsync(ShooterGatewayRestoreRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Calls.Add("restore:" + request.Region + ":" + request.ServerId);
        if (RestoreException != null) throw RestoreException;
        _returnRestoreSnapshot = true;
        _assetsReported = RestoreSnapshotPhase >= 2;
        var anchor = JoinWorldStartAnchor;
        return Task.FromResult(new ShooterGatewayRestoreRoomResult(
            RestoreSuccess,
            RestoreHasActiveRoom,
            RestoreIsInBattle,
            RestoreHasActiveRoom ? "room-1" : string.Empty,
            RestoreHasActiveRoom ? 1001ul : 0UL,
            in anchor,
            "restored",
            JoinBattleId,
            JoinCanStart,
            JoinKind,
            JoinServerNowTicks,
            JoinWorldId,
            RestoreStatus,
            RestoreErrorCode,
            JoinCurrentPlayerId));
    }

    private ShooterGatewayStagedRoomSnapshot CreateStagedSnapshot(string roomId, int phase, string battleId, ulong worldId)
    {
        var anchor = new ShooterGatewayWorldStartAnchor(200000L, 10000000L, 30, 1d / 30d);
        var localAccountId = SnapshotLocalIsOwner ? "account-owner" : "account-member";
        var players = new[]
        {
            new ShooterGatewayStagedRoomPlayerSnapshot(
                localAccountId,
                JoinCurrentPlayerId,
                isOnline: true,
                lobbyReady: true,
                assetsLoaded: _assetsReported,
                loadingProgress: _assetsReported ? 100 : 0)
        };
        return new ShooterGatewayStagedRoomSnapshot(
            roomId,
            phase,
            string.Empty,
            7L,
            0L,
            "manifest-shooter-v3",
            3,
            string.Empty,
            4L,
            4L,
            PreAssetsSnapshotCanStart,
            battleId,
            worldId,
            in anchor,
            "account-owner",
            players);
    }
}
