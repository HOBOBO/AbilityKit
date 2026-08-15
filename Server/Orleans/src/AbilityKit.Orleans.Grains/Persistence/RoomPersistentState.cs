using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using Orleans.Serialization;

namespace AbilityKit.Orleans.Grains.Persistence;

[GenerateSerializer]
public sealed record RoomPersistentMember(
    [property: Id(0)] string AccountId,
    [property: Id(1)] RoomMemberState State);

[GenerateSerializer]
public sealed record RoomGameplayPersistentState(
    [property: Id(0)] string Format,
    [property: Id(1)] int Version,
    [property: Id(2)] byte[] Payload);

[GenerateSerializer]
public enum RoomBattleCommitStatus
{
    None = 0,
    Pending = 1,
    Committed = 2,
    Failed = 3
}

[GenerateSerializer]
public sealed record RoomLaunchPersistentState(
    [property: Id(0)] long Generation,
    [property: Id(1)] long DeadlineUnixMs,
    [property: Id(2)] int ManifestVersion,
    [property: Id(3)] string? ManifestHash,
    [property: Id(4)] List<string> LockedRoster);

[GenerateSerializer]
public sealed record RoomBattleCommitPersistentState(
    [property: Id(0)] long Generation,
    [property: Id(1)] string? CommitId,
    [property: Id(2)] RoomBattleCommitStatus Status,
    [property: Id(3)] string? InitSpecHash,
    [property: Id(4)] string? BattleId,
    [property: Id(5)] ulong WorldId,
    [property: Id(6)] WorldStartAnchor? WorldStartAnchor,
    [property: Id(7)] int AttemptCount,
    [property: Id(8)] string? LastError,
    [property: Id(9)] NetworkSyncCapabilityMetadata? SyncCapabilities = null);

[GenerateSerializer]
public sealed record RoomCommandDedupEntry(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string CommandId,
    [property: Id(2)] string CommandName,
    [property: Id(3)] string PayloadHash,
    [property: Id(4)] bool Success,
    [property: Id(5)] bool Applied,
    [property: Id(6)] RoomOperationErrorCode ErrorCode,
    [property: Id(7)] long AppliedRevision,
    [property: Id(8)] long CreatedAtUnixMs);

[GenerateSerializer]
public sealed record RoomPersistentState(
    [property: Id(0)] int SchemaVersion,
    [property: Id(1)] RoomSummary Summary,
    [property: Id(2)] string DirectoryKey,
    [property: Id(3)] RoomPhase Phase,
    [property: Id(4)] string PhaseReason,
    [property: Id(5)] List<RoomPersistentMember> Members,
    [property: Id(6)] long NextJoinOrdinal,
    [property: Id(7)] RoomGameplayPersistentState GameplayState,
    [property: Id(8)] long Revision,
    [property: Id(9)] long EventSequence,
    [property: Id(10)] RoomLaunchPersistentState Launch,
    [property: Id(11)] RoomBattleCommitPersistentState BattleCommit,
    [property: Id(12)] List<RoomCommandDedupEntry> CommandDedupEntries,
    [property: Id(13)] string? TerminalReason,
    [property: Id(14)] long UpdatedAtUnixMs)
{
    public const int CurrentSchemaVersion = 1;
}
