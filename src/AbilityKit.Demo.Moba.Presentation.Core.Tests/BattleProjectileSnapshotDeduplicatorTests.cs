using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class BattleProjectileSnapshotDeduplicatorTests
{
    [Fact]
    public void ProjectileIdTakesPriorityOverActorId()
    {
        var first = Create(ProjectilePresentationEventKind.Spawn, projectileId: 7, actorId: 100);
        var sameLogicalProjectile = Create(ProjectilePresentationEventKind.Spawn, projectileId: 7, actorId: 200);
        var deduplicator = new BattleProjectileSnapshotDeduplicator();

        Assert.True(deduplicator.ShouldHandle(in first));
        Assert.False(deduplicator.ShouldHandle(in sameLogicalProjectile));
    }

    [Fact]
    public void ActorIdIsUsedWhenProjectileIdIsMissing()
    {
        var first = Create(ProjectilePresentationEventKind.Hit, projectileId: 0, actorId: 100);
        var duplicate = Create(ProjectilePresentationEventKind.Hit, projectileId: 0, actorId: 100);
        var deduplicator = new BattleProjectileSnapshotDeduplicator();

        Assert.True(deduplicator.ShouldHandle(in first));
        Assert.False(deduplicator.ShouldHandle(in duplicate));
    }

    [Fact]
    public void IdentityFreeEventsUseQuantizedEventFields()
    {
        var first = Create(ProjectilePresentationEventKind.Hit, projectileId: 0, actorId: 0, x: 1.0001f);
        var sameQuantizedPosition = Create(ProjectilePresentationEventKind.Hit, projectileId: 0, actorId: 0, x: 1.0002f);
        var differentPosition = Create(ProjectilePresentationEventKind.Hit, projectileId: 0, actorId: 0, x: 1.002f);
        var deduplicator = new BattleProjectileSnapshotDeduplicator();

        Assert.True(deduplicator.ShouldHandle(in first));
        Assert.False(deduplicator.ShouldHandle(in sameQuantizedPosition));
        Assert.True(deduplicator.ShouldHandle(in differentPosition));
    }

    [Fact]
    public void ExitReleasesSpawnAndHitLifecycleKeys()
    {
        var spawn = Create(ProjectilePresentationEventKind.Spawn, projectileId: 7, actorId: 100);
        var hit = Create(ProjectilePresentationEventKind.Hit, projectileId: 7, actorId: 100);
        var exit = Create(ProjectilePresentationEventKind.Exit, projectileId: 7, actorId: 100);
        var deduplicator = new BattleProjectileSnapshotDeduplicator();
        deduplicator.ShouldHandle(in spawn);
        deduplicator.ShouldHandle(in hit);

        deduplicator.ForgetLifecycle(in exit);

        Assert.Equal(0, deduplicator.Count);
        Assert.True(deduplicator.ShouldHandle(in spawn));
        Assert.True(deduplicator.ShouldHandle(in hit));
    }

    private static ProjectileEventData Create(
        ProjectilePresentationEventKind kind,
        int projectileId,
        int actorId,
        float x = 1f)
    {
        return new ProjectileEventData(
            kind,
            actorId,
            ownerActorId: 2,
            templateId: 3,
            launcherActorId: 4,
            rootActorId: 5,
            x,
            y: 2f,
            z: 3f,
            hitCollider: 6,
            exitReason: 7,
            projectileId,
            forwardX: 0f,
            forwardY: 0f,
            forwardZ: 1f);
    }
}
