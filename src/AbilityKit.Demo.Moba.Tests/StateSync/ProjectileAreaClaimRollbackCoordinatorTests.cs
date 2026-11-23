using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class ProjectileAreaClaimRollbackCoordinatorTests
{
    [Fact]
    public void Cancelling_failed_area_spawn_suppresses_only_its_pending_notification()
    {
        using var collisions = new CollisionService();
        using var projectiles = new ProjectileService(collisions);
        var parameters = new AreaSpawnParams(7, Vec3.Zero, 3, 10, -1, 0);
        var existing = projectiles.SpawnArea(in parameters, frame: 0);
        var failed = projectiles.SpawnArea(in parameters, frame: 0);
        Assert.True(projectiles.CancelArea(failed));
        Assert.True(projectiles.IsAreaActive(existing));
        Assert.False(projectiles.IsAreaActive(failed));

        var spawns = new List<AreaSpawnEvent>();
        var expires = new List<AreaExpireEvent>();
        projectiles.DrainAreaSpawnEvents(spawns);
        projectiles.DrainAreaExpireEvents(expires);
        Assert.Equal(existing.Value, Assert.Single(spawns).Area.Value);
        Assert.Empty(expires);
    }

    [Fact]
    public void Coordinator_restore_reinstates_expired_area_group_claims()
    {
        using var collisions = new CollisionService();
        using var projectiles = new ProjectileService(collisions);
        var rootContextId = 1001L;
        var center = Vec3.Zero;
        var parameters = new AreaSpawnParams(
            ownerId: 7,
            center: in center,
            radius: 3,
            lifetimeFrames: 1,
            collisionLayerMask: -1,
            stayIntervalFrames: 0,
            rootContextId: rootContextId,
            groupKey: 9,
            claimTargetsWithinGroup: true);
        var firstArea = projectiles.SpawnArea(in parameters, frame: 0);
        var siblingArea = projectiles.SpawnArea(in parameters, frame: 0);

        Assert.True(projectiles.TryAcceptAreaEnter(firstArea, targetActorId: 42));

        var providers = new RollbackRegistry();
        providers.Register(new ProjectileRollbackProvider(projectiles));
        var coordinator = new RollbackCoordinator(providers, new RollbackSnapshotRingBuffer(2));
        var checkpoint = coordinator.Capture(new FrameIndex(0));

        Assert.True(projectiles.TryAcceptAreaEnter(siblingArea, targetActorId: 43));
        projectiles.Tick(frame: 0, fixedDeltaSeconds: 0f);
        projectiles.Tick(frame: 1, fixedDeltaSeconds: 0f);
        var expired = new List<AreaExpireEvent>();
        projectiles.DrainAreaExpireEvents(expired);
        Assert.Equal(2, expired.Count);
        Assert.False(projectiles.TryAcceptAreaEnter(siblingArea, targetActorId: 44));

        coordinator.Restore(checkpoint);

        Assert.False(projectiles.TryAcceptAreaEnter(siblingArea, targetActorId: 42),
            "The claim captured at frame 0 must remain shared by the restored sibling areas.");
        Assert.True(projectiles.TryAcceptAreaEnter(siblingArea, targetActorId: 43),
            "A claim made after frame 0 must be rolled back, leaving that target available.");
        Assert.True(projectiles.TryAcceptAreaEnter(firstArea, targetActorId: 44));
    }
}
