using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using System.Collections.Generic;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class ProjectileHitPolicyFactoryTests
{
    private const int CustomKind = 9107;

    [Fact]
    public void RegisterFactory_creates_project_policy_with_configured_parameter()
    {
        try
        {
            ProjectileHitPolicyFactory.RegisterFactory(CustomKind, param => new TestPolicy(param));

            var policy = Assert.IsType<TestPolicy>(ProjectileHitPolicyFactory.Create(CustomKind, 23));

            Assert.Equal(23, policy.Parameter);
        }
        finally
        {
            ProjectileHitPolicyFactory.UnregisterFactory(CustomKind);
        }
    }

    [Fact]
    public void Unknown_kind_fails_explicitly_and_builtin_pierce_remains_available()
    {
        var exception = Assert.Throws<System.InvalidOperationException>(
            () => ProjectileHitPolicyFactory.Create(9108, 0));
        Assert.Contains("kind 9108 is not registered", exception.Message);
        Assert.Same(ProjectileHitPolicyFactory.Create(ProjectileHitPolicyKind.Pierce, -1),
            ProjectileHitPolicyFactory.Create(ProjectileHitPolicyKind.Pierce, -1));
        Assert.NotSame(ExitOnHitPolicy.Instance, ProjectileHitPolicyFactory.Create(ProjectileHitPolicyKind.Pierce, 2));
    }

    [Fact]
    public void Registered_factory_must_return_a_policy()
    {
        const int nullFactoryKind = 9109;
        try
        {
            ProjectileHitPolicyFactory.RegisterFactory(nullFactoryKind, _ => null!);

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => ProjectileHitPolicyFactory.Create(nullFactoryKind, 0));

            Assert.Contains("returned null", exception.Message);
        }
        finally
        {
            ProjectileHitPolicyFactory.UnregisterFactory(nullFactoryKind);
        }
    }

    [Fact]
    public void Spawn_rejects_unknown_policy_before_consuming_runtime_identity()
    {
        var world = new ProjectileWorld(new NaiveCollisionWorld());
        var invalid = new ProjectileSpawnParams(1, 1, 1, 1, 0, Vec3.Zero, Vec3.Right, 1f,
            0, 0f, 0f, 10, 10f, 1, ColliderId.Invalid, hitPolicyKind: (ProjectileHitPolicyKind)9108);

        Assert.Throws<System.InvalidOperationException>(() => world.Spawn(invalid));
        Assert.Equal(0, world.ActiveCount);

        var valid = new ProjectileSpawnParams(1, 1, 1, 1, 0, Vec3.Zero, Vec3.Right, 1f,
            0, 0f, 0f, 10, 10f, 1, ColliderId.Invalid);
        Assert.Equal(1, world.Spawn(valid).Value);
    }

    [Fact]
    public void Rollback_restores_hit_filter_and_hit_count_state_in_same_world()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), 1);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), 1);
        var filter = new RejectColliderFilter(second);
        var world = new ProjectileWorld(collision);
        var spawn = new ProjectileSpawnParams(1, 1, 1, 1, 0, Vec3.Zero, Vec3.Right, 10f,
            0, 0f, 0f, 60, 100f, 1 << 1, ColliderId.Invalid,
            hitsRemaining: 2, hitPolicyKind: ProjectileHitPolicyKind.Pierce, hitPolicyParam: 2,
            hitFilter: filter, hitCooldownFrames: 7);
        world.Spawn(spawn);
        var snapshot = world.ExportRollback(default);

        var firstRunHits = new List<ProjectileHitEvent>();
        world.Tick(1, 1f, firstRunHits, null, null);
        Assert.Single(firstRunHits);
        Assert.Equal(first, firstRunHits[0].HitCollider);

        world.ImportRollback(default, snapshot);
        var replayHits = new List<ProjectileHitEvent>();
        world.Tick(1, 1f, replayHits, null, null);

        Assert.Single(replayHits);
        Assert.Equal(first, replayHits[0].HitCollider);
        Assert.Equal(1, replayHits[0].HitCount);
    }

    [Fact]
    public void Rollback_resolves_explicit_hit_filter_key_in_new_world()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), 1);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), 1);
        var filter = new RejectColliderFilter(second);
        var source = new ProjectileWorld(collision);
        var spawn = new ProjectileSpawnParams(1, 1, 1, 1, 0, Vec3.Zero, Vec3.Right, 10f,
            0, 0f, 0f, 60, 100f, 1 << 1, ColliderId.Invalid,
            hitsRemaining: 2, hitPolicyKind: ProjectileHitPolicyKind.Pierce, hitPolicyParam: 2,
            hitFilter: filter, hitFilterKey: 77);
        source.Spawn(spawn);
        var snapshot = source.ExportRollback(default);

        var restored = new ProjectileWorld(collision);
        restored.SetHitFilterResolver(new FixedFilterResolver(77, filter));
        restored.ImportRollback(default, snapshot);
        var hits = new List<ProjectileHitEvent>();
        restored.Tick(1, 1f, hits, null, null);

        Assert.Single(hits);
        Assert.Equal(first, hits[0].HitCollider);
    }

    private sealed class TestPolicy : IProjectileHitPolicy
    {
        public TestPolicy(int parameter) { Parameter = parameter; }

        public int Parameter { get; }

        public bool ShouldExitOnHit(in ProjectileHitEvent hit, ref int hitsRemaining) => false;
    }

    private sealed class RejectColliderFilter : IProjectileHitFilter
    {
        private readonly ColliderId _rejected;

        public RejectColliderFilter(ColliderId rejected) { _rejected = rejected; }

        public bool ShouldHit(int ownerId, ColliderId collider, int frame) => !collider.Equals(_rejected);
    }

    private sealed class FixedFilterResolver : IProjectileHitFilterResolver
    {
        private readonly int _key;
        private readonly IProjectileHitFilter _filter;

        public FixedFilterResolver(int key, IProjectileHitFilter filter)
        {
            _key = key;
            _filter = filter;
        }

        public IProjectileHitFilter Resolve(int key) => key == _key ? _filter : null!;
    }
}
