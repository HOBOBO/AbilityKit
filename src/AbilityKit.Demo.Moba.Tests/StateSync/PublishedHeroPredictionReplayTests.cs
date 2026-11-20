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
public sealed class PublishedHeroPredictionReplayTests(ITestOutputHelper output)
{
    private const float Delta = 1f / 30f;

    [Theory]
    [InlineData("LuxQ")]
    [InlineData("KarthusE")]
    [InlineData("KarthusR")]
    [InlineData("AnnieR")]
    [InlineData("VeigarE")]
    public void Published_skill_replays_live_state_twice_and_settles(string skill) => RunScenario(skill);

    [Fact]
    public void Published_annie_r_replays_across_expiry_and_reclaims_after_restore_floor() => RunScenario("AnnieRExpiry");

    private void RunScenario(string skill)
    {
        var scenario = PublishedHeroPredictionScenario.Describe(skill);
        var config = BattleStartConfig.CreateDefault();
        config.Players = config.Players.Take(1).ToList();
        var caster = config.Players[0];
        caster.HeroId = caster.AttributeTemplateId = scenario.Hero;
        var loadout = PublishedHeroPredictionScenario.Loadout(scenario.Hero);
        caster.BasicAttackSkillId = loadout[0];
        caster.SkillIds = loadout.ToList();
        caster.PositionX = caster.PositionZ = 100;
        caster.EnableBrainOnSpawn = false;

        using var battle = new ConsoleBattleBootstrapper(config, additionalModules: [new MobaTraceAdapterModule()]);
        battle.Initialize();
        battle.Start();
        for (var step = 0; step < 8 && battle.Context.EcsWorld == null; step++) battle.Tick(Delta);
        battle.SetupBattle();
        for (var step = 0; step < 10; step++) battle.Tick(Delta);
        var world = GetBootstrapField<IWorld>(battle, "_runtimeWorld");
        var worlds = GetBootstrapField<WorldManager>(battle, "_runtimeWorldManager");
        var player = new PlayerId(caster.PlayerId);
        Assert.True(world.Services.Resolve<MobaPlayerActorMapService>().TryGetActorId(player, out var casterId));
        var setup = new LiveSimSetupActionExecutor(battle);
        var targets = scenario.Targets.Select((position, index) =>
        {
            var alias = PublishedHeroPredictionScenario.TargetPlayerName(index);
            setup.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = alias, playerId = alias, heroId = 1001,
                attributeTemplateId = 1001, teamId = 2,
                position = new MobaAcceptanceVector3Expectation { x = position.X, z = position.Z }
            });
            Assert.True(setup.TryGetActorId(alias, out var id));
            setup.DisableActorBrain(id);
            if (skill == "VeigarE") setup.SetScenarioActorAttribute(id, "MOVE_SPEED", PublishedHeroPredictionScenario.SweptCrossingFixtureMoveSpeed);
            setup.SetScenarioActorAttribute(id, "MAGIC_DEFENSE", 0);
            setup.SetScenarioActorAttribute(id, "PER_SECOND_BLOOD_R", 0);
            return id;
        }).ToArray();
        battle.Tick(Delta);
        PublishedHeroPredictionScenario.Run(skill, world, worlds, player, casterId, targets, Delta, output.WriteLine);
    }

    private static T GetBootstrapField<T>(ConsoleBattleBootstrapper battle, string name) =>
        (T)typeof(ConsoleBattleBootstrapper).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
}
