using System.Text.Json;
using AbilityKit.Testing.IrSpike.Ir;
using AbilityKit.Testing.IrSpike.Wire;

namespace AbilityKit.Testing.IrSpike;

// 编解码器：两件事
//  1) Parse：把现有 MOBA .expected.json（Wire 层）反序列化并映射成玩法无关 IR。
//     —— 证明 JsonUtility → System.Text.Json 替换可行，且 Wire→IR 翻译层成本可控。
//  2) SerializeSummary：把判定结果按现有 canonical summary.json 形态写出。
//     —— 证明产物格式与既有平台/CI 完全兼容。
// 映射策略与 MobaAcceptanceTraceExporter.BuildSummary 一致：scenario.* 优先，扁平字段回退。
public static class ScenarioCodec
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static TestScenario Parse(string json)
    {
        var wire = JsonSerializer.Deserialize<ExpectationWire>(json, JsonOpts)
                   ?? throw new InvalidOperationException("failed to parse expectation wire JSON");
        return MapToIr(wire);
    }

    private static TestScenario MapToIr(ExpectationWire w)
    {
        // scenario.* 优先，扁平字段回退 —— 与 BuildSummary 的回退逻辑一致。
        var sc = w.Scenario;
        return new TestScenario
        {
            CaseId = w.CaseId ?? sc?.ScenarioId ?? "<unnamed>",
            Description = w.Description,
            WorldId = sc?.WorldId ?? w.WorldId ?? "default",
            TickRate = sc != null && sc.TickRate != 0 ? sc.TickRate : w.TickRate,
            Accelerated = sc?.Accelerated ?? w.Accelerated,
            Category = ResolveCategory(w, sc),
            Tags = (sc?.Tags ?? w.Tags ?? []).ToArray()!,
            Actors = (sc?.Actors ?? w.Actors ?? []).Select(MapActor).ToArray(),
            Setup = (sc?.SetupActions ?? w.SetupActions ?? []).Select(MapSetup).ToArray(),
            Timeline = (sc?.Timeline ?? w.Timeline ?? []).Select(MapTimeline).ToArray(),
            SkillId = w.Config?.SkillId ?? 0,
            EffectId = w.Config?.EffectId ?? 0,
            Expectations = new ScenarioExpectations
            {
                MustContain = (w.MustContain ?? []).Select(MapTrace).ToArray(),
                MustNotContain = (w.MustNotContain ?? []).Select(MapTrace).ToArray(),
                State = (sc?.StateExpectations ?? w.StateExpectations ?? []).Select(MapState).ToArray(),
                Relationships = (w.Relationships ?? []).Select(MapRel).ToArray(),
                ExpectedActions = (w.Config?.ExpectedActions ?? []).Select(MapAction).ToArray(),
            },
        };
    }

    private static ActorDecl MapActor(ActorWire a) => new(
        Alias: a.Alias ?? "",
        PlayerId: a.PlayerId,
        TeamId: a.TeamId,
        HeroId: a.HeroId,
        SkillIds: a.SkillIds ?? [],
        Spawn: MapVec(a.SpawnPosition),
        Facing: MapVec(a.FacingDirection));

    private static SetupAction MapSetup(SetupActionWire s) =>
        new(s.Action ?? "", s.ActorAlias, s.Property, s.Value);

    private static TimelineStep MapTimeline(TimelineStepWire t) => new(
        AtMs: t.AtMs,
        Action: t.Action ?? "",
        ActorAlias: t.ActorAlias,
        TargetAlias: t.TargetAlias,
        Slot: t.Slot,
        Position: MapVec(t.Position),
        Direction: MapVec(t.Direction));

    private static TraceExpectation MapTrace(TraceExpectationWire t) => new(
        Kind: t.Kind ?? "",
        ConfigId: t.ConfigId,
        UnderEffectId: t.UnderEffectId,
        MinCount: t.MinCount,
        MaxCount: t.MaxCount);

    private static StateExpectation MapState(StateExpectationWire s) => new(
        Alias: s.Alias ?? "",
        Property: s.Property ?? "",
        Comparator: s.Comparator ?? "eq",
        ExpectedFloat: s.ExpectedFloat != 0 ? s.ExpectedFloat : null,
        ExpectedInt: s.ExpectedInt != 0 ? s.ExpectedInt : null,
        ExpectedBool: s.Property is "hasBuff" or "buff" ? s.ExpectedBool : null,
        Tolerance: MapVec(s.Tolerance));

    private static Relationship MapRel(RelationshipWire r) => new(
        ParentKind: r.ParentKind ?? "",
        ParentConfigId: r.ParentConfigId,
        ChildKind: r.ChildKind ?? "",
        ChildConfigId: r.ChildConfigId);

    private static ExpectedAction MapAction(ExpectedActionWire a) => new(a.ActionId, a.Type);

    private static Vec3? MapVec(Vec3Wire? v) =>
        v is null ? null : new Vec3(v.X, v.Y, v.Z);

    private static string ResolveCategory(ExpectationWire w, ScenarioWire? sc) =>
        !string.IsNullOrEmpty(sc?.Category) ? sc.Category!
        : !string.IsNullOrEmpty(w.Category) ? w.Category!
        : "contract";

    public static string SerializeSummary(object summary) =>
        JsonSerializer.Serialize(summary, JsonOpts);
}
