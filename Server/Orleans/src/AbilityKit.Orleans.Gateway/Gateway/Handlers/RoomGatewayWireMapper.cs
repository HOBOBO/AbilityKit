using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

internal static class RoomGatewayWireMapper
{
    public static async Task<string?> ValidateAccountAsync(IClusterClient client, string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var session = client.GetGrain<ISessionGrain>("global");
        var result = await session.ValidateAsync(new ValidateSessionRequest(sessionToken));
        return result.IsValid && !string.IsNullOrWhiteSpace(result.AccountId) ? result.AccountId : null;
    }

    public static WireJoinRoomRes ToJoinRoomRes(RoomSnapshot snapshot, string message = "")
    {
        return ToJoinRoomRes(new JoinRoomResponse(snapshot, RoomJoinKind.TeamLobby, DateTime.UtcNow.Ticks), null, message);
    }

    public static WireJoinRoomRes ToJoinRoomRes(JoinRoomResponse response, string? accountId = null, string message = "")
    {
        var snapshot = response.Snapshot;
        var roomId = snapshot.Summary?.RoomId ?? string.Empty;
        return new WireJoinRoomRes
        {
            Success = true,
            RoomId = roomId,
            NumericRoomId = RoomGatewayIds.CreateNumericRoomId(roomId),
            Snapshot = ToWireSnapshot(snapshot),
            WorldStartAnchor = ToWireAnchor(snapshot.WorldStartAnchor),
            Message = message ?? string.Empty,
            JoinKind = ToWireJoinKind(response.JoinKind),
            ServerNowTicks = response.ServerNowTicks,
            CurrentPlayerId = ResolvePlayerId(snapshot, accountId)
        };
    }

    public static WireRestoreRoomRes ToRestoreRoomRes(RestoreRoomResponse response, string? accountId = null, string message = "")
    {
        var snapshot = response.Snapshot;
        var roomId = snapshot.Summary?.RoomId ?? string.Empty;
        return new WireRestoreRoomRes
        {
            Success = true,
            HasActiveRoom = response.HasActiveRoom,
            IsInBattle = response.IsInBattle,
            RoomId = roomId,
            NumericRoomId = RoomGatewayIds.CreateNumericRoomId(roomId),
            Snapshot = ToWireSnapshot(snapshot),
            WorldStartAnchor = ToWireAnchor(snapshot.WorldStartAnchor),
            Message = string.IsNullOrEmpty(message) ? response.Message ?? string.Empty : message,
            JoinKind = ToWireJoinKind(response.JoinKind),
            ServerNowTicks = response.ServerNowTicks,
            Status = ToWireRestoreStatus(response.Status),
            ErrorCode = ToWireRestoreErrorCode(response.ErrorCode),
            CurrentPlayerId = ResolvePlayerId(snapshot, accountId)
        };
    }

    public static WireRestoreRoomRes ToEmptyRestoreRoomRes(string message)
    {
        return ToEmptyRestoreRoomRes(RoomRestoreStatus.NoActiveRoom, RoomRestoreErrorCode.NoAccountRoomMapping, message);
    }

    public static WireRestoreRoomRes ToEmptyRestoreRoomRes(RoomRestoreStatus status, RoomRestoreErrorCode errorCode, string message)
    {
        return new WireRestoreRoomRes
        {
            Success = true,
            HasActiveRoom = false,
            IsInBattle = false,
            RoomId = string.Empty,
            NumericRoomId = 0ul,
            Message = message ?? string.Empty,
            JoinKind = WireRoomJoinKind.TeamLobby,
            ServerNowTicks = DateTime.UtcNow.Ticks,
            Status = ToWireRestoreStatus(status),
            ErrorCode = ToWireRestoreErrorCode(errorCode)
        };
    }

