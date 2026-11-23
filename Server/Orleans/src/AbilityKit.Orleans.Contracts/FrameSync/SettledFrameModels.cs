using System.Collections.Immutable;

namespace AbilityKit.Orleans.Contracts.FrameSync;

[GenerateSerializer]
public enum SettledCommandDisposition : byte
{
    Executed = 1,
    Rejected = 2,
    Duplicate = 3
}

[GenerateSerializer]
public sealed record SettledFrameCommand(
    [property: Id(0)] int Ordinal,
    [property: Id(1)] SettledCommandDisposition Disposition,
    [property: Id(2)] uint PlayerId,
    [property: Id(3)] int OpCode,
    [property: Id(4)] ImmutableArray<byte> ExecutedPayload);

[GenerateSerializer]
public sealed record SettledFrameDecision(
    [property: Id(0)] int Ordinal,
    [property: Id(1)] int ActorId,
    [property: Id(2)] int EntityVersion,
    [property: Id(3)] int PredictionKey,
    [property: Id(4)] int SkillId,
    [property: Id(5)] bool Accepted,
    [property: Id(6)] int AuthoritativeFrame,
    [property: Id(7)] int Reason,
    [property: Id(8)] int InterruptEpoch,
    [property: Id(9)] long AuthoritySequence);

[GenerateSerializer]
public sealed record BattleFrameSettlement(
    [property: Id(0)] Guid RuntimeGeneration,
    [property: Id(1)] int Frame,
    [property: Id(2)] ImmutableArray<SettledFrameCommand> Commands,
    [property: Id(3)] ImmutableArray<SettledFrameDecision> Decisions,
    [property: Id(4)] bool IsInitialCheckpoint = false);

[GenerateSerializer]
public sealed record FrameSyncSettledFrame(
    [property: Id(0)] int Version,
    [property: Id(1)] ulong RoomId,
    [property: Id(2)] ulong WorldId,
    [property: Id(3)] string BattleId,
    [property: Id(4)] BattleFrameSettlement Settlement)
{
    public const int CurrentVersion = 2;
}

[GenerateSerializer]
public sealed record FrameSyncSettledCatchUpRequest(
    [property: Id(0)] ulong RoomId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] Guid RuntimeGeneration,
    [property: Id(3)] int FromFrameExclusive,
    [property: Id(4)] int ToFrameInclusive);

[GenerateSerializer]
public sealed record FrameSyncSettledCatchUpPayload(
    [property: Id(0)] ImmutableArray<FrameSyncSettledFrame> Frames);

[GenerateSerializer]
public sealed record FrameSyncSettledRecording(
    [property: Id(0)] FrameSyncStartOptions StartOptions,
    [property: Id(1)] ImmutableArray<FrameSyncSettledFrame> Frames,
    [property: Id(2)] int LastSettledFrame,
    [property: Id(3)] int? FaultFrame);

[GenerateSerializer]
public sealed record FrameSyncSettledBinding(
    [property: Id(0)] ulong RoomId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] string BattleId,
    [property: Id(3)] Guid RuntimeGeneration,
    [property: Id(4)] int TickRate,
    [property: Id(5)] int LastSettledFrame,
    [property: Id(6)] bool Faulted);
