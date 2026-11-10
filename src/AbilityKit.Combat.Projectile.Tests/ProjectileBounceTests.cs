using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Projectile.Tests;

public sealed class ProjectileBounceTests
{
    private const int Layer = 1;

    [Fact]
    public void Bounce_MaximumHitCountOfOneIncludesTheInitialTarget()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var provider = new StubBounceProvider(first, 101, second, 202);
        var world = new ProjectileWorld(collision);
        world.SetBounceTargetProvider(provider);
        var p = new ProjectileSpawnParams(1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1f, 0f, 0f), 10f,
            0, 0, 0, 60, 100, 1 << Layer, ColliderId.Invalid,
            hitPolicyKind: ProjectileHitPolicyKind.Bounce, hitPolicyParam: 1);
        world.Spawn(p);
        var hits = new List<ProjectileHitEvent>();
        var exits = new List<ProjectileExitEvent>();

        world.Tick(1, 1f, hits, exits, null);

        Assert.Single(hits);
        Assert.Equal(first, hits[0].HitCollider);
        Assert.Single(exits);
        Assert.Equal(ProjectileExitReason.Hit, exits[0].Reason);
        Assert.Empty(provider.SelectedHitHistory);
    }

    [Fact]
    public void Bounce_RetargetsAndExitsAtConfiguredDistinctHitCount()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var provider = new StubBounceProvider(first, 101, second, 202);
        var world = new ProjectileWorld(collision);
        world.SetBounceTargetProvider(provider);
        var p = new ProjectileSpawnParams(1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1f, 0f, 0f), 10f,
            0, 0, 0, 60, 100, 1 << Layer, ColliderId.Invalid,
            hitPolicyKind: ProjectileHitPolicyKind.Bounce, hitPolicyParam: 2);
        world.Spawn(p);
        var hits = new List<ProjectileHitEvent>();
        var exits = new List<ProjectileExitEvent>();

        world.Tick(1, 1f, hits, exits, null);

        Assert.Equal(2, hits.Count);
        Assert.Equal(first, hits[0].HitCollider);
        Assert.Equal(second, hits[1].HitCollider);
        Assert.Single(exits);
        Assert.Equal(ProjectileExitReason.Hit, exits[0].Reason);
        // The selector is called only when another bounce is needed.  The
        // second hit reaches the configured limit and exits immediately.
        Assert.Equal(new[] { 101 }, provider.SelectedHitHistory);
    }

    [Fact]
    public void Bounce_ExcludesAlreadyHitActorsWhenProviderOffersOnlyThoseActors()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var world = new ProjectileWorld(collision);
        var provider = new StubBounceProvider(first, 101, ColliderId.Invalid, 0) { OnlyReturnAlreadyHit = true };
        world.SetBounceTargetProvider(provider);
        var p = new ProjectileSpawnParams(1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1f, 0f, 0f), 10f,
            0, 0, 0, 60, 100, 1 << Layer, ColliderId.Invalid,
            hitPolicyKind: ProjectileHitPolicyKind.Bounce, hitPolicyParam: 3);
        world.Spawn(p);
        var exits = new List<ProjectileExitEvent>();

        world.Tick(1, 1f, null, exits, null);

        Assert.Single(exits);
        Assert.Equal(ProjectileExitReason.Hit, exits[0].Reason);
    }

    [Fact]
    public void Bounce_EndsWhenNoUnhitTargetIsInsideConfiguredSearchRadius()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var world = new ProjectileWorld(collision);
        world.SetBounceTargetProvider(new StubBounceProvider(first, 101, second, 202));
        var p = new ProjectileSpawnParams(1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1f, 0f, 0f), 10f,
            0, 0, 0, 60, 100, 1 << Layer, ColliderId.Invalid,
            hitPolicyKind: ProjectileHitPolicyKind.Bounce, hitPolicyParam: 2, bounceSearchRadius: 1f);
        world.Spawn(p);
        var exits = new List<ProjectileExitEvent>();

        world.Tick(1, 1f, null, exits, null);

        Assert.Single(exits);
        Assert.Equal(ProjectileExitReason.Hit, exits[0].Reason);
    }

    [Fact]
    public void Bounce_SnapshotRestoreKeepsProviderAndDeterministicRetarget()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var provider = new StubBounceProvider(first, 101, second, 202);
        var world = new ProjectileWorld(collision);
        world.SetBounceTargetProvider(provider);
        var p = new ProjectileSpawnParams(1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1f, 0f, 0f), 10f,
            0, 0, 0, 60, 100, 1 << Layer, ColliderId.Invalid,
            hitPolicyKind: ProjectileHitPolicyKind.Bounce, hitPolicyParam: 2);
        var id = world.Spawn(p);
        world.Tick(1, .15f, null, null, null);
        var snapshot = world.ExportRollback(default);
        world.Tick(2, .3f, null, null, null);
        world.ImportRollback(default, snapshot);
        var hits = new List<ProjectileHitEvent>();
        var exits = new List<ProjectileExitEvent>();
        world.Tick(2, .3f, hits, exits, null);

        Assert.Equal(2, hits.Count);
        Assert.Equal(first, hits[0].HitCollider);
        Assert.Equal(second, hits[1].HitCollider);
        Assert.Single(exits);
        Assert.Equal(id, exits[0].Projectile);
    }

    [Fact]
    public void Bounce_TrackingArcStopsAtHitAndStartsANewArcTowardRetarget()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var provider = new StubBounceProvider(first, 101, second, 202);
        var world = new ProjectileWorld(collision);
        world.SetBounceTargetProvider(provider);
        var p = new ProjectileSpawnParams(1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1f, 0f, 0f), 2f,
            0, 0, 0, 60, 100, 1 << Layer, ColliderId.Invalid,
            hitPolicyKind: ProjectileHitPolicyKind.Bounce, hitPolicyParam: 2,
            trackingTargetActorId: 101).WithTrackingArc(1f, 1f);
        var id = world.Spawn(p);
        var hits = new List<ProjectileHitEvent>();

        // The first segment hits during its arc. The retarget is selected immediately,
        // but movement on the second arc begins on the following simulation tick.
        world.Tick(1, 1f, hits, null, null);

        Assert.Single(hits);
        Assert.True(world.TryGetFixedKinematics(id, out var stoppedAtHit, out _));
        Assert.Equal(hits[0].Point.X.ToFixed().RawValue, stoppedAtHit.X.RawValue);
        Assert.Equal(hits[0].Point.Y.ToFixed().RawValue, stoppedAtHit.Y.RawValue);
        Assert.Equal(hits[0].Point.Z.ToFixed().RawValue, stoppedAtHit.Z.RawValue);
        Assert.Equal(new[] { 101 }, provider.SelectedHitHistory);

        world.Tick(2, .01f, null, null, null);

        Assert.True(world.TryGetFixedKinematics(id, out var movingTowardNextTarget, out _));
        Assert.True(movingTowardNextTarget.X > stoppedAtHit.X);
        Assert.True(movingTowardNextTarget.Y > stoppedAtHit.Y);
    }

    [Fact]
    public void Bounce_DoesNotHitSecondColliderOwnedByAlreadyHitActor()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var duplicate = collision.Add(new Transform3(new Vec3(2.5f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var provider = new StubBounceProvider(first, 101, second, 202);
        provider.AddCollider(duplicate, 101, new Vec3(2.5f, 0f, 0f));
        var world = new ProjectileWorld(collision);
        world.SetBounceTargetProvider(provider);
        var p = new ProjectileSpawnParams(1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1f, 0f, 0f), 10f,
            0, 0, 0, 60, 100, 1 << Layer, ColliderId.Invalid,
            hitPolicyKind: ProjectileHitPolicyKind.Bounce, hitPolicyParam: 2);
        world.Spawn(p);
        var hits = new List<ProjectileHitEvent>();

        world.Tick(1, 1f, hits, null, null);

        Assert.Equal(2, hits.Count);
        Assert.Equal(first, hits[0].HitCollider);
        Assert.Equal(second, hits[1].HitCollider);
        Assert.DoesNotContain(hits, hit => hit.HitCollider.Equals(duplicate));
    }

    [Fact]
    public void Bounce_SnapshotRestoreOnSecondArcPreservesTrajectoryAndHitHistory()
    {
        var collision = new NaiveCollisionWorld();
        var first = collision.Add(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var second = collision.Add(new Transform3(new Vec3(4f, 0f, 0f), Quat.Identity, new Vec3(1f, 1f, 1f)),
            ColliderShape.CreateSphere(Vec3.Zero, .25f), Layer);
        var provider = new StubBounceProvider(first, 101, second, 202);
        var world = new ProjectileWorld(collision);
        world.SetBounceTargetProvider(provider);
        var p = new ProjectileSpawnParams(1, 100, 1, 1, 0, Vec3.Zero, new Vec3(1f, 0f, 0f), 2f,
            0, 0, 0, 60, 100, 1 << Layer, ColliderId.Invalid,
            hitPolicyKind: ProjectileHitPolicyKind.Bounce, hitPolicyParam: 2,
            trackingTargetActorId: 101).WithTrackingArc(1f, 1f);
        var id = world.Spawn(p);
        var firstTickHits = new List<ProjectileHitEvent>();
        world.Tick(1, 1f, firstTickHits, null, null);
        Assert.Single(firstTickHits);

        // Capture after the first hit has selected the next target and initialized its arc.
        world.Tick(2, .1f, null, null, null);
        var snapshot = world.ExportRollback(default);
        world.Tick(3, .2f, null, null, null);
        Assert.True(world.TryGetFixedKinematics(id, out var expectedPosition, out _));

        world.ImportRollback(default, snapshot);
        world.Tick(3, .2f, null, null, null);

        Assert.True(world.TryGetFixedKinematics(id, out var replayedPosition, out _));
        Assert.Equal(expectedPosition.X.RawValue, replayedPosition.X.RawValue);
        Assert.Equal(expectedPosition.Y.RawValue, replayedPosition.Y.RawValue);
        Assert.Equal(expectedPosition.Z.RawValue, replayedPosition.Z.RawValue);
        Assert.Equal(new[] { 101 }, provider.SelectedHitHistory);
    }

    private sealed class StubBounceProvider : IProjectileBounceTargetProvider
    {
        private readonly Dictionary<int, (int actor, Vec3 position)> _actors = new();
        public bool OnlyReturnAlreadyHit { get; set; }
        public List<int> SelectedHitHistory { get; } = new();

        public StubBounceProvider(ColliderId firstCollider, int firstActor, ColliderId secondCollider, int secondActor)
        {
            if (firstCollider.Value > 0) _actors[firstCollider.Value] = (firstActor, new Vec3(2f, 0f, 0f));
            if (secondCollider.Value > 0) _actors[secondCollider.Value] = (secondActor, new Vec3(4f, 0f, 0f));
        }

        public void AddCollider(ColliderId collider, int actorId, Vec3 position)
        {
            _actors[collider.Value] = (actorId, position);
        }

        public bool TryResolveActorId(ColliderId collider, out int actorId)
        {
            if (_actors.TryGetValue(collider.Value, out var value)) { actorId = value.actor; return true; }
            actorId = 0; return false;
        }

        public bool TrySelectNextTarget(int ownerId, int hitActorId, in Vec3 projectilePosition, IReadOnlyList<int> hitActorIds,
            float searchRadius,
            out int targetActorId, out Vec3 targetPosition)
        {
            SelectedHitHistory.Add(hitActorId);
            foreach (var pair in _actors)
            {
                if (OnlyReturnAlreadyHit || hitActorIds.Contains(pair.Value.actor)) continue;
                if (searchRadius > 0f && (pair.Value.position - projectilePosition).SqrMagnitude > searchRadius * searchRadius) continue;
                targetActorId = pair.Value.actor;
                targetPosition = pair.Value.position;
                return true;
            }
            targetActorId = 0; targetPosition = default; return false;
        }

        public bool TryGetTrackingTargetPosition(int targetActorId, out Vec3 position)
        {
            foreach (var pair in _actors)
                if (pair.Value.actor == targetActorId) { position = pair.Value.position; return true; }
            position = default; return false;
        }

        public void Dispose() { }
    }
}
