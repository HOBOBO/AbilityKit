using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BattleScenario;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.BattleScenario;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>战斗流程回归：批量跑提交的 .battlescenario 目录，断言全部通过。挂 gate（Gate=BattleScenario）做 CI 回归。</summary>
public sealed class BattleScenarioRegressionTests
{
    [Theory]
    [InlineData("", 1)]
    [InlineData("SkillPresentation", 28)]
    [Trait("Gate", "BattleScenario")]
    public void CommittedFlows_AllPass(string subdirectory, int minimumCases)
    {
        var dir = Path.Combine(ResolveFlowsDir(), subdirectory);
        var result = BattleScenarioBatchRunner.RunDirectory(dir);

        Assert.True(result.Total >= minimumCases, $"场景目录 {dir} 至少应有 {minimumCases} 个用例。");
        var failed = result.Cases.Where(c => !c.Passed).ToList();
        Assert.True(failed.Count == 0,
            "有 " + failed.Count + " 个流程失败:\n" + string.Join("\n", failed.Select(c => c.CaseId + ": " + c.Summary)));
    }

    [Theory]
    [InlineData("jinx-r-long-hit", 90f, 95f)]
    [InlineData("jinx-r-miss", 93f, 95f)]
    [InlineData("jinx-r-max-range", 248f, 250f)]
    [Trait("Gate", "BattleScenario")]
    public void JinxRocketUsesOneContinuousFlightAndCleansUp(string caseId, float minDistance, float maxDistance)
    {
        const int rocketTemplateId = 32020403;
        var scenario = BattleScenarioCompiler.CompileFile(
            Path.Combine(ResolveFlowsDir(), "SkillPresentation", caseId + ".battlescenario"));
        var active = new List<ProjectileRuntimeState>();
        ProjectileId? rocketId = null;
        var removed = false;
        var distance = 0f;
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario, (world, _) =>
        {
            world.ObserveProjectiles("caster", rocketTemplateId, active);
            if (active.Count == 0)
            {
                removed |= rocketId.HasValue;
                return;
            }

            Assert.False(removed, "Rocket must not reappear after cleanup.");
            var rocket = Assert.Single(active);
            if (rocketId.HasValue)
            {
                Assert.Equal(rocketId.Value, rocket.Id);
                Assert.True(rocket.DistanceTravelled > distance, "The active rocket must keep advancing.");
            }
            rocketId ??= rocket.Id;
            distance = rocket.DistanceTravelled;
        });

        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        Assert.True(removed, "The rocket must stop being active before the scenario ends.");
        Assert.InRange(distance, minDistance, maxDistance);
    }

    [Fact]
    [Trait("Gate", "BattleScenario")]
    public void JhinTrapRequiresCastSpacingAndRecoversOneChargeAtATime()
    {
        const int trapLaunchEffectId = 30010301;
        var scenario = BattleScenarioCompiler.CompileFile(
            Path.Combine(ResolveFlowsDir(), "SkillPresentation", "jhin-e-charges.battlescenario"));
        var tickRate = scenario.ResolveExecution().TickRate;
        var rejectedInputMs = new[] { 2400, 5200, 18300, 21200, 36300 };
        var rejectionIndex = 0;
        var casterId = 0;
        var remainingMana = 0f;
        int? startFrame = null;
        var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario, (world, frame) =>
        {
            if (!startFrame.HasValue)
            {
                startFrame = frame - 1;
                Assert.True(world.TryGetActorId("caster", out casterId));
                world.SetScenarioActorAttribute(casterId, "mana", 180);
                world.SetScenarioActorAttribute(casterId, "MANA_REGEN_PER_SECOND", 0);
            }
            remainingMana = world.GetActorMana(casterId);
            if (rejectionIndex >= rejectedInputMs.Length ||
                frame - startFrame.Value < rejectedInputMs[rejectionIndex] * tickRate / 1000)
                return;

            var result = world.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                casterId, 3, new Vec3(0, 0, -6), new Vec3(1, 0, 0), 0);
            Assert.False(result.Success, $"Trap cast at {rejectedInputMs[rejectionIndex]} ms should be rejected.");
            rejectionIndex++;
        });

        Assert.Equal(rejectedInputMs.Length, rejectionIndex);
        Assert.Equal(60f, remainingMana);
        Assert.True(outcome.Result.Passed, outcome.Result.Summary);
        var launchFrames = outcome.TraceNodes
            .Where(node => node.Kind == "EffectExecution" && node.ConfigId == trapLaunchEffectId)
            .Select(node => node.Frame)
            .OrderBy(frame => frame)
            .ToArray();
        Assert.Equal(4, launchFrames.Length);
        var acceptedInputOffsetsMs = new[] { 2200, 18200, 36200 };
        for (var index = 0; index < acceptedInputOffsetsMs.Length; index++)
        {
            var expectedFrames = acceptedInputOffsetsMs[index] * tickRate / 1000;
            Assert.InRange(launchFrames[index + 1] - launchFrames[0], expectedFrames - 2, expectedFrames + 2);
        }
    }

    private static string ResolveFlowsDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "src", "AbilityKit.Demo.Moba.BattleScenario.Runner", "Flows");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        throw new DirectoryNotFoundException("找不到 Flows 目录（未定位到仓库根）");
    }
}
