using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Game.Test.UnitTest;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// 使用已发布七英雄配置和正式施法入口验证数值；预期值是项目基线，
/// 不将配置正确或测试通过表述为完整复刻及实战平衡已经完成。
/// </summary>
public sealed class SevenHeroNumericBaselineTests
{
    [Theory]
    [InlineData(3001, 655, 107, 61, 4, 300, 50)]
    [InlineData(3002, 610, 105, 59, 3, 260, 50)]
    [InlineData(3003, 560, 96, 50, 3, 418, 25)]
    [InlineData(3004, 580, 108, 52, 3, 490, 26)]
    [InlineData(3005, 640, 115, 64, 4, 400, 47)]
    [InlineData(3006, 620, 110, 46, 3, 467, 31)]
    [InlineData(3007, 580, 99, 54, 3, 440, 24)]
    public void Published_attributes_apply_the_linear_baseline_at_levels_one_nine_and_eighteen(
        int hero, int hp, int hpGrowth, int ad, int adGrowth, int mana, int manaGrowth)
    {
        using var battle = new Battle(hero);
        var configs = battle.Services.Resolve<MobaConfigDatabase>();
        Assert.True(configs.TryGetAttributeTemplate(hero, out var template));
        foreach (var level in new[] { 1, 9, 18 })
        {
            var prepared = new MobaActorAttributeInitializer().Prepare(template, level);
            Assert.Equal(hp + hpGrowth * (level - 1), prepared.Group.GetValue(MobaAttributeIds.MAX_HP));
            Assert.Equal(hp + hpGrowth * (level - 1), prepared.Group.GetValue(MobaAttributeIds.HP));
            Assert.Equal(mana + manaGrowth * (level - 1), prepared.Group.GetValue(MobaAttributeIds.MAX_MANA));
            Assert.Equal(ad + adGrowth * (level - 1), prepared.Group.GetValue(MobaAttributeIds.PHYSICS_ATTACK));
            Assert.Equal(0, prepared.Group.GetValue(MobaAttributeIds.MAGIC_ATTACK));
            Assert.InRange(prepared.Group.GetValue(MobaAttributeIds.MOVE_SPEED), 3.2f, 3.5f);
        }
    }

