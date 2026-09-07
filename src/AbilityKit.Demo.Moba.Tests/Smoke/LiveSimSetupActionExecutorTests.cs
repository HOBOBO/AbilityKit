using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Game.Test.UnitTest;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 切片 2：setupActions 在真实 console 逻辑世界执行的端到端验证。
/// spawn_actor → set_attr → move_to → add_buff → remove_buff → wait 全链路跑通（纯 dotnet，无 Unity）。
/// Boot 模式镜像 <c>MobaSummonRollbackTests</c>（直接 bootstrapper，不走 BattleTestScript）。
/// </summary>
[Trait("Gate", "MobaConsoleSmoke")]
[Trait("Category", "Smoke")]
public class LiveSimSetupActionExecutorTests
{
    [Fact]
    public void Setup_actions_execute_against_real_console_world()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();

        var executor = new LiveSimSetupActionExecutor(bootstrapper);

        // 本地玩家注册为 caster（acceptance 场景的施法者通常绑定本地玩家）
        executor.SeedLocalPlayerAlias("caster");
        Assert.True(executor.TryGetActorId("caster", out var casterId) && casterId > 0);

        // spawn_actor：召唤一个敌对 target
        executor.Execute(new MobaAcceptanceSetupActionExpectation
        {
            action = "spawn_actor",
            alias = "target",
            teamId = 2,
            heroId = 1001,
            attributeTemplateId = 1001,
            position = new MobaAcceptanceVector3Expectation { x = 3, y = 0, z = 0 },
        });
        Assert.True(executor.TryGetActorId("target", out var targetId) && targetId > 0);

        // set_attr：hp = 555
        executor.Execute(new MobaAcceptanceSetupActionExpectation
        {
            action = "set_attr",
            actorAlias = "target",
            property = "hp",
            value = 555f,
        });
        Assert.Equal(555f, executor.GetActorHp(targetId));

        // move_to：(5, 0, 1)
        executor.Execute(new MobaAcceptanceSetupActionExpectation
        {
            action = "move_to",
            actorAlias = "target",
            position = new MobaAcceptanceVector3Expectation { x = 5, y = 0, z = 1 },
        });
        var pos = executor.GetActorPosition(targetId);
        Assert.Equal(5f, pos.X);
        Assert.Equal(1f, pos.Z);

        // add_buff / remove_buff（ caster 作源）
        executor.Execute(new MobaAcceptanceSetupActionExpectation
        {
            action = "add_buff",
            targetAlias = "target",
            sourceAlias = "caster",
            buffId = 10010000,
        });
        executor.Execute(new MobaAcceptanceSetupActionExpectation
        {
            action = "remove_buff",
            targetAlias = "target",
            buffId = 10010000,
        });

        // wait/tick 不抛
        executor.Execute(new MobaAcceptanceSetupActionExpectation { action = "wait", durationMs = 100 });
    }
}
