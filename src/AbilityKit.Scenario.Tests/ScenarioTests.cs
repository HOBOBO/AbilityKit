using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.Scenario.Tests;

/// <summary>验证中立 IR：场景结构可组合、断言插件是 opaque（项目自定义类型可挂载）。</summary>
public sealed class ScenarioTests
{
    [Fact]
    public void Scenario_ComposesNeutralStructure()
    {
        var scenario = new TestScenario
        {
            CaseId = "case-1",
            Actors = new[]
            {
                new TestActor { Alias = "caster", HeroId = 1001, Position = new TestVector3(-15, 0, 0) },
                new TestActor { Alias = "target", TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            },
            Timeline = new[]
            {
                new TestTimelineStep { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target" },
            },
        };

        Assert.Empty(TestScenarioValidator.Validate(scenario));
        Assert.Equal(2, scenario.Actors.Count);
        Assert.Equal(-15f, scenario.Actors[0].Position!.Value.X);
    }

    [Fact]
    public void Scenario_ExpectationsIsOpaqueProjectPlugin()
    {
        var expectations = new ProjectExpectations { ExpectedHp = 500 };
        var scenario = new TestScenario { CaseId = "case-2", Expectations = expectations };

        Assert.Same(expectations, scenario.Expectations);
        Assert.Equal(500, ((ProjectExpectations)scenario.Expectations!).ExpectedHp);
    }

    [Fact]
    public void Validator_RejectsMissingCaseId()
    {
        var scenario = new TestScenario();
        Assert.Contains(TestScenarioValidator.Validate(scenario), e => e.Contains("caseId"));
    }

    [Fact]
    public void Codec_RoundTripsNeutralScenario()
    {
        var scenario = new TestScenario
        {
            CaseId = "codec-roundtrip",
            EnvironmentProfileId = "jungle-camp",
            Actors = new[]
            {
                new TestActor { Alias = "caster", HeroId = 1001, Position = new TestVector3(-15, 0, 0) },
            },
            Timeline = new[]
            {
                new TestTimelineStep { AtMs = 100, Action = "cast_skill", ActorAlias = "caster" },
            },
        };

        var json = ScenarioCodec.Serialize(scenario);
        var back = ScenarioCodec.Parse(json);

        Assert.Equal("codec-roundtrip", back.CaseId);
        Assert.Equal("jungle-camp", back.EnvironmentProfileId);
        Assert.Equal("caster", back.Actors[0].Alias);
        Assert.Single(back.Timeline);
    }

    /// <summary>模拟一个项目自定义的断言插件（挂在 opaque 的 Expectations 上）。</summary>
    private sealed class ProjectExpectations
    {
        public int ExpectedHp { get; init; }
    }
}
