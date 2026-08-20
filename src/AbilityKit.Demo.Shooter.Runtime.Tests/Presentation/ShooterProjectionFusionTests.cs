using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Presentation;

public sealed class ShooterProjectionFusionTests
{
    [Fact]
    public void AlignedEntityAndTransformChangesPreserveProjectionResults()
    {
        var first = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 1);
        var second = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2);
        var batch = CreateBatch(
            new[]
            {
                new ShooterViewEntityChange(first, 0, alive: true),
                new ShooterViewEntityChange(second, 0, alive: true)
            },
            new[]
            {
                Transform(first, 10f),
                Transform(second, 20f)
            });
        var projection = new ShooterSnapshotViewProjection();

        var result = projection.Apply(in batch);

        Assert.Equal(2, result.AddedEntities);
        Assert.Equal(2, result.ComponentUpdates);
        Assert.True(projection.Store.TryGetTransform(first, out var firstTransform));
        Assert.True(projection.Store.TryGetTransform(second, out var secondTransform));
        Assert.Equal(10f, firstTransform.X);
        Assert.Equal(20f, secondTransform.X);
    }

    [Fact]
    public void MisalignedTransformChangesUseFallbackWithoutDroppingUpdates()
    {
        var first = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 1);
        var second = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2);
        var batch = CreateBatch(
            new[]
            {
                new ShooterViewEntityChange(first, 0, alive: true),
                new ShooterViewEntityChange(second, 0, alive: true)
            },
            new[]
            {
                Transform(second, 20f),
                Transform(first, 10f)
            });
        var projection = new ShooterSnapshotViewProjection();

        var result = projection.Apply(in batch);

        Assert.Equal(2, result.AddedEntities);
        Assert.Equal(2, result.ComponentUpdates);
        Assert.True(projection.Store.TryGetTransform(first, out var firstTransform));
        Assert.True(projection.Store.TryGetTransform(second, out var secondTransform));
        Assert.Equal(10f, firstTransform.X);
        Assert.Equal(20f, secondTransform.X);
    }

    [Fact]
    public void DeadPlayerWithTransformRetainsExistingRecoverySemantics()
    {
        var player = new ShooterViewEntityKey(ShooterViewEntityKind.Player, 7);
        var projection = new ShooterSnapshotViewProjection();
        var initial = CreateBatch(
            new[] { new ShooterViewEntityChange(player, 0, alive: true) },
            new[] { Transform(player, 1f) });
        projection.Apply(in initial);
        var recovery = CreateBatch(
            new[] { new ShooterViewEntityChange(player, 0, alive: false) },
            new[] { Transform(player, 3f) });

        var result = projection.Apply(in recovery);

        Assert.Equal(1, result.DeadEntityRemovals);
        Assert.Equal(1, result.AddedEntities);
        Assert.True(projection.Store.ContainsEntity(player));
        Assert.True(projection.Store.TryGetTransform(player, out var transform));
        Assert.Equal(3f, transform.X);
    }

    private static ShooterSnapshotViewBatch CreateBatch(
        IReadOnlyList<ShooterViewEntityChange> entities,
        IReadOnlyList<ShooterViewTransformComponentChange> transforms)
    {
        return new ShooterSnapshotViewBatch(
            1ul,
            1,
            1ul,
            ShooterViewSnapshotKind.Delta,
            ShooterViewBatchSource.AuthoritativeCorrection,
            entities,
            Array.Empty<ShooterViewEntityKey>(),
            transforms,
            Array.Empty<ShooterViewHealthComponentChange>(),
            Array.Empty<ShooterViewScoreComponentChange>(),
            Array.Empty<ShooterViewProjectileLifetimeComponentChange>(),
            Array.Empty<ShooterEventSnapshot>());
    }

    private static ShooterViewTransformComponentChange Transform(ShooterViewEntityKey key, float x)
    {
        return new ShooterViewTransformComponentChange(key, x, 0f, 1f, 0f, 0f, 0f);
    }
}