    public static WireRoomSnapshotRes ToSnapshotRes(RoomSnapshot snapshot, string message = "")
    {
        var roomId = snapshot.Summary?.RoomId ?? string.Empty;
        return new WireRoomSnapshotRes
        {
            Success = true,
            RoomId = roomId,
            NumericRoomId = RoomGatewayIds.CreateNumericRoomId(roomId),
            Snapshot = ToWireSnapshot(snapshot),
            Message = message ?? string.Empty,
            ServerNowTicks = DateTime.UtcNow.Ticks,
            Applied = true,
            ErrorCode = (int)RoomOperationErrorCode.None
        };
    }

    public static WireRoomSnapshotRes ToSnapshotRes(
        RoomSnapshot snapshot,
        RoomOperationResult result)
    {
        var roomId = snapshot.Summary?.RoomId ?? string.Empty;
        return new WireRoomSnapshotRes
        {
            Success = result.Success,
            RoomId = roomId,
            NumericRoomId = RoomGatewayIds.CreateNumericRoomId(roomId),
            Snapshot = ToWireSnapshot(snapshot),
            Message = result.Message ?? string.Empty,
            ServerNowTicks = DateTime.UtcNow.Ticks,
            Applied = result.Applied,
            ErrorCode = (int)result.ErrorCode
        };
    }

    public static WireListRoomsRes ToListRoomsRes(ListRoomsResponse response, string message = "")
    {
        var rooms = response.Rooms == null ? new List<WireRoomSummary>() : new List<WireRoomSummary>(response.Rooms.Count);
        if (response.Rooms != null)
        {
            foreach (var room in response.Rooms)
            {
                rooms.Add(ToWireSummary(room));
            }
        }

        return new WireListRoomsRes
        {
            Success = true,
            Rooms = rooms,
            NextOffset = response.NextOffset,
            Message = message ?? string.Empty
        };
    }

    public static RoomGameplayCommandRequest ToGameplayCommand(string accountId, WireRoomPickHeroReq req)
    {
        return RoomGameplayCommandRequest.CreateMobaLoadout(
            accountId,
            req.HeroId,
            req.TeamId,
            req.SpawnPointId,
            req.Level,
            req.AttributeTemplateId,
            req.BasicAttackSkillId,
            req.SkillIds);
    }

    public static RoomGameplayCommandRequest ToGameplayCommand(
        string accountId,
        int heroId,
        int teamId,
        int spawnPointId,
        int level,
        int attributeTemplateId,
        int basicAttackSkillId,
        IReadOnlyList<int>? skillIds)
    {
        return RoomGameplayCommandRequest.CreateMobaLoadout(
            accountId,
            heroId,
            teamId,
            spawnPointId,
            level,
            attributeTemplateId,
            basicAttackSkillId,
            skillIds);
    }

    public static WireRoomSnapshot ToWireSnapshot(RoomSnapshot snapshot)
    {
        return new WireRoomSnapshot
        {
            Summary = ToWireSummary(snapshot.Summary),
            Members = snapshot.Members == null ? null : new List<string>(snapshot.Members),
            Players = ToWirePlayers(snapshot.Players),
            CanStart = snapshot.CanStart,
            BattleId = snapshot.BattleId ?? string.Empty,
            WorldId = snapshot.WorldId,
            // 阶段 4 append-only 字段
            WorldStartAnchor = ToWireAnchor(snapshot.WorldStartAnchor),
            SchemaVersion = snapshot.SchemaVersion,
            RoomRevision = snapshot.RoomRevision,
            LastEventSequence = snapshot.LastEventSequence,
            Phase = (int)snapshot.Phase,
            PhaseReason = snapshot.PhaseReason ?? string.Empty,
            LaunchGeneration = snapshot.LaunchGeneration,
            LoadingDeadlineUnixMs = snapshot.LoadingDeadlineUnixMs,
            LaunchManifestHash = snapshot.LaunchManifestHash ?? string.Empty,
            LaunchManifestVersion = snapshot.LaunchManifestVersion,
            LastStartFailureCode = snapshot.LastStartFailureCode ?? string.Empty
        };
    }

