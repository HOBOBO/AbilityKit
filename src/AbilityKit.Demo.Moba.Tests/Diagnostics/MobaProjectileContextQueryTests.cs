using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Context;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class MobaProjectileContextQueryTests
{
    private const float FixedDeltaSeconds = 0.1f;

    [Fact]
    public void Retired_projectile_is_not_live_and_each_hit_remains_addressable()
    {
        using var collisions = new CollisionService();
        using var projectiles = new ProjectileService(collisions);
        var firstTarget = AddTarget(collisions.World, 0.8f);
        var secondTarget = AddTarget(collisions.World, 2.8f);
        var snapshots = new SnapshotStorage();
        snapshots.RegisterType<HitFacts>("test.projectile.hit", 1, ContextSnapshotPurpose.Observation);
        var reader = new ContextValueResolver(new ContextRegistry(), snapshots);
        var hits = new List<ProjectileHitEvent>();

        var original = projectiles.Spawn(Spawn(0, 0f));
        var firstHit = TickAndReadHit(projectiles, hits, 1);
        Assert.Equal(original, firstHit.Projectile);
        Assert.Equal(firstTarget, firstHit.HitCollider);
        var firstReference = Capture(snapshots, firstHit);
        Assert.True(projectiles.TryGetRuntimeState(original, out var afterFirstHit));

        hits.Clear();
        projectiles.Tick(2, FixedDeltaSeconds);
        projectiles.DrainHitEvents(hits);
        Assert.Empty(hits);
        Assert.True(projectiles.TryGetRuntimeState(original, out var afterAdvance));
        Assert.True(afterAdvance.Position.X > afterFirstHit.Position.X);
        AssertHit(reader, firstReference, firstHit);

        var secondHit = TickAndReadHit(projectiles, hits, 3);
        Assert.Equal(original, secondHit.Projectile);
        Assert.Equal(secondTarget, secondHit.HitCollider);
        Assert.Equal(2, secondHit.HitCount);
        Assert.NotEqual(firstHit.Point, secondHit.Point);
        var secondReference = Capture(snapshots, secondHit);
        Assert.NotEqual(firstReference.SnapshotId, secondReference.SnapshotId);
        var exits = new List<ProjectileExitEvent>();
        projectiles.DrainExitEvents(exits);
        Assert.Equal(original, Assert.Single(exits).Projectile);
        Assert.False(projectiles.TryGetRuntimeState(original, out _));

        var replacement = projectiles.Spawn(Spawn(4, 2.2f));
        var replacementHit = TickAndReadHit(projectiles, hits, 4);
        Assert.Equal(replacement, replacementHit.Projectile);
        Assert.NotEqual(original, replacement);
        Assert.Equal(firstHit.TemplateId, replacementHit.TemplateId);
        var replacementReference = Capture(snapshots, replacementHit);
        Assert.True(projectiles.TryGetRuntimeState(replacement, out _));
        Assert.False(projectiles.TryGetRuntimeState(original, out _));
        AssertHit(reader, firstReference, firstHit);
        AssertHit(reader, secondReference, secondHit);
        AssertHit(reader, replacementReference, replacementHit);

        Assert.Equal(1, snapshots.PruneBeforeFrame(2));
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable,
            reader.ReadSnapshot<HitFacts, Vec3>(firstReference, "point").Status);
        AssertHit(reader, secondReference, secondHit);
        AssertHit(reader, replacementReference, replacementHit);
    }

    private static ColliderId AddTarget(ICollisionWorld world, float position) => world.Add(
        new Transform3(new Vec3(position, 0, 0), Quat.Identity, Vec3.One),
        ColliderShape.CreateSphere(Vec3.Zero, 0.1f), layerId: 1);

    private static ProjectileSpawnParams Spawn(int frame, float position) => new(
        ownerId: 100, templateId: 7, launcherActorId: 100, rootActorId: 100, spawnFrame: frame,
        position: new Vec3(position, 0, 0), direction: new Vec3(1, 0, 0), speed: 10,
        returnAfterFrames: 0, returnSpeed: 0, returnStopDistance: 0, lifetimeFrames: 90, maxDistance: 100,
        collisionLayerMask: 1 << 1, ignoreCollider: ColliderId.Invalid,
        hitsRemaining: 2, hitPolicyKind: ProjectileHitPolicyKind.Pierce, hitPolicyParam: 2);

    private static ProjectileHitEvent TickAndReadHit(IProjectileService projectiles, List<ProjectileHitEvent> hits, int frame)
    {
        hits.Clear();
        projectiles.Tick(frame, FixedDeltaSeconds);
        projectiles.DrainHitEvents(hits);
        return Assert.Single(hits);
    }

    private static ContextSnapshotReference Capture(SnapshotStorage snapshots, in ProjectileHitEvent hit) =>
        snapshots.SaveManaged(new HitFacts(hit), new ContextSnapshotCapture(1, hit.Frame, "hit"));

    private static void AssertHit(ContextValueResolver reader, in ContextSnapshotReference reference, in ProjectileHitEvent expected)
    {
        var point = reader.ReadSnapshot<HitFacts, Vec3>(reference, "point");
        Assert.Equal(ContextSnapshotQueryStatus.Found, point.Status);
        Assert.Equal(expected.Projectile.Value, point.Record.Reference.EntityId);
        Assert.Equal(expected.Frame, point.Record.Frame);
        Assert.Equal(expected.Point, point.Value);
        var target = reader.ReadSnapshot<HitFacts, ColliderId>(reference, "target");
        Assert.Equal(ContextSnapshotQueryStatus.Found, target.Status);
        Assert.Equal(expected.HitCollider, target.Value);
        Assert.Equal(ContextSnapshotQueryStatus.FieldMissing, reader.ReadSnapshot<HitFacts, int>(reference, "unknown").Status);
    }

    private sealed class HitFacts(in ProjectileHitEvent hit) : IImmutableContextSnapshot
    {
        private readonly Vec3 _point = hit.Point;
        private readonly ColliderId _target = hit.HitCollider;
        public long EntityId { get; } = hit.Projectile.Value;
        public long CreatedAtMs => 0;
        public long Version => 1;
        public int Frame { get; } = hit.Frame;

        public bool TryGetValue<T>(string key, out T value)
        {
            object? fact = key switch { "point" => _point, "target" => _target, _ => null };
            if (fact is T typed)
            {
                value = typed;
                return true;
            }
            value = default!;
            return false;
        }
    }
}
