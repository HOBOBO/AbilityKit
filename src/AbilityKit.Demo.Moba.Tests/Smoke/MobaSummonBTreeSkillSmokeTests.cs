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
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Ability.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaSummonBTreeSkillSmokeTests
{
    private const int SummonId = 1;
    private const int BrainId = 1;

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(.6f, .8f)]
    [InlineData(-.8f, .6f)]
    public void Airship_preserves_launch_heading_through_turning_and_brain_recreation(float x, float z)
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var summons = services.Resolve<MobaSummonService>();
        var brains = services.Resolve<MobaBrainService>();
        var root = registry.Entries.First(entry => entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var start = root.Value.transform.Value.Position + new Vec3(-4, 0, -6);
        var heading = new Vec3(x, 0, z);
        var source = default(SummonSourceContext);
        Assert.True(summons.TrySummon(root.Key, 1008, in start, in heading, in source, out var id));
        Assert.True(registry.TryGet(id, out var actor));
        Assert.Equal(101, actor.actorBrain.BrainId);
        for (var i = 0; i < 3; i++) battle.Tick();
        Assert.True(brains.TryGetBehavior(actor.actorBrain.BehaviorInstanceId, out var behavior));
        var snapshot = Assert.IsAssignableFrom<IBehaviorRuntimeSnapshot>(behavior.Decision);
        var payload = snapshot.CaptureSnapshot();
        var before = actor.transform.Value.Position;
        Assert.True(Vec3.Dot(before - start, heading) > .05f, "Initial movement must follow the launch heading.");
        var sideways = new Vec3(-z, 0, x);
        actor.ReplaceTransform(new Transform3(before, Quat.LookRotation(sideways, Vec3.Up), actor.transform.Value.Scale));
        var oldBrain = actor.actorBrain;
        Assert.True(brains.ActivateBrain(actor, oldBrain.BrainId, oldBrain.SourceKind, oldBrain.SourceId));
        Assert.True(brains.TryRestoreBehaviorSnapshot(actor, snapshot.SnapshotType, payload));
        for (var i = 0; i < 6; i++) battle.Tick();
        var displacement = actor.transform.Value.Position - before;
        Assert.True(Vec3.Dot(displacement, heading) > .05f, $"Expected movement along launch heading, got {displacement}.");
        Assert.True(System.Math.Abs(Vec3.Dot(displacement, sideways)) < .03f, $"Facing changed the restored launch heading: {displacement}.");
    }

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

        // Summon creation runs after BrainTick in the skill pipeline. The
        // runtime binding must still be complete in this creation frame.
        Assert.True(registry.TryGet(summonActorId, out var created) && created != null);
        Assert.True(created.hasActorBrain);
        Assert.Equal(BrainId, created.actorBrain.BrainId);
        Assert.True(created.actorBrain.BehaviorInstanceId > 0);
        Assert.True(brains.TryGetBehavior(created.actorBrain.BehaviorInstanceId, out var createdBehavior));
        Assert.Equal("MobaBTree", createdBehavior.Decision.DecisionType);

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

    [Fact]
    public void Nested_summons_inherit_root_owner_and_root_death_cleans_all_policy_enabled_summons()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var summons = services.Resolve<MobaSummonService>();
        var root = registry.Entries.First(entry => entry.Value != null &&
            entry.Value.hasTeam && entry.Value.hasTransform);
        var forward = Vec3.Forward;
        var sourceContext = default(SummonSourceContext);

        var firstPosition = root.Value.transform.Value.Position + new Vec3(2f, 0f, 0f);
        Assert.True(summons.TrySummon(root.Key, SummonId, in firstPosition, in forward,
            in sourceContext, out var firstId));
        Assert.True(registry.TryGet(firstId, out var first) && first != null);

        var secondPosition = first.transform.Value.Position + new Vec3(2f, 0f, 0f);
        Assert.True(summons.TrySummon(firstId, SummonId, in secondPosition, in forward,
            in sourceContext, out var secondId));
        Assert.True(registry.TryGet(secondId, out var second) && second != null);
        Assert.True(second.hasOwnerLink);
        Assert.Equal(firstId, second.ownerLink.OwnerActorId);
        Assert.Equal(root.Key, second.ownerLink.RootOwnerActorId);

        Assert.Equal(2, summons.GetAliveCount(root.Key, SummonId));
        Assert.Equal(2, summons.DespawnOnRootOwnerDeath(root.Key));
        Assert.True(first.hasActorDespawnRequest);
        Assert.Equal(ActorDespawnReason.SummonOwnerDead, first.actorDespawnRequest.Reason);
        Assert.True(second.hasActorDespawnRequest);
        Assert.Equal(ActorDespawnReason.SummonOwnerDead, second.actorDespawnRequest.Reason);

        for (var i = 0; i < 8 && (registry.TryGet(firstId, out _) || registry.TryGet(secondId, out _)); i++)
        {
            battle.Tick();
        }

        Assert.False(registry.TryGet(firstId, out _));
        Assert.False(registry.TryGet(secondId, out _));
        Assert.Equal(0, summons.GetAliveCount(root.Key, SummonId));
    }

    [Fact]
    public void Root_owner_capacity_replacement_is_deferred_but_keeps_one_live_summon_after_cleanup()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var config = services.Resolve<MobaConfigDatabase>();
        var registry = services.Resolve<MobaActorRegistry>();
        var summons = services.Resolve<MobaSummonService>();
        const int limitedSummonId = 1008;
        Assert.True(config.TryGetSummon(limitedSummonId, out var limited));
        Assert.Equal(1, limited.MaxAlivePerOwner);

        var root = registry.Entries.First(entry => entry.Value != null &&
            entry.Value.hasTeam && entry.Value.hasTransform);
        var forward = Vec3.Forward;
        var sourceContext = default(SummonSourceContext);
        var firstPosition = root.Value.transform.Value.Position + new Vec3(2f, 0f, 0f);
        Assert.True(summons.TrySummon(root.Key, limitedSummonId, in firstPosition, in forward,
            in sourceContext, out var firstId));
        var secondPosition = root.Value.transform.Value.Position + new Vec3(3f, 0f, 0f);
        Assert.True(summons.TrySummon(root.Key, limitedSummonId, in secondPosition, in forward,
            in sourceContext, out var secondId));

        Assert.True(registry.TryGet(firstId, out var first) && first != null);
        Assert.True(first.hasActorDespawnRequest);
        Assert.Equal(ActorDespawnReason.SummonReplacedByLimit, first.actorDespawnRequest.Reason);
        Assert.True(registry.TryGet(secondId, out _));

        for (var i = 0; i < 8 && registry.TryGet(firstId, out _); i++)
        {
            battle.Tick();
        }

        Assert.False(registry.TryGet(firstId, out _));
        Assert.True(registry.TryGet(secondId, out _));
        Assert.Equal(1, summons.GetAliveCount(root.Key, limitedSummonId));
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
