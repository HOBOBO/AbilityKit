using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using Xunit;

namespace AbilityKit.World.FrameSync.Tests;

public sealed class RollbackSnapshotPreflightTests
{
    [Fact]
    public void Provider_view_keeps_snapshot_order_independent_of_registration_map_and_mutation()
    {
        var first = new SnapshotProbe(1, 0, false);
        var second = new SnapshotProbe(2, 0, false);
        var extra = new SnapshotProbe(3, 0, false);
        var providers = new Dictionary<int, IRollbackStateProvider> { [1] = first, [3] = extra, [2] = second };
        var entries = new[] { new WorldRollbackSnapshotEntry(2, new byte[] { 2 }), new WorldRollbackSnapshotEntry(1, new byte[] { 1 }) };
        var context = new RollbackSnapshotPreflightContext(new FrameIndex(7), entries, providers);
        providers.Clear();
        Array.Reverse(entries);
        Assert.Equal(new[] { 2, 1, 3 }, context.Providers.Select(pair => pair.Key));
        Assert.Equal(new[] { 2, 1, 3 }, context.Providers.Keys);
        Assert.Equal(new IRollbackStateProvider[] { second, first, extra }, context.Providers.Values);
        Assert.Same(first, context.Providers[1]);
        Assert.Equal(new byte[] { 2 }, context.RequirePayload(2).ToArray());
        Assert.False(context.TryGetPayload(3, out _));
    }

