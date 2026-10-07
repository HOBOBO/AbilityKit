using System.Linq;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaSummonBTreeSkillSmokeTests
{
    private const int SummonId = 1;
    private const int BrainId = 1;

    [Fact]
    public void Configured_summon_activates_declared_btree_brain()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var config = services.Resolve<MobaConfigDatabase>();
        var registry = services.Resolve<MobaActorRegistry>();
        var summons = services.Resolve<MobaSummonService>();
        var brains = services.Resolve<MobaBrainService>();
        var brainCatalog = services.Resolve<IMobaActorBrainCatalog>();

        Assert.True(config.TryGetSummon(SummonId, out var summonConfig));
        Assert.Equal(BrainId, summonConfig.BrainId);
        Assert.True(brainCatalog.TryGet(BrainId, out var brainDefinition));
        Assert.Equal(MobaBrainDriverKeys.BehaviorTree, brainDefinition.DriverKind);
        Assert.Equal("summon_warden_bt", brainDefinition.DecisionName);

        var caster = registry.Entries.First(entry =>
            entry.Value != null &&
            entry.Value.hasTeam &&
            entry.Value.hasTransform);
        var spawnPosition = caster.Value.transform.Value.Position + new Vec3(2f, 0f, 0f);
        var forward = Vec3.Forward;
        var sourceContext = default(SummonSourceContext);

        Assert.True(summons.TrySummon(
            caster.Key,
            SummonId,
            in spawnPosition,
            in forward,
            in sourceContext,
            out var summonActorId));

        for (var i = 0; i < 8; i++)
        {
            battle.Tick();
        }

        Assert.True(registry.TryGet(summonActorId, out var summoned) && summoned != null);
        Assert.True(summoned.hasActorBrain);
        Assert.Equal(BrainId, summoned.actorBrain.BrainId);
        Assert.True(summoned.actorBrain.BehaviorInstanceId > 0);
        Assert.True(brains.TryGetBehavior(summoned.actorBrain.BehaviorInstanceId, out var behavior));
        Assert.NotNull(behavior);
        Assert.Equal("MobaBTree", behavior.Decision.DecisionType);
    }

    [Fact]
    public void Published_summon_inheritance_changes_spawned_actor_attribute()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var config = services.Resolve<MobaConfigDatabase>();
        var registry = services.Resolve<MobaActorRegistry>();
        var summons = services.Resolve<MobaSummonService>();
        Assert.True(config.TryGetSummon(SummonId, out var summonConfig));
        Assert.Equal(1, summonConfig.InheritAttributeConfigId);

        var caster = registry.Entries.First(entry => entry.Value != null &&
            entry.Value.hasTeam && entry.Value.hasTransform && entry.Value.hasAttributeGroup);
        var attribute = MobaAttributeIds.Get((BattleAttributeType)4);
        caster.Value.attributeGroup.Group.SetBase(attribute, 200f);
        var spawnPosition = caster.Value.transform.Value.Position + new Vec3(2f, 0f, 0f);
        var forward = Vec3.Forward;
        var sourceContext = default(SummonSourceContext);
        Assert.True(summons.TrySummon(caster.Key, SummonId, in spawnPosition, in forward,
            in sourceContext, out var summonActorId));
        Assert.True(registry.TryGet(summonActorId, out var summoned));
        Assert.Equal(55f, summoned.attributeGroup.Group.GetValue(attribute));
    }

    [Fact]
    public void Published_showcase_buff_projects_selected_projectile_speed()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var buffs = services.Resolve<MobaBuffService>();
        var modifiers = services.Resolve<MobaSkillParamModifierService>();
        var caster = registry.Entries.First(entry => entry.Value != null &&
            entry.Value.hasTeam && entry.Value.hasTransform);
        var matching = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            baseSkillId: 10020101, projectileTemplateId: 30020101);
        var other = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            baseSkillId: 10020101, projectileTemplateId: 30020102);

        Assert.True(buffs.ApplyBuffImmediate(caster.Key, 102, caster.Key, 5000));
        Assert.Equal(12.5f, modifiers.Projectile.ResolveSpeed(caster.Key, 10f, in matching));
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(caster.Key, 10f, in other));
    }

    [Fact]
    public void Summon_despawn_runs_common_actor_state_cleanup()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var summons = services.Resolve<MobaSummonService>();
        var buffs = services.Resolve<MobaBuffService>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null &&
            entry.Value.hasTeam &&
            entry.Value.hasTransform);
        var spawnPosition = caster.Value.transform.Value.Position + new Vec3(2f, 0f, 0f);
        var forward = Vec3.Forward;
        var sourceContext = default(SummonSourceContext);

        Assert.True(summons.TrySummon(
            caster.Key,
            SummonId,
            in spawnPosition,
            in forward,
            in sourceContext,
            out var summonActorId));
        Assert.True(registry.TryGet(summonActorId, out var summoned) && summoned != null);
        Assert.True(buffs.ApplyBuffImmediate(summonActorId, 1, caster.Key, 3000));
        Assert.True(summoned.hasBuffs);
        Assert.NotEmpty(summoned.buffs.Active);

        Assert.True(summons.TryDespawn(summonActorId, SummonDespawnReason.ManualRemove));
        for (var i = 0; i < 8 && registry.TryGet(summonActorId, out _); i++)
        {
            battle.Tick();
        }

        Assert.False(registry.TryGet(summonActorId, out _));
        Assert.False(summoned.hasBuffs);
    }

    private static ConsoleBattleBootstrapper StartBattle()
    {
        var battle = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++)
        {
            battle.Tick();
        }

        battle.SetupBattle();
        for (var i = 0; i < 10; i++)
        {
            battle.Tick();
        }

        Assert.NotNull(battle.RuntimeServices);
        return battle;
    }
}
