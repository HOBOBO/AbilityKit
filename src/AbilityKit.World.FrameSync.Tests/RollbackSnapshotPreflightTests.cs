using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using Xunit;

namespace AbilityKit.World.FrameSync.Tests;

public sealed class RollbackSnapshotPreflightTests
{
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

        public void Import(FrameIndex frame, byte[] payload) => ImportCount++;
    }
}
