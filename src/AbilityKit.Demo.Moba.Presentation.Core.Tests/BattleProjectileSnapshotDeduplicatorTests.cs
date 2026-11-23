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
    public void ExitReleasesActiveKeysButSuppressesRepeatedBatchAndTrigger()
    {
        var spawn = Create(ProjectilePresentationEventKind.Spawn, projectileId: 7, actorId: 100);
        var hit = Create(ProjectilePresentationEventKind.Hit, projectileId: 7, actorId: 100);
        var exit = Create(ProjectilePresentationEventKind.Exit, projectileId: 7, actorId: 100);
        var deduplicator = new BattleProjectileSnapshotDeduplicator();
        deduplicator.ShouldHandle(in spawn);
        deduplicator.ShouldHandle(in hit);

        var key = BattleProjectileSnapshotKey.From(in hit);
        deduplicator.RecordHitVisual(7, in key);
        deduplicator.CompleteLifecycle(in exit);
        deduplicator.CompleteLifecycle(in exit);

        Assert.Equal(0, deduplicator.Count);
        Assert.False(deduplicator.ShouldHandle(in spawn));
        Assert.False(deduplicator.ShouldHandle(in hit));
        Assert.True(deduplicator.HasHitVisual(7, in key));

        deduplicator.Clear();
        Assert.False(deduplicator.HasHitVisual(7, in key));
        Assert.True(deduplicator.ShouldHandle(in spawn));
        Assert.True(deduplicator.ShouldHandle(in hit));
    }

    [Fact]
    public void CompletedWindowIsBoundedAndAllowsAFirstTriggerVisualAfterExit()
    {
        var deduplicator = new BattleProjectileSnapshotDeduplicator();
        for (var id = 1; id <= 1025; id++)
        {
            var exit = Create(ProjectilePresentationEventKind.Exit, projectileId: id, actorId: id);
            var hit = Create(ProjectilePresentationEventKind.Hit, projectileId: id, actorId: id);
            var key = BattleProjectileSnapshotKey.From(in hit);
            deduplicator.CompleteLifecycle(in exit);
            Assert.False(deduplicator.HasHitVisual(id, in key));
            deduplicator.RecordHitVisual(id, in key);
        }

        Assert.Equal(0, deduplicator.Count);
        var oldestHit = Create(ProjectilePresentationEventKind.Hit, projectileId: 1, actorId: 1);
        var latestHit = Create(ProjectilePresentationEventKind.Hit, projectileId: 1025, actorId: 1025);
        var oldestKey = BattleProjectileSnapshotKey.From(in oldestHit);
        var latestKey = BattleProjectileSnapshotKey.From(in latestHit);
        Assert.False(deduplicator.HasHitVisual(1, in oldestKey));
        Assert.True(deduplicator.HasHitVisual(1025, in latestKey));
        Assert.True(deduplicator.ShouldHandle(in oldestHit));
        Assert.False(deduplicator.ShouldHandle(in latestHit));
    }

    [Fact]
    public void DistinctHitsAreNotDuplicateAndOnlySuccessfulPlaybackSuppressesTheOtherSource()
    {
        var deduplicator = new BattleProjectileSnapshotDeduplicator();
        var first = Create(ProjectilePresentationEventKind.Hit, 7, 100, collider: 6, frame: 22);
        var second = Create(ProjectilePresentationEventKind.Hit, 7, 100, collider: 7, frame: 22);
        var repeatedContact = Create(ProjectilePresentationEventKind.Hit, 7, 100, collider: 6, frame: 29);
        var key = BattleProjectileSnapshotKey.From(in first);
        var localKey = BattleProjectileSnapshotKey.FromHit(7, 3, 4, 6, 22, 1f, 2f, 3f);
        Assert.Equal(key, localKey);
        Assert.True(deduplicator.ShouldHandle(in first, snapshotFrame: 30));
        Assert.False(deduplicator.ShouldHandle(in first, snapshotFrame: 31));
        Assert.False(deduplicator.HasHitVisual(7, in localKey));
        deduplicator.RecordHitVisual(7, in key);
        Assert.True(deduplicator.HasHitVisual(7, in localKey));
        Assert.True(deduplicator.ShouldHandle(in second));
        Assert.True(deduplicator.ShouldHandle(in repeatedContact));

        var triggerFirst = Create(ProjectilePresentationEventKind.Hit, 7, 100, x: 2f, collider: 7, frame: 29);
        var triggerKey = BattleProjectileSnapshotKey.FromHit(7, 3, 4, 7, 29, 2f, 2f, 3f);
        deduplicator.RecordHitVisual(7, in triggerKey);
        Assert.False(deduplicator.ShouldHandle(in triggerFirst));
    }

    private static ProjectileEventData Create(
        ProjectilePresentationEventKind kind,
        int projectileId,
        int actorId,
        float x = 1f,
        int collider = 6,
        int frame = 0)
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
            hitCollider: collider,
            exitReason: 7,
            projectileId,
            forwardX: 0f,
            forwardY: 0f,
            forwardZ: 1f,
            snapshotFrame: frame);
    }
}
