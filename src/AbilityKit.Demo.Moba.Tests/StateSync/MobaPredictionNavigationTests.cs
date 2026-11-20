using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Services.Navigation;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaSkillLifecycleRollbackPreparationTests
{
    [Fact]
    public void Prediction_navigation_freezes_the_world_queried_by_path_following()
    {
        var service = Get<MobaNavigationService>();
        var queries = Get<INavigationService>();
        Assert.Same(service, queries);
        var normal = Assert.IsType<NavigationWorld>(queries.World);
        Assert.False(MobaRollbackRegistryBuilder.Create(_world).TryGet(MobaPredictionNavigationRollbackProvider.DefaultKey, out _));
        service.Build();
        normal = Assert.IsType<NavigationWorld>(queries.World);

        var start = normal.Grid.CellCenter(12, 18);
        var target = normal.Grid.CellCenter(28, 24);
        var expected = queries.World.FindPath(in start, in target, 0f, out var expectedPath);
        Assert.True(expectedPath.Length > 1);
        var registry = MobaRollbackRegistryBuilder.CreateForPrediction(_world);
        var frozen = Assert.IsType<MobaNavigationService.FrozenNavigationWorld>(queries.World);
        Assert.NotSame(normal.Grid, frozen.Grid);
        Assert.True(registry.TryGet(MobaPredictionNavigationRollbackProvider.DefaultKey, out var provider));
        var payload = provider.Export(_frame);
        normal.Options.MaxIterations = 1;
        normal.Options.AllowDiagonal = !normal.Options.AllowDiagonal;
        normal.Options.SimplifyPath = !normal.Options.SimplifyPath;
        Assert.Throws<InvalidOperationException>(service.Build);
        Assert.Same(frozen, queries.World);
        Assert.Equal(expected, queries.World.FindPath(in start, in target, 0f, out var replayPath));
        Assert.Equal(expectedPath.Waypoints, replayPath.Waypoints);
        Assert.Equal(payload, provider.Export(_frame));
    }

    [Fact]
    public void Prediction_navigation_allows_initial_bake_but_not_restoring_pre_bake_inputs()
    {
        using var service = new MobaNavigationService(Get<ICollisionService>(), Get<IMobaMapRuntimeService>());
        service.EnablePredictionNavigation();
        var provider = new MobaPredictionNavigationRollbackProvider(service);
        var pending = provider.Export(_frame);
        Assert.Null(service.World);
        provider.ValidateImport(_frame, pending);

        service.Build();
        Assert.IsType<MobaNavigationService.FrozenNavigationWorld>(service.World);
        Assert.Throws<InvalidOperationException>(() => provider.Import(_frame, pending));
        Assert.Throws<InvalidOperationException>(service.Build);
        var built = provider.Export(_frame);
        provider.Import(_frame, built);

        using var equivalent = new MobaNavigationService(Get<ICollisionService>(), Get<IMobaMapRuntimeService>());
        equivalent.Build();
        equivalent.EnablePredictionNavigation();
        new MobaPredictionNavigationRollbackProvider(equivalent).ValidateImport(_frame, built);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Prediction_navigation_missing_or_changed_payload_rejects_before_world_import(bool missing)
    {
        const int key = MobaPredictionNavigationRollbackProvider.DefaultKey;
        var coordinator = new RollbackCoordinator(MobaRollbackRegistryBuilder.CreateForPrediction(_world), new RollbackSnapshotRingBuffer(2));
        var target = coordinator.Capture(_frame);
        Assert.True(coordinator.TryRestore(target, out var valid), valid.ToString());
        var entries = missing ? target.Entries.Where(entry => entry.Key != key).ToArray() :
            target.Entries.Select(entry => entry.Key == key
                ? new WorldRollbackSnapshotEntry(key, entry.Payload.Append((byte)0).ToArray()) : entry).ToArray();
        var before = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame));
        Assert.False(coordinator.TryRestore(new WorldRollbackSnapshot(target.Version, target.Frame, entries), out var result));
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(key, result.ProviderKey);
        Assert.Equal(before, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(_frame)));
    }
}
