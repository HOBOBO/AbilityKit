using System.Linq;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaCompleteBattleLifecycleSmokeTests
{
    [Fact]
    public void ConsoleWorldCompletesDeathRespawnRedeathAndSettlement()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++)
        {
            bootstrapper.Tick();
        }

        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++)
        {
            bootstrapper.Tick();
        }

        var services = bootstrapper.RuntimeServices;
        Assert.NotNull(services);
        var registry = services.Resolve<MobaActorRegistry>();
        var combatants = registry.Entries
            .Where(entry => entry.Value != null && entry.Value.hasTeam && entry.Value.hasAttributeGroup && entry.Value.hasResourceContainer)
            .ToArray();
        var attacker = combatants.First();
        var target = combatants.First(entry => entry.Value.team.Value != attacker.Value.team.Value);

        var lifecycle = services.Resolve<MobaUnitLifecycleService>();
        var damage = services.Resolve<DamagePipelineService>();
        var rules = services.Resolve<MobaCombatRulesService>();
        var gameplay = services.Resolve<MobaGameplayService>();
        Assert.Equal(MobaGameplayPhase.Running, gameplay.Phase);

        var firstDeath = ExecuteLethalDamage(damage, attacker.Key, target.Key, target.Value.GetMobaAttrs().Hp);
        Assert.Equal(0f, firstDeath.TargetHp, 3);
        Assert.Equal(MobaCombatRuleFailure.Dead, rules.CanBeSearchedTarget(attacker.Key, target.Key).Failure);

        var firstRespawn = lifecycle.TryRespawn(target.Key, healthRatio: 0.5f);
        Assert.True(firstRespawn.Succeeded);
        Assert.True(rules.CanBeSearchedTarget(attacker.Key, target.Key).Passed);
        Assert.Equal(MobaUnitRespawnFailure.AlreadyAlive, lifecycle.TryRespawn(target.Key).Failure);

        var secondDeath = ExecuteLethalDamage(damage, attacker.Key, target.Key, firstRespawn.RestoredHp);
        Assert.Equal(0f, secondDeath.TargetHp, 3);
        Assert.True(lifecycle.TryRespawn(target.Key).Succeeded);

        Assert.True(gameplay.End("team_defeated", winTeamId: (int)attacker.Value.team.Value));
        Assert.Equal(MobaGameplayPhase.Ended, gameplay.Phase);
        Assert.Equal((int)attacker.Value.team.Value, gameplay.LastResult.WinTeamId);
    }

    private static DamageResult ExecuteLethalDamage(
        DamagePipelineService damage,
        int attackerActorId,
        int targetActorId,
        float targetHp)
    {
        var attack = new AttackInfo
        {
            AttackerActorId = attackerActorId,
            TargetActorId = targetActorId,
            DamageType = DamageType.Physical,
            ReasonKind = DamageReasonKind.Environment,
        };
        attack.BaseDamage.BaseValue = targetHp * 10f + 1f;
        return damage.Execute(attack);
    }
}
