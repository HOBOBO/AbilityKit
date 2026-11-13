using System;
using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class ProjectileHitIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pierce_counts_distinct_actors_in_distance_order_and_restores_history(bool grid)
    {
        ICollisionWorld collision = grid ? new GridCollisionWorld(2f) : new NaiveCollisionWorld();
        var provider = new IdentityProvider();
        // Register in reverse spatial order so Collider ID order cannot satisfy the test.
        var third = Add(collision, provider, 6, 303);
        var second = Add(collision, provider, 4, 202);
        var duplicate = Add(collision, provider, 2.5f, 101);
        var first = Add(collision, provider, 2, 101);
        var world = new ProjectileWorld(collision);
        world.SetActorIdentityProvider(provider);
        var spawn = Spawn(ProjectileHitIdentityPolicy.OncePerActor);
        world.Spawn(spawn);
        var hits = new List<ProjectileHitEvent>();
        world.Tick(1, .21f, hits, null, null);
        Assert.Equal(first, Assert.Single(hits).HitCollider);
        var snapshot = world.ExportRollback(default);

        var exits = new List<ProjectileExitEvent>();
        hits.Clear();
        world.Tick(2, .6f, hits, exits, null);
        Assert.Equal(second, Assert.Single(hits).HitCollider);
        Assert.Equal(2, hits[0].HitCount);
        Assert.Single(exits);
        Assert.Equal(0, world.ActiveCount);
        var end = world.ExportRollback(default);

        // Restore into a different world: actor identities are injected, history is serialized.
        var restored = new ProjectileWorld(collision);
        restored.SetActorIdentityProvider(provider);
        restored.ImportRollback(default, snapshot);
        hits.Clear();
        exits.Clear();
        restored.Tick(2, .6f, hits, exits, null);
        Assert.Equal(second, Assert.Single(hits).HitCollider);
        Assert.DoesNotContain(hits, h => h.HitCollider == duplicate || h.HitCollider == third);
        Assert.Single(exits);
        Assert.Equal(end, restored.ExportRollback(default));
    }

    [Fact]
    public void One_step_sweep_accepts_two_distinct_actors_before_spending_the_budget()
    {
        var collision = new NaiveCollisionWorld();
        var provider = new IdentityProvider();
        var third = Add(collision, provider, 6, 303);
        var second = Add(collision, provider, 4, 202);
        Add(collision, provider, 2.5f, 101);
        var first = Add(collision, provider, 2, 101);
        var world = new ProjectileWorld(collision);
        world.SetActorIdentityProvider(provider);
        world.Spawn(Spawn(ProjectileHitIdentityPolicy.OncePerActor));
        var hits = new List<ProjectileHitEvent>();
        world.Tick(1, 1f, hits, null, null);
        Assert.Equal(new[] { first, second }, hits.ConvertAll(h => h.HitCollider));
        Assert.DoesNotContain(hits, h => h.HitCollider == third);
        Assert.Equal(0, world.ActiveCount);
    }

    [Fact]
    public void Default_policy_preserves_collider_hit_budget()
    {
        var collision = new NaiveCollisionWorld();
        var provider = new IdentityProvider();
        var first = Add(collision, provider, 2, 101);
        var duplicate = Add(collision, provider, 2.5f, 101);
        Add(collision, provider, 4, 202);
        var world = new ProjectileWorld(collision);
        world.SetActorIdentityProvider(provider);
        world.Spawn(Spawn(ProjectileHitIdentityPolicy.Collider));
        var hits = new List<ProjectileHitEvent>();
        world.Tick(1, 1f, hits, null, null);
        Assert.Equal(new[] { first, duplicate }, hits.ConvertAll(h => h.HitCollider));
    }

    [Fact]
    public void Identity_policy_survives_every_spawn_copy_and_requires_provider()
    {
        var p = Spawn(ProjectileHitIdentityPolicy.OncePerActor)
            .WithDirection(Vec3.Forward).WithPosition(Vec3.One).WithSpeed(20)
            .WithLifetimeFrames(90).WithHitPolicyParam(3).WithPatternSlot(1, 3)
            .WithSpawnFrame(12).WithTrackingArc(1).WithTrackingArcHeightPerUnit(.5f);
        Assert.Equal(ProjectileHitIdentityPolicy.OncePerActor, p.HitIdentityPolicy);
        var world = new ProjectileWorld(new NaiveCollisionWorld());
        Assert.Throws<InvalidOperationException>(() => world.Spawn(p));
        Assert.Equal(0, world.ActiveCount);
    }

    private static ProjectileSpawnParams Spawn(ProjectileHitIdentityPolicy policy) => new(
        1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1, 0, 0), 10,
        0, 0, 0, 90, 100, 1 << 1, ColliderId.Invalid,
        hitPolicyKind: ProjectileHitPolicyKind.Pierce, hitPolicyParam: 2,
        hitsRemaining: 0, hitIdentityPolicy: policy);

    private static ColliderId Add(ICollisionWorld world, IdentityProvider provider, float x, int actor)
    {
        var id = world.Add(new Transform3(new Vec3(x, 0, 0), Quat.Identity, Vec3.One),
            ColliderShape.CreateSphere(Vec3.Zero, .1f), 1);
        provider.Actors[id.Value] = actor;
        return id;
    }

    private sealed class IdentityProvider : IProjectileActorIdentityProvider
    {
        public readonly Dictionary<int, int> Actors = new();
        public bool TryResolveActorId(ColliderId collider, out int actorId) => Actors.TryGetValue(collider.Value, out actorId);
        public void Dispose() { }
    }
}
