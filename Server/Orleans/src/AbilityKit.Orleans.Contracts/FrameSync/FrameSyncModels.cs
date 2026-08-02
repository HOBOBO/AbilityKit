using System.Collections.Generic;
using Orleans.Serialization;

namespace AbilityKit.Orleans.Contracts.FrameSync;

[GenerateSerializer]
public sealed record FrameSyncStartOptions(
    [property: Id(0)] ulong RoomId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] int TickRate,
    [property: Id(3)] string? BattleId,
    [property: Id(4)] string? SyncTemplateId,
    [property: Id(5)] int RuntimeMode,
    [property: Id(6)] bool EnableRecording = false,
    [property: Id(7)] int MinTickRate = 10,
    [property: Id(8)] int MaxTickRate = 60);

[GenerateSerializer]
public sealed record FrameInputItem(
    [property: Id(0)] uint PlayerId,
    [property: Id(1)] int OpCode,
    [property: Id(2)] byte[] Payload);

[GenerateSerializer]
public enum FrameInputSubmitReason
{
    None = 0,
    WorldMismatch = 1,
    NegativeFrame = 2,
    FrameAlreadyProcessed = 3,
    FrameTooFarAhead = 4,
    RateLimited = 100
}

[GenerateSerializer]
public sealed record FrameInputSubmitResult(
    [property: Id(0)] bool Accepted,
    [property: Id(1)] int ServerFrame,
    [property: Id(2)] FrameInputSubmitReason Reason);

[GenerateSerializer]
public sealed record FramePushedEvent(
    [property: Id(0)] ulong RoomId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] int Frame,
    [property: Id(3)] List<FrameInputItem> Inputs);

[GenerateSerializer]
public sealed record FrameSyncCatchUpRequest(
    [property: Id(0)] ulong RoomId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] int FromFrameExclusive,
    [property: Id(3)] int ToFrameInclusive);

[GenerateSerializer]
public sealed record FrameSyncCatchUpPayload(
    [property: Id(0)] ulong RoomId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] int StartFrame,
    [property: Id(3)] List<List<FrameInputItem>> FrameInputs);

/// <summary>
/// 帧同步完整录制数据，包含启动参数和从帧 0 起的所有帧输入序列。
/// 可序列化到文件用于离线回放、desync 复现和确定性验证。
/// </summary>
[GenerateSerializer]
public sealed record FrameSyncRecording(
    [property: Id(0)] FrameSyncStartOptions StartOptions,
    [property: Id(1)] List<List<FrameInputItem>> AllFrameInputs,
    [property: Id(2)] long RecordedAtTicks,
    [property: Id(3)] int TotalFrames);

/// <summary>
/// 帧同步运行时健康指标快照。用于运维监控和联调诊断。
/// </summary>
[GenerateSerializer]
public sealed record FrameSyncMetrics(
    [property: Id(0)] ulong RoomId,
    [property: Id(1)] ulong WorldId,
    [property: Id(2)] string? BattleId,
    [property: Id(3)] int CurrentFrame,
    [property: Id(4)] int TickRate,
    [property: Id(5)] int ObserverCount,
    [property: Id(6)] double AvgTickDeltaMs,
    [property: Id(7)] double LastTickDeltaMs,
    [property: Id(8)] double EffectiveHz,
    [property: Id(9)] int TotalInputsReceived,
    [property: Id(10)] int CatchUpHistoryFrames,
    [property: Id(11)] int RecordingFrameCount,
    [property: Id(12)] long UptimeSeconds);
