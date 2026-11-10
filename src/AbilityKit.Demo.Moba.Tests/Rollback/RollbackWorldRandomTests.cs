using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Services;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Triggering.Runtime.Random;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Rollback;

public sealed class RollbackWorldRandomTests
{
    [Fact]
    public void SameSeedProducesTheSameMixedSequence()
    {
        var first = new RollbackWorldRandom();
        var second = new RollbackWorldRandom();
        first.SetSeed(123456);
        second.SetSeed(123456);

        for (var i = 0; i < 128; i++)
        {
            Assert.Equal(first.NextInt(int.MinValue, int.MaxValue), second.NextInt(int.MinValue, int.MaxValue));
            Assert.Equal(first.NextFloat01(), second.NextFloat01());
        }

        Assert.Equal((ulong)256, first.DrawCount);
        Assert.Equal(first.DrawCount, second.DrawCount);
    }

    [Fact]
    public void CoordinatorRestoreReplaysTheExactNextValues()
    {
        var random = new RollbackWorldRandom();
        random.SetSeed(77);
        var registry = new RollbackRegistry();
        registry.Register(random);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));

        Assert.True(coordinator.CaptureAndStore(new FrameIndex(10)));
        _ = random.NextInt(0, 1000);
        _ = random.NextFloat01();
        var expectedInt = random.NextInt(-100, 100);
        var expectedFloat = random.NextFloat01();

        Assert.True(coordinator.TryRestore(new FrameIndex(10)));
        _ = random.NextInt(0, 1000);
        _ = random.NextFloat01();

        Assert.Equal(expectedInt, random.NextInt(-100, 100));
        Assert.Equal(expectedFloat, random.NextFloat01());
    }

    [Fact]
    public void ImportRejectsUnsupportedAndMalformedPayloads()
    {
        var random = new RollbackWorldRandom();
        var frame = new FrameIndex(1);

        Assert.Throws<InvalidOperationException>(() => random.Import(frame, Array.Empty<byte>()));

        var unsupported = MemoryPackSerializer.Serialize(new MobaRollbackWorldRandomPayload(
            RollbackWorldRandom.CurrentPayloadVersion + 1,
            1,
            1,
            1,
            0));
        Assert.Throws<NotSupportedException>(() => random.ValidateImport(frame, unsupported));

        var zeroState = MemoryPackSerializer.Serialize(new MobaRollbackWorldRandomPayload(
            RollbackWorldRandom.CurrentPayloadVersion,
            1,
            0,
            0,
            0));
        Assert.Throws<InvalidOperationException>(() => random.ValidateImport(frame, zeroState));
    }

    [Fact]
    public void InvalidIntegerRangeIsRejectedBeforeConsumingTheStream()
    {
        var random = new RollbackWorldRandom();
        random.SetSeed(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(4, 4));
        Assert.Equal((ulong)0, random.DrawCount);
    }

    [Fact]
    public void WorldAndTriggerAdaptersShareTheCanonicalStreamContract()
    {
        const ulong seed = 0x1234_5678UL;
        IDeterministicRandomStream canonical = new AbilityKit.Deterministic.DeterministicRandom(seed);
        IDeterministicRandomStream world = new DefaultWorldRandom(unchecked((int)seed));
        IDeterministicRandomStream trigger = new AbilityKit.Triggering.Runtime.Random.DeterministicRandom(
            unchecked((uint)seed));
        IDeterministicRandomStream rollback = new RollbackWorldRandom();
        rollback.SetSeed(seed);

        for (var i = 0; i < 64; i++)
        {
            var expectedUInt = canonical.NextUInt64();
            Assert.Equal(expectedUInt, world.NextUInt64());
            Assert.Equal(expectedUInt, trigger.NextUInt64());
            Assert.Equal(expectedUInt, rollback.NextUInt64());

            var expectedInt = canonical.NextInt32(-1000, 1000);
            Assert.Equal(expectedInt, world.NextInt32(-1000, 1000));
            Assert.Equal(expectedInt, trigger.NextInt32(-1000, 1000));
            Assert.Equal(expectedInt, rollback.NextInt32(-1000, 1000));

            var expectedFloat = canonical.NextFloat01();
            Assert.Equal(expectedFloat, world.NextFloat01());
            Assert.Equal(expectedFloat, trigger.NextFloat01());
            Assert.Equal(expectedFloat, rollback.NextFloat01());
        }

        Assert.Equal(canonical.Sequence, world.Sequence);
        Assert.Equal(canonical.Sequence, trigger.Sequence);
        Assert.Equal(canonical.Sequence, rollback.Sequence);
    }

    [Fact]
    public void CanonicalStateCanBeRestoredAcrossAdapters()
    {
        IDeterministicRandomStream source = new AbilityKit.Deterministic.DeterministicRandom(42);
        _ = source.NextUInt64();
        _ = source.NextInt32(0, 10);
        source.CaptureState(out var state0, out var state1, out var sequence);

        IDeterministicRandomStream world = new DefaultWorldRandom(0);
        IDeterministicRandomStream trigger = new AbilityKit.Triggering.Runtime.Random.DeterministicRandom(0);
        IDeterministicRandomStream rollback = new RollbackWorldRandom();
        world.RestoreState(state0, state1, sequence);
        trigger.RestoreState(state0, state1, sequence);
        rollback.RestoreState(state0, state1, sequence);

        for (var i = 0; i < 32; i++)
        {
            var expected = source.NextUInt64();
            Assert.Equal(expected, world.NextUInt64());
            Assert.Equal(expected, trigger.NextUInt64());
            Assert.Equal(expected, rollback.NextUInt64());
        }
    }
}
