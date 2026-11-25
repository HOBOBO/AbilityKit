using System;
using Orleans;

namespace AbilityKit.Orleans.Contracts.Battle;

public static class BattleDiagnosticContractConstants
{
    public const string SchemaVersion = "abilitykit-battle-diagnostics.v2";
}

/// <summary>
/// 运行时诊断查询。StoreRevision 为零表示读取当前保留视图。
/// </summary>
[GenerateSerializer]
public sealed record BattleDiagnosticEventsQuery(
    [property: Id(0)] long RequestId,
    [property: Id(1)] int? ActorId,
    [property: Id(2)] int? SkillId,
    [property: Id(3)] int Limit,
    [property: Id(4)] long StoreRevision = 0,
    [property: Id(5)] bool NewestFirst = true,
    [property: Id(6)] int Offset = 0);

/// <summary>
/// 运行时诊断查询结果。Available 结果仍可能不含事件；消费方必须分别解释状态及可用性。
/// </summary>
[GenerateSerializer]
public sealed record BattleDiagnosticEventsResult(
    [property: Id(0)] string Status,
    [property: Id(1)] string Availability,
    [property: Id(2)] long StoreRevision,
    [property: Id(3)] bool HasMore,
    [property: Id(4)] string SchemaVersion,
    [property: Id(5)] long MonotonicTimestampFrequency,
    [property: Id(6)] string SessionId,
    [property: Id(7)] string WorldId,
    [property: Id(8)] long Generation,
    [property: Id(9)] string? Message,
    [property: Id(10)] BattleDiagnosticEventRecord[] Events,
    [property: Id(11)] int Offset,
    [property: Id(12)] int Limit);

/// <summary>
/// 单个运行时事件的传输投影。SkillId 对应运行时 ConfigId，SkillInstanceId 对应 RuntimeId，NodeId 对应 ContextId，RootId 对应 RootContextId。运行时来源未提供可信关系时，父级、来源及所有者 ID 为零；零不能解释为关系边。
/// </summary>
[GenerateSerializer]
public sealed record BattleDiagnosticEventRecord(
    [property: Id(0)] int Frame,
    [property: Id(1)] long Sequence,
    [property: Id(2)] long MonotonicTimestamp,
    [property: Id(3)] string EventType,
    [property: Id(4)] string Channel,
    [property: Id(5)] string Outcome,
    [property: Id(6)] long SourceActorId,
    [property: Id(7)] long TargetActorId,
    [property: Id(8)] int SkillId,
    [property: Id(9)] long SkillInstanceId,
    [property: Id(10)] long RootContextId,
    [property: Id(11)] long ContextId,
    [property: Id(12)] string? Message,
    [property: Id(13)] int Generation,
    [property: Id(14)] long NodeId,
    [property: Id(15)] long RootId,
    [property: Id(16)] long ParentId,
    [property: Id(17)] long SourceContextId,
    [property: Id(18)] long OwnerContextId);