    [Theory]
    [InlineData(3001, 5)]
    [InlineData(3002, 5)]
    [InlineData(3003, 5)]
    [InlineData(3004, 5)]
    [InlineData(3005, 5)]
    [InlineData(3006, 5)]
    [InlineData(3007, 5)]
    public void Basic_attacks_do_one_total_ad_without_demonstration_flat_damage(int hero, int slot)
    {
        using var battle = new Battle(hero, hero == 3005 ? 1.5f : 3);
        battle.Set(battle.Caster, "physics_attack", 100);
        battle.Set(battle.Caster, "magic_attack", 275);
        var before = battle.Setup.GetActorHp(battle.Target);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, default, Vec3.Forward, battle.Target);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(2500);
        Assert.Equal(100f, before - battle.Setup.GetActorHp(battle.Target), .05f);
    }

    [Theory]
    [InlineData(3001, 1, 1, 40, 7000)]
    [InlineData(3001, 1, 5, 60, 5000)]
    [InlineData(3001, 2, 1, 50, 12000)]
    [InlineData(3001, 2, 5, 70, 12000)]
    [InlineData(3001, 3, 1, 30, 18000)]
    [InlineData(3001, 3, 5, 30, 14000)]
    [InlineData(3001, 4, 1, 100, 120000)]
    [InlineData(3001, 4, 3, 100, 90000)]
    [InlineData(3002, 3, 1, 90, 24000)]
    [InlineData(3002, 3, 5, 90, 10000)]
    [InlineData(3002, 4, 1, 100, 85000)]
    [InlineData(3002, 4, 3, 100, 45000)]
    [InlineData(3004, 3, 1, 70, 20000)]
    [InlineData(3004, 3, 5, 90, 14000)]
    public void Actual_commit_consumes_rank_mana_once_and_starts_rank_cooldown(
        int hero, int slot, int rank, float mana, int cooldownMs)
    {
        using var battle = new Battle(hero);
        var runtime = battle.Actor(battle.Caster).skillLoadout.ActiveSkills[slot - 1];
        runtime.Level = rank;
        var before = battle.Setup.GetActorMana(battle.Caster);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 3), Vec3.Forward, battle.Target);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(2000);
        Assert.Equal(mana, before - battle.Setup.GetActorMana(battle.Caster), .05f);
        Assert.Equal(cooldownMs, runtime.CooldownDurationMs);
    }

    [Theory]
    [InlineData(3001, 1, 1, 0, 0, 45)]
    [InlineData(3001, 1, 3, 100, 0, 154)]
    [InlineData(3001, 1, 5, 200, 0, 293)]
    [InlineData(3001, 2, 1, 0, 0, 60)]
    [InlineData(3001, 2, 3, 100, 0, 180)]
    [InlineData(3001, 2, 5, 200, 0, 300)]
    [InlineData(3001, 3, 1, 0, 0, 80)]
    [InlineData(3001, 3, 3, 100, 0, 310)]
    [InlineData(3001, 3, 5, 200, 0, 540)]
    [InlineData(3002, 3, 1, 0, 0, 70)]
    [InlineData(3002, 3, 3, 0, 100, 270)]
    [InlineData(3002, 3, 5, 0, 200, 470)]
    public void Published_hits_use_rank_base_and_the_configured_attribute(
        int hero, int slot, int rank, float ad, float ap, float expected)
    {
        using var battle = new Battle(hero);
        battle.Actor(battle.Caster).skillLoadout.ActiveSkills[slot - 1].Level = rank;
        battle.Set(battle.Caster, "physics_attack", ad);
        battle.Set(battle.Caster, "magic_attack", ap);
        var before = battle.Setup.GetActorHp(battle.Target);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 3), Vec3.Forward, battle.Target);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(6500);
        Assert.Equal(expected, before - battle.Setup.GetActorHp(battle.Target), .1f);
    }

    [Theory]
    [InlineData(3003, 3, 1, 0, 100)]
    [InlineData(3003, 3, 3, 100, 190)]
    [InlineData(3003, 3, 5, 200, 280)]
    [InlineData(3007, 2, 1, 0, 40)]
    [InlineData(3007, 2, 3, 100, 110)]
    [InlineData(3007, 2, 5, 200, 180)]
    public void Shields_use_rank_base_plus_ap_on_the_first_application(
        int hero, int slot, int rank, float ap, float expected)
    {
        using var battle = new Battle(hero);
        battle.Actor(battle.Caster).skillLoadout.ActiveSkills[slot - 1].Level = rank;
        battle.Set(battle.Caster, "magic_attack", ap);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 8), Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(600);
        Assert.Equal(expected, battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster), .05f);
    }

    [Theory]
    [InlineData(940, 995)]
    [InlineData(990, 1000)]
    public void Gragas_passive_heals_max_hp_fraction_and_clamps_overhealing(float before, float expected)
    {
        using var battle = new Battle(3005);
        battle.Set(battle.Caster, "max_hp", 1000);
        battle.Set(battle.Caster, "hp", before);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 2, default, Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(2000);
        Assert.Equal(expected, battle.Setup.GetActorHp(battle.Caster), .05f);
    }

    private sealed class Battle : IDisposable
    {
        private readonly ConsoleBattleBootstrapper _bootstrapper;
        public LiveSimSetupActionExecutor Setup { get; }
        public int Caster { get; }
        public int Target { get; }
        public IWorldResolver Services => _bootstrapper.RuntimeServices!;
        public SkillCastCoordinator Skills => Services.Resolve<SkillCastCoordinator>();

        public Battle(int hero, float targetZ = 3)
        {
            var config = BattleStartConfig.CreateDefault();
            foreach (var player in config.Players) player.EnableBrainOnSpawn = false;
            _bootstrapper = new ConsoleBattleBootstrapper(config);
            _bootstrapper.Initialize();
            _bootstrapper.Start();
            for (var i = 0; i < 8 && _bootstrapper.Context.EcsWorld == null; i++) _bootstrapper.Tick();
            _bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++) _bootstrapper.Tick();
            Setup = new LiveSimSetupActionExecutor(_bootstrapper);
            Caster = Spawn("caster", hero, 1, 0);
            Target = Spawn("target", 3007, 2, targetZ);
            Set(Target, "max_hp", 10000);
            Set(Target, "hp", 10000);
            Set(Target, "physics_defense", 0);
            Set(Target, "magic_defense", 0);
            Set(Caster, "max_mana", 1000);
            Set(Caster, "mana", 1000);
            Setup.Tick(2);
        }

        private int Spawn(string alias, int hero, int team, float z)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = alias, playerId = "numeric_" + alias,
                heroId = hero, attributeTemplateId = hero, teamId = team,
                position = new MobaAcceptanceVector3Expectation { z = z }
            });
            Assert.True(Setup.TryGetActorId(alias, out var actor));
            Setup.DisableActorBrain(actor);
            return actor;
        }

        public ActorEntity Actor(int id)
        {
            Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(id, out var actor));
            return actor;
        }

        public void Set(int actor, string name, float value) => Setup.SetScenarioActorAttribute(actor, name, value);
        public void Dispose() => _bootstrapper.Dispose();
    }
}
