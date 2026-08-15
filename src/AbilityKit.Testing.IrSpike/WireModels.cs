using System.Text.Json.Serialization;

namespace AbilityKit.Testing.IrSpike.Wire;

// 现有 MOBA 验收 JSON 的线格式（camelCase，镜像 MobaAcceptanceModels.cs 的字段名）。
// 关键点：这些 DTO 本身就与 Unity 无关（无 UnityEngine.Vector3，用自带 Vec3），
// 唯一的 Unity 耦合是调用处的 UnityEngine.JsonUtility。这里改用 System.Text.Json 反序列化，
// 证明「序列化器替换」这一步成本极低。
// 字段名保持与现有 .expected.json 一致，配合 CamelCase 命名策略。

public sealed class ExpectationWire
{
    public string? CaseId { get; set; }
    public string? Description { get; set; }
    public string? WorldId { get; set; }
    public int TickRate { get; set; }
    public bool Accelerated { get; set; }
    public string? Category { get; set; }
    public string[]? Tags { get; set; }
    public ConfigWire? Config { get; set; }
    public ScenarioWire? Scenario { get; set; }
    public ActorWire[]? Actors { get; set; }
    public SetupActionWire[]? SetupActions { get; set; }
    public TimelineStepWire[]? Timeline { get; set; }
    public StateExpectationWire[]? StateExpectations { get; set; }
    public ContextExpectationWire[]? ContextExpectations { get; set; }
    public TraceExpectationWire[]? MustContain { get; set; }
    public TraceExpectationWire[]? MustNotContain { get; set; }
    public RelationshipWire[]? Relationships { get; set; }
}

public sealed class ScenarioWire
{
    public string? ScenarioId { get; set; }
    public string? WorldId { get; set; }
    public int TickRate { get; set; }
    public bool Accelerated { get; set; }
    public string? Category { get; set; }
    public string[]? Tags { get; set; }
    public ActorWire[]? Actors { get; set; }
    public SetupActionWire[]? SetupActions { get; set; }
    public TimelineStepWire[]? Timeline { get; set; }
    public StateExpectationWire[]? StateExpectations { get; set; }
    public ContextExpectationWire[]? ContextExpectations { get; set; }
}

public sealed class ConfigWire
{
    public int SkillId { get; set; }
    public int EffectId { get; set; }
    public int TriggerId { get; set; }
    public ExpectedActionWire[]? ExpectedActions { get; set; }
}

public sealed class ExpectedActionWire
{
    public long ActionId { get; set; }
    public string? Type { get; set; }
}

public sealed class ActorWire
{
    public string? Alias { get; set; }
    public string? PlayerId { get; set; }
    public int TeamId { get; set; }
    public int HeroId { get; set; }
    public int[]? SkillIds { get; set; }
    public Vec3Wire? SpawnPosition { get; set; }
    public Vec3Wire? FacingDirection { get; set; }
}

public sealed class SetupActionWire
{
    public string? Action { get; set; }
    public string? ActorAlias { get; set; }
    public string? Property { get; set; }
    public double Value { get; set; }
}

public sealed class TimelineStepWire
{
    public string? StepId { get; set; }
    public int AtMs { get; set; }
    public string? Action { get; set; }
    public string? ActorAlias { get; set; }
    public string? TargetAlias { get; set; }
    public int Slot { get; set; }
    public Vec3Wire? Position { get; set; }
    public Vec3Wire? Direction { get; set; }
}

public sealed class StateExpectationWire
{
    public string? Alias { get; set; }
    public string? Property { get; set; }
    public string? Comparator { get; set; }
    public double ExpectedFloat { get; set; }
    public int ExpectedInt { get; set; }
    public bool ExpectedBool { get; set; }
    public Vec3Wire? Tolerance { get; set; }
    public string? Note { get; set; }
}

public sealed class ContextExpectationWire
{
    public string? Alias { get; set; }
    public string? Kind { get; set; }
    public string? Property { get; set; }
    public string? Comparator { get; set; }
}

public sealed class TraceExpectationWire
{
    public string? Kind { get; set; }
    public long ConfigId { get; set; }
    public long UnderEffectId { get; set; }
    public int MinCount { get; set; }
    public int MaxCount { get; set; }
}

public sealed class RelationshipWire
{
    public string? ParentKind { get; set; }
    public long ParentConfigId { get; set; }
    public string? ChildKind { get; set; }
    public long ChildConfigId { get; set; }
}

public sealed class Vec3Wire
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
