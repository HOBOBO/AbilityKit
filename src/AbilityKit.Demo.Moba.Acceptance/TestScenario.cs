using System.Text.Json;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.Acceptance;

/// <summary>MOBA 的断言插件（项目侧），挂在 <see cref="TestScenario.Expectations"/>（opaque）上。</summary>
public sealed class TestExpectations
{
    public MobaAcceptanceTraceExpectation[] MustContain { get; init; } = Array.Empty<MobaAcceptanceTraceExpectation>();
    public MobaAcceptanceTraceExpectation[] MustNotContain { get; init; } = Array.Empty<MobaAcceptanceTraceExpectation>();
    public MobaAcceptanceRelationshipExpectation[] Relationships { get; init; } = Array.Empty<MobaAcceptanceRelationshipExpectation>();
    public MobaAcceptanceStateExpectation[] State { get; init; } = Array.Empty<MobaAcceptanceStateExpectation>();
    public MobaAcceptanceContextExpectation[] Context { get; init; } = Array.Empty<MobaAcceptanceContextExpectation>();
}

/// <summary>把 MOBA acceptance 期望翻译成玩法中立的 <see cref="TestScenario"/>（断言挂到 opaque 的 Expectations）。</summary>
public static class TestScenarioAdapter
{
    public static TestScenario FromMoba(MobaAcceptanceExpectation expectation, string? carrier = null)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        var scenario = expectation.scenario;
        var actors = Pick(scenario?.actors, expectation.actors);
        return new TestScenario
        {
            CaseId = expectation.caseId ?? scenario?.scenarioId ?? string.Empty,
            WorldProfileId = scenario?.worldId ?? expectation.worldId ?? "default",
            Carrier = carrier ?? "dotnet.console",
            TickRate = scenario?.tickRate > 0 ? scenario.tickRate : expectation.tickRate > 0 ? expectation.tickRate : 30,
            Actors = actors?.Where(a => a is not null).Select(a => new TestActor
            {
                Alias = a.alias ?? string.Empty,
                PlayerId = a.playerId,
                Archetype = string.IsNullOrEmpty(a.configKey) ? "unit" : a.configKey,
                BehaviorProfileId = null,
                TeamId = a.teamId,
                HeroId = a.heroId,
                AttributeTemplateId = a.attributeTemplateId,
                SkillIds = a.skillIds ?? a.carriedSkillIds ?? Array.Empty<int>(),
                Position = a.spawnPosition is null ? null : new TestVector3(a.spawnPosition.x, a.spawnPosition.y, a.spawnPosition.z),
                Facing = a.facingDirection is null ? null : new TestVector3(a.facingDirection.x, a.facingDirection.y, a.facingDirection.z),
            }).ToArray() ?? Array.Empty<TestActor>(),
            Setup = Pick(scenario?.setupActions, expectation.setupActions)?.Where(a => a is not null).Select(a => new TestSetupAction
            {
                Action = a.action ?? string.Empty,
                ActorAlias = a.actorAlias ?? a.alias,
                Property = a.property,
                Value = a.value,
            }).ToArray() ?? Array.Empty<TestSetupAction>(),
            Timeline = Pick(scenario?.timeline, expectation.timeline)?.Where(s => s is not null).Select(s => new TestTimelineStep
            {
                AtMs = s.atMs,
                Action = s.action ?? string.Empty,
                ActorAlias = s.actorAlias,
                TargetAlias = s.targetAlias,
                Slot = s.slot,
                Position = s.position is null ? null : new TestVector3(s.position.x, s.position.y, s.position.z),
                Direction = s.direction is null ? null : new TestVector3(s.direction.x, s.direction.y, s.direction.z),
                DurationMs = s.durationMs,
            }).ToArray() ?? Array.Empty<TestTimelineStep>(),
            Expectations = new TestExpectations
            {
                MustContain = expectation.mustContain ?? Array.Empty<MobaAcceptanceTraceExpectation>(),
                MustNotContain = expectation.mustNotContain ?? Array.Empty<MobaAcceptanceTraceExpectation>(),
                Relationships = expectation.relationships ?? Array.Empty<MobaAcceptanceRelationshipExpectation>(),
                State = Pick(scenario?.stateExpectations, expectation.stateExpectations) ?? Array.Empty<MobaAcceptanceStateExpectation>(),
                Context = Pick(scenario?.contextExpectations, expectation.contextExpectations) ?? Array.Empty<MobaAcceptanceContextExpectation>(),
            },
        };
    }

    public static TestScenario Load(string path, string? carrier = null) =>
        FromMoba(AcceptanceJsonCodec.LoadExpectation(path), carrier);

    public static string Serialize(TestScenario scenario) => JsonSerializer.Serialize(scenario, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    });

    private static T[]? Pick<T>(T[]? preferred, T[]? fallback) where T : class =>
        preferred is { Length: > 0 } ? preferred : fallback;
}
