using System.Reflection;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Test.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
public sealed class GragasQPredictionReplayTests(ITestOutputHelper output)
{
    private const float Delta = 1f / 30f;

    [Theory]
    [InlineData("flight")]
    [InlineData("deployed")]
    public void Published_q_replays_registered_state_twice_in_live_phase(string checkpointPhase) =>
        RunScenario(checkpointPhase, manualDetonation: false, correct: true, throughCleanup: false);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Published_q_without_corrections_damages_once_and_cleans_up(bool manualDetonation) =>
        RunScenario("deployed", manualDetonation, correct: false, throughCleanup: true);

    [Theory]
    [InlineData("flight", true)]
    [InlineData("deployed", false)]
    public void Published_q_after_two_corrections_still_damages_once_and_cleans_up(string checkpointPhase, bool manualDetonation) =>
        RunScenario(checkpointPhase, manualDetonation, correct: true, throughCleanup: true);

    [Theory]
    [InlineData("flight", true)]
    [InlineData("deployed", false)]
    public void Published_q_rejects_post_cleanup_correction_before_import(string checkpointPhase, bool manualDetonation) =>
        RunScenario(checkpointPhase, manualDetonation, correct: false, throughCleanup: true, correctAfterCleanup: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Published_q_checks_late_deployment_after_cast_entity_cleanup(bool correctAfterCleanup) =>
        RunScenario("deployed", manualDetonation: false, correct: !correctAfterCleanup, throughCleanup: true,
            correctAfterCleanup: correctAfterCleanup, afterCastEntityCleanup: true);

    private void RunScenario(string checkpointPhase, bool manualDetonation, bool correct, bool throughCleanup,
        bool correctAfterCleanup = false, bool afterCastEntityCleanup = false)
    {
        var config = BattleStartConfig.CreateDefault();
        config.Players = config.Players.Take(1).ToList();
        var caster = config.Players[0];
        caster.HeroId = caster.AttributeTemplateId = 3005;
        caster.BasicAttackSkillId = 30050101;
        caster.SkillIds = [30050101, 30050201, 30050301, 30050401, 30050501];
        caster.PositionX = caster.PositionZ = 100;
        caster.EnableBrainOnSpawn = false;

        using var battle = new ConsoleBattleBootstrapper(config, additionalModules: new[] { new MobaTraceAdapterModule() });
        battle.Initialize();
        battle.Start();
        for (var step = 0; step < 8 && battle.Context.EcsWorld == null; step++) battle.Tick(Delta);
        battle.SetupBattle();
        for (var step = 0; step < 10; step++) battle.Tick(Delta);
        var world = GetBootstrapField<IWorld>(battle, "_runtimeWorld");
        var worlds = GetBootstrapField<WorldManager>(battle, "_runtimeWorldManager");
        var players = world.Services.Resolve<MobaPlayerActorMapService>();
        var player = new PlayerId(caster.PlayerId);
        Assert.True(players.TryGetActorId(player, out var casterId));
        var setup = new LiveSimSetupActionExecutor(battle);
        setup.Execute(new MobaAcceptanceSetupActionExpectation
        {
            action = "spawn_actor", alias = "target", playerId = "gragas_q_target", heroId = 1001,
            attributeTemplateId = 1001, teamId = 2, position = new MobaAcceptanceVector3Expectation { x = 100, z = 106 },
        });
        Assert.True(setup.TryGetActorId("target", out var targetId));
        setup.DisableActorBrain(targetId);
        setup.SetScenarioActorAttribute(targetId, "MAGIC_DEFENSE", 0);
        setup.SetScenarioActorAttribute(targetId, "PER_SECOND_BLOOD_R", 0);
        battle.Tick(Delta);
        GragasQPredictionReplayScenario.Run(world, worlds, player, casterId, targetId, checkpointPhase,
            manualDetonation, correct, throughCleanup, Delta, output.WriteLine, correctAfterCleanup, afterCastEntityCleanup);
    }

    private static T GetBootstrapField<T>(ConsoleBattleBootstrapper battle, string field) =>
        (T)typeof(ConsoleBattleBootstrapper).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
}
