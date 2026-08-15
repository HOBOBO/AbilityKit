using System.Linq;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Test.UnitTest;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 切片 3：acceptance timeline 动词在真实 console 逻辑世界执行的端到端验证。
/// press（富 SkillInputEvent 经 IMobaInputCoordinator 提交）→ wait → 技能结算 → 真实 trace 产生。
/// 与 <see cref="LiveSimSetupActionExecutorTests"/>（切片 2）合成「setup + timeline」的完整场景驱动能力。
/// </summary>
[Trait("Gate", "MobaConsoleSmoke")]
[Trait("Category", "Smoke")]
public class LiveSimTimelineRunnerTests
{
    [Fact]
    public void Timeline_press_and_wait_produces_real_skill_cast_trace()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var executor = new LiveSimSetupActionExecutor(bootstrapper);
        executor.SeedLocalPlayerAlias("caster");
        var runner = new LiveSimTimelineRunner(bootstrapper, executor);

        // 镜像真实期望（skill_10010101_scenario）的 timeline 形态：press slot 1 → wait 900ms
        runner.Run(new[]
        {
            new MobaAcceptanceTimelineStepExpectation
            {
                stepId = "press_skill_1",
                atMs = 0,
                action = "press",
                actorAlias = "caster",
                slot = 1,
            },
            new MobaAcceptanceTimelineStepExpectation
            {
                stepId = "wait_for_skill",
                atMs = 1,
                action = "wait",
                durationMs = 900,
            },
        });

        // 结算尾巴（镜像 TickScenarioTail 的作用）
        for (var i = 0; i < 25; i++) bootstrapper.Tick();

        var services = bootstrapper.RuntimeServices;
        Assert.NotNull(services);
        Assert.True(services!.TryResolve<MobaTraceRegistry>(out var trace) && trace != null,
            "MobaTraceRegistry must be resolvable from the console world.");
        Assert.True(trace!.GetNodesByKind((int)MobaTraceKind.SkillCast).Any(),
            "Timeline press must produce a real SkillCast trace node.");
        Assert.True(trace.GetNodesByKind((int)MobaTraceKind.EffectExecution).Any(),
            "A cast skill must execute at least one formal effect trace.");
    }
}
