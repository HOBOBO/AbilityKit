using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using Xunit;

namespace AbilityKit.World.FrameSync.Tests;

public sealed class RollbackRequiredStateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Missing_or_duplicate_required_state_rejects_before_any_import(int count)
    {
        var registry = new RollbackRegistry();
        var provider = new Probe();
        registry.Register(provider);
        registry.RequireState(provider.Key);
        registry.RequireState(provider.Key);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var entries = new WorldRollbackSnapshotEntry[count];
        for (var i = 0; i < count; i++) entries[i] = new WorldRollbackSnapshotEntry(provider.Key, new byte[] { 7 });
        var snapshot = new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion, new FrameIndex(3), entries);
        Assert.False(coordinator.TryRestore(in snapshot, out var result));
        Assert.Equal(provider.Key, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(0, provider.ImportCount);
        Assert.Equal(0, provider.PreflightCount);
        snapshot = new WorldRollbackSnapshot(snapshot.Version, snapshot.Frame,
            new[] { new WorldRollbackSnapshotEntry(provider.Key, new byte[] { 7 }) });
        Assert.True(coordinator.TryRestore(in snapshot, out _));
        Assert.Equal(1, provider.ImportCount);
        Assert.Equal(1, provider.PreflightCount);
    }

    [Fact]
    public void Requirement_lifecycle_rejects_unregistered_and_sealed_changes_and_clears()
    {
        var registry = new RollbackRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.RequireState(7));
        registry.Register(new Probe());
        registry.RequireState(7);
        registry.Seal();
        Assert.Throws<InvalidOperationException>(() => registry.RequireState(7));
        registry.Clear();
        Assert.Empty(registry.RequiredStateKeys);
        registry.Register(new Probe());
        registry.RequireState(7);
        Assert.Single(registry.RequiredStateKeys);
    }

    [Fact]
    public void Registry_without_requirements_keeps_partial_snapshot_contract()
    {
        var registry = new RollbackRegistry();
        var provider = new Probe();
        registry.Register(provider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion, new FrameIndex(3),
            Array.Empty<WorldRollbackSnapshotEntry>());
        Assert.True(coordinator.TryRestore(in snapshot, out _));
        Assert.Equal(0, provider.ImportCount);
    }

    private sealed class Probe : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public int Key => 7;
        public int ImportCount;
        public int PreflightCount;
        public byte[] Export(FrameIndex frame) => new byte[] { 7 };
        public void Import(FrameIndex frame, byte[] payload) => ImportCount++;
        public void ValidateImport(FrameIndex frame, byte[] payload) => PreflightCount++;
    }
}
