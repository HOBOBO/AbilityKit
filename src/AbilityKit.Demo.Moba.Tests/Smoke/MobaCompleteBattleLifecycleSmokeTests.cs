using System.Linq;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaCompleteBattleLifecycleSmokeTests
{
    [Fact]
    public void Damage_context_lifecycle_is_independent_from_optional_trace()
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(
            BattleStartConfig.CreateDefault(),
            additionalModules: new[] { new MobaTraceAdapterModule() });
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

        var services = bootstrapper.RuntimeServices!;
        var actors = services.Resolve<MobaActorRegistry>();
        var combatants = actors.Entries
            .Where(entry => entry.Value != null && entry.Value.hasTeam && entry.Value.hasAttributeGroup)
            .ToArray();
        var attacker = combatants.First();
        var target = combatants.First(entry => entry.Value.team.Value != attacker.Value.team.Value);
        var damage = services.Resolve<DamagePipelineService>();
        var contexts = services.Resolve<MobaExecutionContextRegistry>();
        var trace = services.Resolve<MobaTraceRegistry>();

        trace.OnDeinit(services);
        ContextLifecycleEventHandler<MobaExecutionContextNode> failingObserver =
            (in ContextLifecycleEvent<MobaExecutionContextNode> _) => throw new InvalidOperationException("observer failure");
        using var failingSubscription = contexts.Observe(failingObserver, replayExisting: false);
        var withoutTraceStart = contexts.NextContextId;
        var withoutTrace = ExecuteDamage(damage, attacker.Key, target.Key, 1f);
        failingSubscription.Dispose();

        Assert.NotNull(withoutTrace);
        AssertDamageContextChain(contexts, withoutTraceStart, expectPeriodic: true);
        Assert.False(trace.Contains(withoutTraceStart));

        trace.OnInit(services);
        trace.RegistryEvent += _ => throw new InvalidOperationException("trace observer failure");
        var withTraceStart = contexts.NextContextId;
        var withTrace = ExecuteDamage(damage, attacker.Key, target.Key, 1f);

        Assert.NotNull(withTrace);
        Assert.True(withoutTrace.Value > 0f);
        Assert.True(withTrace.Value > 0f);
        AssertDamageContextChain(contexts, withTraceStart, expectPeriodic: true);
        Assert.True(trace.Contains(withTraceStart));
        Assert.True(trace.Contains(withTraceStart + 1));
        Assert.True(trace.Contains(withTraceStart + 2));
    }

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
        Assert.False(services.TryResolve<MobaTraceRegistry>(out _));
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
            CritType = CritType.None,
            ReasonKind = DamageReasonKind.Environment,
        };
        attack.BaseDamage.BaseValue = targetHp * 10f + 1f;
        return damage.Execute(attack);
    }

    private static DamageResult ExecuteDamage(
        DamagePipelineService damage,
        int attackerActorId,
        int targetActorId,
        float value)
    {
        var attack = new AttackInfo
        {
            AttackerActorId = attackerActorId,
            TargetActorId = targetActorId,
            DamageType = DamageType.Physical,
            CritType = CritType.None,
            ReasonKind = DamageReasonKind.Environment,
            CombatFlags = MobaCombatExecutionFlags.PeriodicDamage,
        };
        attack.BaseDamage.BaseValue = value;
        return damage.Execute(attack);
    }

    private static void AssertDamageContextChain(
        MobaExecutionContextRegistry contexts,
        long startContextId,
        bool expectPeriodic)
    {
        Assert.True(contexts.TryGet(startContextId, out var attack));
        Assert.True(contexts.TryGet(startContextId + 1, out var calc));
        Assert.True(contexts.TryGet(startContextId + 2, out var apply));
        Assert.Equal(MobaExecutionKind.DamageAttack, attack.Kind);
        Assert.Equal(MobaExecutionKind.DamageCalc, calc.Kind);
        Assert.Equal(MobaExecutionKind.DamageApply, apply.Kind);
        Assert.Equal(attack.ContextId, calc.ParentContextId);
        Assert.Equal(calc.ContextId, apply.ParentContextId);
        Assert.True(attack.IsEnded);
        Assert.True(calc.IsEnded);
        Assert.True(apply.IsEnded);
        Assert.Equal((int)MobaExecutionEndReason.Completed, attack.EndReason);
        Assert.Equal((int)MobaExecutionEndReason.Completed, calc.EndReason);
        Assert.Equal((int)MobaExecutionEndReason.Completed, apply.EndReason);
        Assert.Equal(expectPeriodic, attack.CombatFacts.IsPeriodicDamage);
        Assert.Equal(attack.CombatFacts, calc.CombatFacts);
        Assert.Equal(calc.CombatFacts, apply.CombatFacts);
    }
}