    [Fact]
    public void Cross_provider_preflight_runs_before_any_import()
    {
        var first = new SnapshotProbe(1, requiredKey: 2, reject: true);
        var second = new SnapshotProbe(2, requiredKey: 0, reject: false);
        var registry = new RollbackRegistry();
        registry.Register(first);
        registry.Register(second);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = new WorldRollbackSnapshot(
            WorldRollbackSnapshotCodec.CurrentVersion,
            new FrameIndex(8),
            new[]
            {
                new WorldRollbackSnapshotEntry(1, new byte[] { 1 }),
                new WorldRollbackSnapshotEntry(2, new byte[] { 2 }),
            });

        Assert.False(coordinator.TryRestore(in snapshot, out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(0, first.ImportCount);
        Assert.Equal(0, second.ImportCount);
        Assert.Equal(new byte[] { 2 }, first.ObservedPayload);
    }

    [Fact]
    public void Duplicate_snapshot_key_is_rejected_before_import()
    {
        var provider = new SnapshotProbe(7, requiredKey: 0, reject: false);
        var registry = new RollbackRegistry();
        registry.Register(provider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = new WorldRollbackSnapshot(
            WorldRollbackSnapshotCodec.CurrentVersion,
            new FrameIndex(9),
            new[]
            {
                new WorldRollbackSnapshotEntry(7, new byte[] { 1 }),
                new WorldRollbackSnapshotEntry(7, new byte[] { 2 }),
            });

        Assert.False(coordinator.TryRestore(in snapshot, out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(0, provider.ImportCount);
    }

    [Fact]
    public void RestoreFloorRejectsOldLocalAndExternalSnapshotsWithoutImport()
    {
        var probe = new SnapshotProbe(1, 0, false);
        var registry = new RollbackRegistry();
        registry.Register(probe);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));
        var earlier = new FrameIndex(9);
        var boundary = new FrameIndex(10);
        Assert.True(coordinator.CaptureAndStore(earlier));
        var oldSnapshot = coordinator.Capture(earlier);
        Assert.False(coordinator.TryGetRestoreFloor(out _));
        Assert.False(coordinator.TryAdvanceRestoreFloor(boundary));
        Assert.True(coordinator.CaptureAndStore(boundary));
        Assert.True(coordinator.TryAdvanceRestoreFloor(boundary));
        Assert.True(coordinator.TryAdvanceRestoreFloor(boundary));
        Assert.False(coordinator.TryAdvanceRestoreFloor(earlier));

        Assert.False(coordinator.TryRestore(earlier, out var local));
        Assert.Equal(RollbackOperationStatus.BeforeRestoreFloor, local.Status);
        Assert.Equal(0, local.ProviderCount);
        Assert.False(coordinator.TryRestore(in oldSnapshot, out var external));
        Assert.Equal(RollbackOperationStatus.BeforeRestoreFloor, external.Status);
        Assert.Equal(0, external.ProviderCount);
        Assert.Throws<InvalidOperationException>(() => coordinator.Restore(oldSnapshot));
        Assert.Equal(0, probe.ImportCount);
        Assert.True(coordinator.TryRestore(boundary));
        Assert.Equal(1, probe.ImportCount);

        coordinator.ClearHistory();
        Assert.True(coordinator.TryGetRestoreFloor(out var committed));
        Assert.Equal(boundary, committed);
        Assert.False(coordinator.TryRestore(in oldSnapshot, out external));
        Assert.Equal(RollbackOperationStatus.BeforeRestoreFloor, external.Status);
        Assert.Equal(1, probe.ImportCount);
    }

    [Fact]
    public void CorrectedBranchRejectsDiscardedAnchorUntilRecaptured()
    {
        var registry = new RollbackRegistry();
        var probe = new SnapshotProbe(1, 0, false);
        registry.Register(probe);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));
        var restored = new FrameIndex(9);
        var anchor = new FrameIndex(10);
        Assert.True(coordinator.CaptureAndStore(restored));
        Assert.True(coordinator.CaptureAndStore(anchor));
        Assert.True(coordinator.TryAdvanceRestoreFloor(restored));
        Assert.False(coordinator.TryRestoreAndDiscardFuture(new FrameIndex(8), out var rejected));
        Assert.Equal(RollbackOperationStatus.BeforeRestoreFloor, rejected.Status);

        Assert.True(coordinator.TryRestoreAndDiscardFuture(restored, out _));
        Assert.False(coordinator.TryAdvanceRestoreFloor(anchor));
        Assert.False(coordinator.TryRestore(anchor, out rejected));
        Assert.Equal(RollbackOperationStatus.SnapshotNotFound, rejected.Status);
        Assert.Equal(1, probe.ImportCount);
        Assert.True(coordinator.TryGetRestoreFloor(out var floor));
        Assert.Equal(restored, floor);

        Assert.True(coordinator.CaptureAndStore(anchor));
        Assert.True(coordinator.TryAdvanceRestoreFloor(anchor));
    }

    [Fact]
    public void RestoreFloorCannotAdvanceFromAnInFlightImport()
    {
        var registry = new RollbackRegistry();
        var probe = new SnapshotProbe(1, 0, false);
        registry.Register(probe);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));
        var earlier = new FrameIndex(9);
        var boundary = new FrameIndex(10);
        Assert.True(coordinator.CaptureAndStore(earlier));
        Assert.True(coordinator.CaptureAndStore(boundary));
        bool? advancedDuringImport = null;
        probe.OnImport = () => advancedDuringImport = coordinator.TryAdvanceRestoreFloor(boundary);

        Assert.True(coordinator.TryRestore(earlier));
        Assert.Equal(false, advancedDuringImport);
        Assert.False(coordinator.TryGetRestoreFloor(out _));
        Assert.True(coordinator.TryAdvanceRestoreFloor(boundary));
    }

    private sealed class SnapshotProbe : IRollbackStateProvider, IRollbackStatePreflightProvider,
        IRollbackSnapshotPreflightProvider
    {
        private readonly int _requiredKey;
        private readonly bool _reject;

        public SnapshotProbe(int key, int requiredKey, bool reject)
        {
            Key = key;
            _requiredKey = requiredKey;
            _reject = reject;
        }

        public int Key { get; }
        public int ImportCount { get; private set; }
        public Action? OnImport { get; set; }
        public byte[] ObservedPayload { get; private set; } = Array.Empty<byte>();

        public byte[] Export(FrameIndex frame) => new byte[] { (byte)Key };

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
        }

        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            if (_requiredKey != 0)
            {
                var payload = context.RequirePayload(_requiredKey);
                ObservedPayload = payload.ToArray();
            }

            if (_reject) throw new InvalidOperationException("cross-provider mismatch");
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            ImportCount++;
            OnImport?.Invoke();
        }
    }
}
