using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaDeterministicCheckpointTests
{
    [Fact]
    public void Capture_OrdersProvidersAndProducesStableHash()
    {
        var first = new FakeProvider(20, 200);
        var second = new FakeProvider(10, 100);
        var frame = new FrameIndex(42);

        var unordered = CreateCoordinator(first, second).Capture(frame);
        var ordered = CreateCoordinator(second, first).Capture(frame);

        Assert.Equal(new[] { 10, 20 }, Array.ConvertAll(unordered.Entries, entry => entry.Key));
        Assert.Equal(ordered.StateHash, unordered.StateHash);
        Assert.Equal("world-1", unordered.WorldId);
        Assert.Equal("Moba", unordered.WorldType);
        Assert.Equal(30, unordered.TickRate);
        Assert.Equal(42, unordered.Frame);
    }

    [Fact]
    public void Restore_ReproducesContinuousProviderHash()
    {
        var sourceA = new FakeProvider(10, 111);
        var sourceB = new FakeProvider(20, 222);
        var frame = new FrameIndex(75);
        var checkpoint = CreateCoordinator(sourceA, sourceB).Capture(frame);

        var restoredA = new FakeProvider(10, -1);
        var restoredB = new FakeProvider(20, -2);
        var restored = CreateCoordinator(restoredB, restoredA);
        restored.Restore(checkpoint);

        Assert.Equal(111, restoredA.Value);
        Assert.Equal(222, restoredB.Value);
        Assert.Equal(checkpoint.StateHash, restored.ComputeStateHash(frame));
    }

    [Fact]
    public void Restore_WhenProviderFails_RollsBackAllTouchedProviders()
    {
        var checkpoint = CreateCoordinator(
            new FakeProvider(10, 111),
            new FakeProvider(20, 222)).Capture(new FrameIndex(8));

        var first = new FakeProvider(10, 7);
        var second = new FakeProvider(20, 9) { FailNextImport = true };
        var target = CreateCoordinator(first, second);

        Assert.Throws<InvalidOperationException>(() => target.Restore(checkpoint));
        Assert.Equal(7, first.Value);
        Assert.Equal(9, second.Value);
    }

    [Fact]
    public void Restore_WithDifferentWorldIdentity_DoesNotImportProviders()
    {
        var source = new FakeProvider(10, 111);
        var checkpoint = CreateCoordinator(source).Capture(new FrameIndex(3));
        var targetProvider = new FakeProvider(10, 7);
        var target = new MobaDeterministicCheckpointCoordinator(
            "other-world",
            "Moba",
            30,
            new[] { targetProvider });

        Assert.Throws<InvalidOperationException>(() => target.Restore(checkpoint));
        Assert.Equal(7, targetProvider.Value);
        Assert.Equal(0, targetProvider.ImportCount);
    }

    private static MobaDeterministicCheckpointCoordinator CreateCoordinator(params IMobaStateRecoveryProvider[] providers)
    {
        return new MobaDeterministicCheckpointCoordinator("world-1", "Moba", 30, providers);
    }

    private sealed class FakeProvider : IMobaStateRecoveryProvider
    {
        public FakeProvider(int key, int value)
        {
            Key = key;
            Value = value;
        }

        public int Key { get; }
        public string Name => $"Fake-{Key}";
        public int Value { get; private set; }
        public int ImportCount { get; private set; }
        public bool FailNextImport { get; set; }

        public byte[] ExportState(FrameIndex frame)
        {
            return BitConverter.GetBytes(Value);
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            ImportCount++;
            if (FailNextImport)
            {
                FailNextImport = false;
                throw new InvalidOperationException("Injected import failure.");
            }

            Value = BitConverter.ToInt32(payload, 0);
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            hash.AddInt(Value);
        }
    }
}
