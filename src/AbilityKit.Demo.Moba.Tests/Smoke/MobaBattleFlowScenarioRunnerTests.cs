using System.Collections.Generic;
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.BattleFlow;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>验证 MOBA 世界执行核心：TestScenario → boot → 生成 actors → 施放 → 采 trace → 中性结果。</summary>
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
}