    /// <summary>
    /// 将 RoomOperationResult + 操作后快照映射为 wire 响应。
    /// </summary>
    public static WireRoomOperationRes ToRoomOperationRes(RoomOperationResult result, RoomSnapshot? snapshot)
    {
        return new WireRoomOperationRes
        {
            Success = result.Success,
            Applied = result.Applied,
            ErrorCode = (int)result.ErrorCode,
            Message = result.Message ?? string.Empty,
            RoomRevision = result.RoomRevision,
            Snapshot = snapshot == null ? default : ToWireSnapshot(snapshot)
        };
    }

    /// <summary>
    /// wire BeginLoading 请求 -> Grain BeginLoadingRequest。
    /// </summary>
    public static BeginLoadingRequest ToBeginLoadingReq(string accountId, WireBeginLoadingReq wire)
    {
        return new BeginLoadingRequest(
            accountId,
            wire.ExpectedRevision,
            string.IsNullOrWhiteSpace(wire.CommandId) ? null : wire.CommandId);
    }

    /// <summary>
    /// wire ReportAssetsLoaded 请求 -> Grain ReportAssetsLoadedRequest。
    /// </summary>
    public static ReportAssetsLoadedRequest ToReportAssetsLoadedReq(string accountId, WireReportAssetsLoadedReq wire)
    {
        return new ReportAssetsLoadedRequest(
            accountId,
            wire.LaunchGeneration,
            wire.ManifestVersion,
            string.IsNullOrWhiteSpace(wire.ManifestHash) ? null : wire.ManifestHash,
            string.IsNullOrWhiteSpace(wire.CommandId) ? null : wire.CommandId);
    }

    public static ReportLoadingProgressRequest ToReportLoadingProgressReq(string accountId, WireReportLoadingProgressReq wire)
    {
        return new ReportLoadingProgressRequest(
            accountId,
            wire.LaunchGeneration,
            wire.ManifestVersion,
            string.IsNullOrWhiteSpace(wire.ManifestHash) ? null : wire.ManifestHash,
            wire.Progress);
    }

    /// <summary>
    /// wire CancelLoading 请求 -> Grain CancelLoadingRequest。
    /// </summary>
    public static CancelLoadingRequest ToCancelLoadingReq(string accountId, WireCancelLoadingReq wire)
    {
        return new CancelLoadingRequest(
            accountId,
            wire.ExpectedRevision,
            string.IsNullOrWhiteSpace(wire.CommandId) ? null : wire.CommandId);
    }

    public static WireRoomJoinKind ToWireJoinKind(RoomJoinKind joinKind)
    {
        return joinKind switch
        {
            RoomJoinKind.Reconnect => WireRoomJoinKind.Reconnect,
            RoomJoinKind.LateJoin => WireRoomJoinKind.LateJoin,
            _ => WireRoomJoinKind.TeamLobby
        };
    }

    public static WireRoomRestoreStatus ToWireRestoreStatus(RoomRestoreStatus status)
    {
        return status switch
        {
            RoomRestoreStatus.NoActiveRoom => WireRoomRestoreStatus.NoActiveRoom,
            RoomRestoreStatus.NotMember => WireRoomRestoreStatus.NotMember,
            RoomRestoreStatus.RoomClosed => WireRoomRestoreStatus.RoomClosed,
            RoomRestoreStatus.RoomExpired => WireRoomRestoreStatus.RoomExpired,
            RoomRestoreStatus.InvalidSession => WireRoomRestoreStatus.InvalidSession,
            RoomRestoreStatus.Failed => WireRoomRestoreStatus.Failed,
            _ => WireRoomRestoreStatus.Restored
        };
    }

