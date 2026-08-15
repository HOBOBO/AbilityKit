using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Battle;
using Orleans.Serialization;

namespace AbilityKit.Orleans.Contracts.Rooms;

[GenerateSerializer]
public sealed record RoomSummary(
    [property: Id(0)] string Region,
    [property: Id(1)] string ServerId,
    [property: Id(2)] string RoomId,
    [property: Id(3)] string RoomType,
    [property: Id(4)] string Title,
    [property: Id(5)] bool IsPublic,
    [property: Id(6)] int MaxPlayers,
    [property: Id(7)] int PlayerCount,
    [property: Id(8)] string OwnerAccountId,
    [property: Id(9)] long CreatedAtUnixMs,
    [property: Id(10)] Dictionary<string, string>? Tags);

[GenerateSerializer]
public enum RoomPhase
{
    Lobby = 0,
    Loading = 1,
    Starting = 2,
    InBattle = 3,
    Closing = 4,
    Closed = 5,
    Expired = 6
}

[GenerateSerializer]
public enum RoomOperationErrorCode
{
    None = 0,
    NotMember = 1,
    NotOwner = 2,
    InvalidPhase = 3,
    RevisionConflict = 4,
    CommandIdConflict = 5,
    LaunchGenerationMismatch = 6,
    RoomClosed = 7,
    HeroConflict = 8,
    InvalidGameplayCommand = 9,
    InvalidOperation = 100
}

[GenerateSerializer]
public sealed record RoomOperationResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] bool Applied,
    [property: Id(2)] RoomOperationErrorCode ErrorCode,
    [property: Id(3)] string Message,
    [property: Id(4)] long RoomRevision)
{
    public static RoomOperationResult AppliedAt(long revision) =>
        new(true, true, RoomOperationErrorCode.None, string.Empty, revision);

    public static RoomOperationResult NoChange(long revision, string message = "") =>
        new(true, false, RoomOperationErrorCode.None, message, revision);

    public static RoomOperationResult Rejected(RoomOperationErrorCode errorCode, string message, long revision) =>
        new(false, false, errorCode, message, revision);
}

[GenerateSerializer]
public sealed record RoomMemberState(
    [property: Id(0)] bool IsOnline,
    [property: Id(1)] long LastSeenTicks,
    [property: Id(2)] long OfflineSinceTicks,
    [property: Id(3)] bool IsBot = false,
    [property: Id(4)] long JoinOrdinal = 0,
    [property: Id(5)] bool LobbyReady = false,
    [property: Id(6)] bool AssetsLoaded = false,
    [property: Id(7)] int LoadedManifestVersion = 0,
    [property: Id(8)] string? LoadedManifestHash = null,
    [property: Id(9)] int LoadingProgress = 0);

[GenerateSerializer]
public sealed record CreateRoomRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] string RoomType,
    [property: Id(4)] string Title,
    [property: Id(5)] bool IsPublic,
    [property: Id(6)] int MaxPlayers,
    [property: Id(7)] Dictionary<string, string>? Tags);

[GenerateSerializer]
public sealed record CreateRoomResponse(
    [property: Id(0)] string RoomId);

[GenerateSerializer]
public sealed record JoinRoomRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] string RoomId);

[GenerateSerializer]
public enum RoomJoinKind
{
    TeamLobby = 0,
    Reconnect = 1,
    LateJoin = 2
}

[GenerateSerializer]
public sealed record LeaveRoomRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] string RoomId);

[GenerateSerializer]
public sealed record RoomPlayerSnapshot(
    [property: Id(0)] string AccountId,
    [property: Id(1)] int TeamId,
    [property: Id(2)] bool Ready,
    [property: Id(3)] int HeroId,
    [property: Id(4)] int SpawnPointId,
    [property: Id(5)] int Level,
    [property: Id(6)] int AttributeTemplateId,
    [property: Id(7)] int BasicAttackSkillId,
    [property: Id(8)] List<int>? SkillIds,
    [property: Id(9)] uint PlayerId,
    [property: Id(10)] bool LobbyReady = false,
    [property: Id(11)] bool AssetsLoaded = false,
    [property: Id(12)] bool IsOnline = false,
    [property: Id(13)] long JoinOrdinal = 0,
    [property: Id(14)] int LoadedManifestVersion = 0,
    [property: Id(15)] string? LoadedManifestHash = null,
    [property: Id(16)] long LastSeenTicks = 0,
    [property: Id(17)] long OfflineSinceTicks = 0,
    [property: Id(18)] int LoadingProgress = 0);

[GenerateSerializer]
public sealed record RoomSnapshot(
    [property: Id(0)] RoomSummary Summary,
    [property: Id(1)] List<string> Members,
    [property: Id(2)] List<RoomPlayerSnapshot> Players,
    [property: Id(3)] bool CanStart,
    [property: Id(4)] string? BattleId,
    [property: Id(5)] WorldStartAnchor? WorldStartAnchor,
    [property: Id(6)] ulong WorldId,
    [property: Id(7)] Dictionary<string, RoomMemberState>? MemberStates,
    [property: Id(8)] int SchemaVersion = 1,
    [property: Id(9)] long RoomRevision = 0,
    [property: Id(10)] long LastEventSequence = 0,
    [property: Id(11)] RoomPhase Phase = RoomPhase.Lobby,
    [property: Id(12)] string PhaseReason = "",
    [property: Id(13)] long LaunchGeneration = 0,
    [property: Id(14)] long LoadingDeadlineUnixMs = 0,
    [property: Id(15)] string? LaunchManifestHash = null,
    [property: Id(16)] int LaunchManifestVersion = 0,
    [property: Id(17)] string? LastStartFailureCode = null,
    [property: Id(18)] NetworkSyncCapabilityMetadata? SyncCapabilities = null);

