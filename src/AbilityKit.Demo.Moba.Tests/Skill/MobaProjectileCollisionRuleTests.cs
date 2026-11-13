using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaProjectileCollisionRuleTests
{
    [Theory]
    [InlineData(UnitSubType.Hero, ProjectileCollisionResponse.Block)]
    [InlineData(UnitSubType.Minion, ProjectileCollisionResponse.Ignore)]
    [InlineData(UnitSubType.Neutral, ProjectileCollisionResponse.Hit)]
    [InlineData(UnitSubType.Boss, ProjectileCollisionResponse.Hit)]
    [InlineData(UnitSubType.Tower, ProjectileCollisionResponse.Block)]
    [InlineData(UnitSubType.Base, ProjectileCollisionResponse.Block)]
    public void Ordered_type_rules_choose_response_without_changing_target_identity(UnitSubType subtype, ProjectileCollisionResponse expected)
    {
        using var registry = new MobaActorRegistry();
        var contexts = new Contexts();
        Actor(contexts, registry, 1, 1, Team.Team1, EntityMainType.Unit, UnitSubType.Hero);
        Actor(contexts, registry, 2, 2, Team.Team2, EntityMainType.Unit, subtype);
        Actor(contexts, registry, 3, 3, Team.Team2, EntityMainType.Unit, UnitSubType.Hero);
        var rules = new[] {
            Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Block, subtype: UnitSubType.Hero),
            Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Ignore, subtype: UnitSubType.Minion),
            Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Block, subtype: UnitSubType.Tower),
            Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Block, subtype: UnitSubType.Base),
        };
        var filter = new MobaProjectileCollisionFilter(registry, trackingTargetActorId: 3, rules: rules);
        Assert.Equal(expected, filter.ResolveCollision(1, new ColliderId(2), 1));
        Assert.Equal(ProjectileCollisionResponse.Hit, filter.ResolveCollision(1, new ColliderId(3), 1));
        // Launch-time copy cannot change if the caller changes its rule array later.
        rules[0] = Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Ignore);
        Assert.Equal(expected, filter.ResolveCollision(1, new ColliderId(2), 2));
    }

    [Fact]
    public void Main_type_and_relation_select_interceptors_and_Hit_never_grants_friendly_fire()
    {
        using var registry = new MobaActorRegistry();
        var contexts = new Contexts();
        Actor(contexts, registry, 1, 1, Team.Team1, EntityMainType.Unit, UnitSubType.Hero);
        Actor(contexts, registry, 2, 2, Team.Team1, EntityMainType.Unit, UnitSubType.Hero);
        Actor(contexts, registry, 3, 3, Team.Team2, EntityMainType.Summon, UnitSubType.None);
        Actor(contexts, registry, 4, 4, Team.None, EntityMainType.Unit, UnitSubType.Neutral);
        var filter = new MobaProjectileCollisionFilter(registry, rules: new[] {
            Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Block, main: EntityMainType.Summon),
            Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Ignore, relation: ProjectileCollisionRelation.Neutral),
            Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Hit),
        });
        Assert.Equal(ProjectileCollisionResponse.Ignore, filter.ResolveCollision(1, new ColliderId(1), 1));
        Assert.Equal(ProjectileCollisionResponse.Ignore, filter.ResolveCollision(1, new ColliderId(2), 1));
        Assert.Equal(ProjectileCollisionResponse.Block, filter.ResolveCollision(1, new ColliderId(3), 1));
        Assert.Equal(ProjectileCollisionResponse.Ignore, filter.ResolveCollision(1, new ColliderId(4), 1));
        var allyBlock = new MobaProjectileCollisionFilter(registry, rules: new[] {
            Rule(ProjectileCollisionRole.OtherActor, ProjectileCollisionResponse.Block, relation: ProjectileCollisionRelation.Ally),
        });
        Assert.Equal(ProjectileCollisionResponse.Block, allyBlock.ResolveCollision(1, new ColliderId(2), 1));
    }

    [Theory]
    [InlineData(ProjectileCollisionResponse.Ignore, 1, 0)]
    [InlineData(ProjectileCollisionResponse.Block, 0, 0)]
    [InlineData(ProjectileCollisionResponse.Hit, 0, 1)]
    public void Responses_drive_real_projectile_lifecycle_and_restore_in_a_new_world(
        ProjectileCollisionResponse response, int active, int damageHits)
    {
        var collision = new NaiveCollisionWorld();
        var collider = collision.Add(new Transform3(new Vec3(2, 0, 0), Quat.Identity, Vec3.One),
            ColliderShape.CreateSphere(Vec3.Zero, .1f), 1);
        using var originalRegistry = new MobaActorRegistry();
        var contexts = new Contexts();
        Actor(contexts, originalRegistry, 11, 90, Team.Team1, EntityMainType.Unit, UnitSubType.Hero);
        Actor(contexts, originalRegistry, 22, collider.Value, Team.Team2, EntityMainType.Unit, UnitSubType.Hero);
        var filter = new MobaProjectileCollisionFilter(originalRegistry, trackingTargetActorId: 99, rules: new[] {
            Rule(ProjectileCollisionRole.OtherActor, response, subtype: UnitSubType.Hero),
        });
        var world = new ProjectileWorld(collision);
        world.SetHitFilterResolver(new MobaProjectileCollisionFilterResolver(originalRegistry));
        world.Spawn(new ProjectileSpawnParams(11, 70, 0, 11, 0, Vec3.Zero, new Vec3(1, 0, 0),
            10, 0, 0, 0, 90, 100, 1 << 1, ColliderId.Invalid, hitFilter: filter));
        var snapshot = world.ExportRollback(default);
        var hits = new List<ProjectileHitEvent>();
        var exits = new List<ProjectileExitEvent>();
        world.Tick(1, .4f, hits, exits, null);
        Assert.Equal(active, world.ActiveCount);
        Assert.Equal(damageHits, hits.Count);
        Assert.Equal(1 - active, exits.Count);
        var expected = world.ExportRollback(default);

        // The old registry deliberately changes; restored filters must bind the new world's registry.
        Assert.True(originalRegistry.TryGet(22, out var old));
        old.ReplaceTeam(Team.Team1);
        using var restoredRegistry = new MobaActorRegistry();
        var restoredContexts = new Contexts();
        Actor(restoredContexts, restoredRegistry, 11, 90, Team.Team1, EntityMainType.Unit, UnitSubType.Hero);
        Actor(restoredContexts, restoredRegistry, 22, collider.Value, Team.Team2, EntityMainType.Unit, UnitSubType.Hero);
        var restored = new ProjectileWorld(collision);
        restored.SetHitFilterResolver(new MobaProjectileCollisionFilterResolver(restoredRegistry));
        restored.ImportRollback(default, snapshot);
        hits.Clear(); exits.Clear();
        restored.Tick(1, .4f, hits, exits, null);
        Assert.Equal(damageHits, hits.Count);
        Assert.Equal(active, restored.ActiveCount);
        Assert.Equal(expected, restored.ExportRollback(default));
    }

    [Fact]
    public void World_rules_preserve_default_block_or_explicitly_ignore_without_damage()
    {
        using var registry = new MobaActorRegistry();
        Actor(new Contexts(), registry, 1, 1, Team.Team1, EntityMainType.Unit, UnitSubType.Hero);
        var legacy = new MobaProjectileCollisionFilter(registry);
        Assert.Equal(ProjectileCollisionResponse.Block, legacy.ResolveCollision(1, new ColliderId(999), 1));
        var pass = new MobaProjectileCollisionFilter(registry, rules: new[] {
            Rule(ProjectileCollisionRole.StaticWorld, ProjectileCollisionResponse.Ignore),
        });
        Assert.Equal(ProjectileCollisionResponse.Ignore, pass.ResolveCollision(1, new ColliderId(999), 1));
        Assert.False(Rule(ProjectileCollisionRole.StaticWorld, ProjectileCollisionResponse.Hit).TryValidate(out _));
        var invalid = new ProjectileMO(new ProjectileDTO { CollisionRules = new[] {
            new ProjectileCollisionRuleDTO { Role = (ProjectileCollisionRole)99 },
        }});
        Assert.False(invalid.TryResolveRuntimeSettings(out _, out _, out _, out _));
    }

    private static ProjectileCollisionRule Rule(ProjectileCollisionRole role, ProjectileCollisionResponse response,
        UnitSubType subtype = 0, EntityMainType main = 0, ProjectileCollisionRelation relation = 0) =>
        new(new ProjectileCollisionRuleDTO { Role = role, MainType = (int)main,
            UnitSubType = (int)subtype, Relation = relation, Response = (int)response });

    private static void Actor(Contexts contexts, MobaActorRegistry registry, int id, int collider,
        Team team, EntityMainType main, UnitSubType subtype)
    {
        var entity = contexts.actor.CreateEntity();
        entity.AddActorId(id);
        entity.AddTeam(team);
        entity.AddCollisionId(new ColliderId(collider));
        entity.AddEntityMainType(main);
        entity.AddUnitSubType(subtype);
        registry.Register(id, entity);
    }
}