    public static WireRoomRestoreErrorCode ToWireRestoreErrorCode(RoomRestoreErrorCode errorCode)
    {
        return errorCode switch
        {
            RoomRestoreErrorCode.NoAccountRoomMapping => WireRoomRestoreErrorCode.NoAccountRoomMapping,
            RoomRestoreErrorCode.AccountNotInRoom => WireRoomRestoreErrorCode.AccountNotInRoom,
            RoomRestoreErrorCode.RoomClosed => WireRoomRestoreErrorCode.RoomClosed,
            RoomRestoreErrorCode.RoomExpired => WireRoomRestoreErrorCode.RoomExpired,
            RoomRestoreErrorCode.InvalidSession => WireRoomRestoreErrorCode.InvalidSession,
            RoomRestoreErrorCode.InternalError => WireRoomRestoreErrorCode.InternalError,
            _ => WireRoomRestoreErrorCode.None
        };
    }

    public static WireWorldStartAnchor ToWireAnchor(WorldStartAnchor? anchor)
    {
        if (anchor is null)
        {
            return default;
        }

        return new WireWorldStartAnchor
        {
            StartServerTicks = anchor.StartServerTicks,
            ServerTickFrequency = anchor.ServerTickFrequency,
            StartFrame = anchor.StartFrame,
            FixedDeltaSeconds = anchor.FixedDeltaSeconds
        };
    }

    public static uint ResolvePlayerId(RoomSnapshot? snapshot, string? accountId)
    {
        if (snapshot?.Players == null || string.IsNullOrWhiteSpace(accountId))
        {
            return 0u;
        }

        foreach (var player in snapshot.Players)
        {
            if (string.Equals(player.AccountId, accountId, StringComparison.Ordinal))
            {
                return player.PlayerId;
            }
        }

        return 0u;
    }

    public static WireRoomSummary ToWireSummary(RoomSummary? summary)
    {
        if (summary == null)
        {
            return default;
        }

        return new WireRoomSummary
        {
            Region = summary.Region ?? string.Empty,
            ServerId = summary.ServerId ?? string.Empty,
            RoomId = summary.RoomId ?? string.Empty,
            RoomType = summary.RoomType ?? string.Empty,
            Title = summary.Title ?? string.Empty,
            IsPublic = summary.IsPublic,
            MaxPlayers = summary.MaxPlayers,
            PlayerCount = summary.PlayerCount,
            OwnerAccountId = summary.OwnerAccountId ?? string.Empty,
            CreatedAtUnixMs = summary.CreatedAtUnixMs,
            Tags = summary.Tags == null ? null : new Dictionary<string, string>(summary.Tags)
        };
    }

    private static List<WireRoomPlayerSnapshot>? ToWirePlayers(List<RoomPlayerSnapshot>? players)
    {
        if (players == null || players.Count == 0)
        {
            return players == null ? null : new List<WireRoomPlayerSnapshot>();
        }

        var result = new List<WireRoomPlayerSnapshot>(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            result.Add(new WireRoomPlayerSnapshot
            {
                AccountId = player.AccountId ?? string.Empty,
                TeamId = player.TeamId,
                Ready = player.Ready,
                HeroId = player.HeroId,
                SpawnPointId = player.SpawnPointId,
                Level = player.Level,
                AttributeTemplateId = player.AttributeTemplateId,
                BasicAttackSkillId = player.BasicAttackSkillId,
                SkillIds = player.SkillIds == null ? null : new List<int>(player.SkillIds),
                PlayerId = player.PlayerId,
                // 阶段 4 append-only 字段
                LobbyReady = player.LobbyReady,
                AssetsLoaded = player.AssetsLoaded,
                LoadingProgress = player.LoadingProgress,
                IsOnline = player.IsOnline,
                JoinOrdinal = player.JoinOrdinal,
                LoadedManifestVersion = player.LoadedManifestVersion,
                LoadedManifestHash = player.LoadedManifestHash ?? string.Empty,
                LastSeenTicks = player.LastSeenTicks,
                OfflineSinceTicks = player.OfflineSinceTicks
            });
        }

        return result;
    }
}
