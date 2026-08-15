namespace AbilityKit.Testing.IrSpike.Ir;

// 玩法无关的规范场景 IR（TestScenario）。
// 设计意图：configId / actionId / buffId 等都是「不透明 token」（opaque long），
// IR 本身不知道它们是 MOBA 的技能/效果/buff id —— 同一份 IR 也能由 Shooter 或未来玩法产出。
// 这是「去 MOBA 化」的核心：把 MOBA 整数 id 的语义留在 Wire 层，IR 只做结构化匹配。

public sealed record TestScenario
{
    public required string CaseId { get; init; }
    public string? Description { get; init; }
    public string? WorldId { get; init; }
    public int TickRate { get; init; }
    public bool Accelerated { get; init; }
    public string? Category { get; init; }
    public string[] Tags { get; init; } = [];
    public ActorDecl[] Actors { get; init; } = [];
    public SetupAction[] Setup { get; init; } = [];
    public TimelineStep[] Timeline { get; init; } = [];
    public ScenarioExpectations Expectations { get; init; } = new();

    // 配置引用：作为不透明 token 透传给判定器（用于定位 effectRoot 等）。
    public long SkillId { get; init; }
    public long EffectId { get; init; }
}

public sealed record ActorDecl(
    string Alias,
    string? PlayerId,
    int TeamId,
    int HeroId,
    int[] SkillIds,
    Vec3? Spawn,
    Vec3? Facing);

public sealed record SetupAction(string Action, string? ActorAlias, string? Property, double Value);

public sealed record TimelineStep(
    int AtMs,
    string Action,
    string? ActorAlias,
    string? TargetAlias,
    int Slot,
    Vec3? Position,
    Vec3? Direction);

public sealed record ScenarioExpectations
{
    public TraceExpectation[] MustContain { get; init; } = [];
    public TraceExpectation[] MustNotContain { get; init; } = [];
    public StateExpectation[] State { get; init; } = [];
    public Relationship[] Relationships { get; init; } = [];
    public ExpectedAction[] ExpectedActions { get; init; } = [];
}

public sealed record TraceExpectation(
    string Kind,
    long ConfigId,
    long UnderEffectId,
    int MinCount,
    int MaxCount);

public sealed record StateExpectation(
    string Alias,
    string Property,
    string Comparator,
    double? ExpectedFloat,
    int? ExpectedInt,
    bool? ExpectedBool,
    Vec3? Tolerance);

public sealed record Relationship(
    string ParentKind,
    long ParentConfigId,
    string ChildKind,
    long ChildConfigId);

public sealed record ExpectedAction(long ActionId, string? Type);

public readonly record struct Vec3(double X, double Y, double Z);

// 运行期观测到的一条 trace（与 MobaAcceptanceTraceRecord 对齐的最小子集）。
public sealed record TraceRecord(string Kind, long ConfigId, long RootId);