/// <summary>服务端最终选择的战斗同步能力，字段值与通用网络运行时策略枚举保持一致。</summary>
[GenerateSerializer]
public sealed record NetworkSyncCapabilityMetadata(
    [property: Id(0)] int MetadataVersion,
    [property: Id(1)] string ProfileName,
    [property: Id(2)] int MinimumSchemaVersion,
    [property: Id(3)] int MaximumSchemaVersion,
    [property: Id(4)] int ClientPlayback,
    [property: Id(5)] int Input,
    [property: Id(6)] int Snapshot,
    [property: Id(7)] int Interest,
    [property: Id(8)] int Recovery,
    [property: Id(9)] int ServerValidation,
    [property: Id(10)] int ReliableEvent = 0);

[GenerateSerializer]
public sealed record RoomRuntimeState(
    [property: Id(0)] string RoomId,
    [property: Id(1)] string RoomType,
    [property: Id(2)] string? BattleId,
    [property: Id(3)] ulong WorldId,
    [property: Id(4)] bool IsClosed,
    [property: Id(5)] bool IsInBattle,
    [property: Id(6)] List<string> Members,
    [property: Id(7)] Dictionary<string, RoomMemberState>? MemberStates,
    [property: Id(8)] long ServerNowTicks,
    [property: Id(9)] Dictionary<string, string>? Tags);

[GenerateSerializer]
public sealed record JoinRoomResponse(
    [property: Id(0)] RoomSnapshot Snapshot,
    [property: Id(1)] RoomJoinKind JoinKind,
    [property: Id(2)] long ServerNowTicks);

[GenerateSerializer]
public enum RoomRestoreStatus
{
    Restored = 0,
    NoActiveRoom = 1,
    NotMember = 2,
    RoomClosed = 3,
    RoomExpired = 4,
    InvalidSession = 5,
    Failed = 100
}

[GenerateSerializer]
public enum RoomRestoreErrorCode
{
    None = 0,
    NoAccountRoomMapping = 1,
    AccountNotInRoom = 2,
    RoomClosed = 3,
    RoomExpired = 4,
    InvalidSession = 5,
    InternalError = 100
}

[GenerateSerializer]
public sealed record RestoreRoomResponse(
    [property: Id(0)] RoomSnapshot Snapshot,
    [property: Id(1)] RoomJoinKind JoinKind,
    [property: Id(2)] bool HasActiveRoom,
    [property: Id(3)] bool IsInBattle,
    [property: Id(4)] long ServerNowTicks,
    [property: Id(5)] RoomRestoreStatus Status = RoomRestoreStatus.Restored,
    [property: Id(6)] RoomRestoreErrorCode ErrorCode = RoomRestoreErrorCode.None,
    [property: Id(7)] string Message = "")
{
    public static RestoreRoomResponse Active(RoomSnapshot snapshot, RoomJoinKind joinKind, bool IsInBattle, long serverNowTicks)
    {
        return new RestoreRoomResponse(snapshot, joinKind, true, IsInBattle, serverNowTicks, RoomRestoreStatus.Restored, RoomRestoreErrorCode.None, string.Empty);
    }

    public static RestoreRoomResponse Failed(RoomRestoreStatus status, RoomRestoreErrorCode errorCode, string message)
    {
        return new RestoreRoomResponse(default!, RoomJoinKind.TeamLobby, false, false, DateTime.UtcNow.Ticks, status, errorCode, message ?? string.Empty);
    }
}

[GenerateSerializer]
public sealed record RoomReadyRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] bool Ready);

[GenerateSerializer]
public sealed record JoinRoomMemberRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] bool IsBot = false);

[GenerateSerializer]
public sealed record RoomPickHeroRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] int HeroId,
    [property: Id(2)] int TeamId,
    [property: Id(3)] int SpawnPointId,
    [property: Id(4)] int Level,
    [property: Id(5)] int AttributeTemplateId,
    [property: Id(6)] int BasicAttackSkillId,
    [property: Id(7)] List<int>? SkillIds);

[GenerateSerializer]
public sealed record StartRoomBattleRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] int GameplayId,
    [property: Id(2)] int RuleSetId,
    [property: Id(3)] int ConfigVersion,
    [property: Id(4)] int ProtocolVersion,
    [property: Id(5)] string? WorldType,
    [property: Id(6)] string? ClientId,
    [property: Id(7)] BattleSyncStartOptions? SyncOptions = null);

[GenerateSerializer]
public sealed record StartRoomBattleResponse(
    [property: Id(0)] string BattleId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] bool Started,
    [property: Id(3)] WorldStartAnchor? WorldStartAnchor,
    [property: Id(4)] long ServerNowTicks,
    [property: Id(5)] NetworkSyncCapabilityMetadata? SyncCapabilities = null);

[GenerateSerializer]
public sealed record ListRoomsRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string Region,
    [property: Id(2)] string ServerId,
    [property: Id(3)] int Offset,
    [property: Id(4)] int Limit,
    [property: Id(5)] string? RoomType);

[GenerateSerializer]
public sealed record ListRoomsResponse(
    [property: Id(0)] List<RoomSummary> Rooms,
    [property: Id(1)] int NextOffset);
