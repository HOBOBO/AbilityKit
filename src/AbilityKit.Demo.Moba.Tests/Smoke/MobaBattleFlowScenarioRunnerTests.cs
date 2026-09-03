using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>验证 MOBA 世界执行核心：TestScenario → boot → 生成 actors → 施放 → 采 trace → 断言判定 → 中性结果。</summary>
public sealed class MobaBattleFlowScenarioRunnerTests
{
    [Fact]
    public void Run_CastsSkillAndCapturesTrace()
    {
        var scenario = BattleFlowCompiler.Compile("runner-smoke", new BattleBlock[]
        {
            new SetEnvironmentBlock { ProfileId = "jungle-camp" },
            new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1", Position = new TestVector3(-15, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
        });

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed);
        Assert.Contains("actors=2", result.Summary);
        Assert.Contains("traceNodes=", result.Summary);
        Assert.Contains("env=jungle-camp(3个)", result.Summary);
    }

    [Fact]
    public void Run_WithAssertion_ProducesVerdict()
    {
        // 断言一个必然不存在的 trace kind（mustNotContain），verdict 应为 PASSED
        var scenario = BattleFlowCompiler.Compile("runner-assert", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1", Position = new TestVector3(-15, 0, 0) },
            new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
            new TestAssertBlock { MustNotContain = new MobaTraceAssertion { Kind = "NonExistentKind", ConfigId = 0 } },
        });

        var result = MobaBattleFlowScenarioRunner.Run(scenario);

        Assert.True(result.Passed);
        Assert.Contains("verdict=PASSED", result.Summary);
    }

    [Fact]
    public void Codec_RoundTripsAssertions()
    {
        var scenario = BattleFlowCompiler.Compile("codec-assert", new BattleBlock[]
        {
            new TestAssertBlock { MustContain = new MobaTraceAssertion { Kind = "SkillCast", ConfigId = 10010101 } },
        });

        var json = ScenarioCodec.Serialize(scenario);
        var back = ScenarioCodec.Parse(json);

        var assertions = back.Expectations as MobaBattleFlowAssertions;
        Assert.NotNull(assertions);
        Assert.Single(assertions!.MustContain);
        Assert.Equal("SkillCast", assertions.MustContain[0].Kind);
    }

    /// <summary>测试内的断言积木（镜像 MOBA 的 AssertTraceBlock，但直接用 .NET 可访问的 MobaBattleFlowAssertions）。</summary>
    private sealed class TestAssertBlock : BattleAtomicBlock
    {
        public MobaTraceAssertion? MustContain { get; set; }
        public MobaTraceAssertion? MustNotContain { get; set; }

        public override void Compile(BattleFlowBuilder builder)
        {
            var assertions = builder.Expectations as MobaBattleFlowAssertions ?? new MobaBattleFlowAssertions();
            if (MustContain != null) assertions.MustContain.Add(MustContain);
            if (MustNotContain != null) assertions.MustNotContain.Add(MustNotContain);
            builder.SetExpectations(assertions);
        }
    }
}
