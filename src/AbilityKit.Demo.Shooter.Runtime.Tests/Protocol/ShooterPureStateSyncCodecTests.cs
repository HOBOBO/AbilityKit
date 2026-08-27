using System;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Protocol;

public sealed class ShooterPureStateSyncCodecTests
{
    [Fact]
    public void ReusableDeserializeOverwritesStableArraysWithoutReplacingThem()
    {
        var snapshot = CreateSnapshot();
        snapshot.Frame = 10;
        var first = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var buffer = new ShooterPureStateSyncDecodeBuffer();

        var firstDecoded = buffer.Decode(first.AsSpan());
        var entities = firstDecoded.Entities;
        var hints = firstDecoded.VisibilityHints;

        snapshot.Frame = 11;
        var second = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var secondDecoded = buffer.Decode(second.AsSpan());

        Assert.Same(entities, secondDecoded.Entities);
        Assert.Same(hints, secondDecoded.VisibilityHints);
        Assert.Equal(11, secondDecoded.Frame);
        Assert.Equal(snapshot.EffectiveEntityCount, secondDecoded.EffectiveEntityCount);
    }

    [Fact]
    public void ReusableDeserializeDoesNotAllocateAfterWarmup()
    {
        var snapshot = CreateSnapshot();
        var payload = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var buffer = new ShooterPureStateSyncDecodeBuffer();
        buffer.Decode(payload.AsSpan());

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            buffer.Decode(payload.AsSpan());
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 256, $"Expected allocation-free steady state, actual={allocated} bytes.");
    }

    [Fact]
    public void ReusableDeserializeReusesCapacityAcrossVaryingEntityCounts()
    {
        var populated = CreateSnapshot();
        var empty = CreateSnapshot();
        empty.SetTransientCounts(0, 0);
        var populatedPayload = ShooterPureStateSyncCodec.Serialize(in populated);
        var emptyPayload = ShooterPureStateSyncCodec.Serialize(in empty);
        var buffer = new ShooterPureStateSyncDecodeBuffer();
        var first = buffer.Decode(populatedPayload);
        var capacity = first.Entities;
        buffer.Decode(emptyPayload);
        buffer.Decode(populatedPayload);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            buffer.Decode((i & 1) == 0 ? emptyPayload : populatedPayload);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var decodedEmpty = buffer.Decode(emptyPayload);
        Assert.Same(capacity, decodedEmpty.Entities);
        Assert.Equal(0, decodedEmpty.EffectiveEntityCount);
        var decodedPopulated = buffer.Decode(populatedPayload);
        Assert.Same(capacity, decodedPopulated.Entities);
        Assert.Equal(1, decodedPopulated.EffectiveEntityCount);
        Assert.True(allocated < 256, $"Expected allocation-free varying-count decode, actual={allocated} bytes.");
    }

    [Fact]
    public void TransientSerializationReusesExactLengthOutputBuffer()
    {
        var snapshot = CreateSnapshot();
        var buffer = new ReusableMemoryPackSerializationBuffer();

        var first = ShooterPureStateSyncCodec.SerializeTransient(in snapshot, buffer);
        var restored = ShooterPureStateSyncCodec.Deserialize(first);
        var second = ShooterPureStateSyncCodec.SerializeTransient(in snapshot, buffer);

        Assert.Same(first, second);
        Assert.Equal(buffer.WrittenCount, second.Length);
        Assert.Equal(snapshot.WorldId, restored.WorldId);
        Assert.Equal(snapshot.Entities, restored.Entities);
    }

    [Fact]
    public void TransientSerializationDoesNotAllocateAfterWarmup()
    {
        var snapshot = CreateSnapshot();
        var buffer = new ReusableMemoryPackSerializationBuffer();
        ShooterPureStateSyncCodec.SerializeTransient(in snapshot, buffer);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            ShooterPureStateSyncCodec.SerializeTransient(in snapshot, buffer);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 256, $"Expected allocation-free steady state, actual={allocated} bytes.");
    }

    [Fact]
    public void CapacityBackedSnapshotSerializesOnlyEffectivePrefixesInCurrentLayout()
    {
        var exact = CreateSnapshot();
        var entities = new ShooterPureStateEntityDelta[8];
        var hints = new ShooterPureStateVisibilityHint[8];
        entities[0] = exact.Entities[0];
        hints[0] = exact.VisibilityHints[0];
        var capacityBacked = new ShooterPureStateSnapshotPayload(
            exact.Version,
            exact.WorldId,
            exact.Frame,
            exact.ServerTick,
            exact.SnapshotKind,
            exact.BaselineFrame,
            exact.BaselineHash,
            exact.StateHash,
            exact.Settings,
            entities,
            hints);
        capacityBacked.SetTransientCounts(1, 1);

        var expected = MemoryPackSerializer.Serialize(exact);
        var actual = ShooterPureStateSyncCodec.Serialize(in capacityBacked);
        var restored = ShooterPureStateSyncCodec.Deserialize(actual);

        Assert.Equal(expected, actual);
        Assert.Single(restored.Entities);
        Assert.Single(restored.VisibilityHints);
        Assert.Equal(exact.Entities[0], restored.Entities[0]);
    }

    [Fact]
    public void SegmentSerializationReusesCapacityBufferAcrossPayloadLengths()
    {
        var snapshot = CreateSnapshot();
        var buffer = new ReusableMemoryPackSerializationBuffer();
        var first = ShooterPureStateSyncCodec.SerializeTransientSegment(in snapshot, buffer);
        snapshot.SetTransientCounts(0, 0);
        var second = ShooterPureStateSyncCodec.SerializeTransientSegment(in snapshot, buffer);

        Assert.Same(first.Array, second.Array);
        Assert.True(first.Count > second.Count);
        Assert.Equal(buffer.WrittenCount, second.Count);
        var restored = ShooterPureStateSyncCodec.Deserialize(second.ToArray());
        Assert.Empty(restored.Entities);
        Assert.Empty(restored.VisibilityHints);
    }

    [Fact]
    public void WireSegmentSerializationMatchesOwnedPayloadLayout()
    {
        var payloadCapacity = new byte[64];
        payloadCapacity[0] = 1;
        payloadCapacity[1] = 2;
        payloadCapacity[2] = 3;
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 9,
            Frame = 10,
            Timestamp = 1.25,
            IsFullSnapshot = false,
            PayloadOpCode = 77,
            Payload = new byte[] { 1, 2, 3 },
            ServerTicks = 11
        };
        var expected = WireRoomGatewayBinary.Serialize(in wire);
        var buffer = new ReusableMemoryPackSerializationBuffer();

        var actual = WireRoomGatewayBinary.SerializeTransient(
            in wire,
            new ArraySegment<byte>(payloadCapacity, 0, 3),
            buffer);
        var restored = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(actual);

        Assert.Equal(expected.ToArray(), actual.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3 }, restored.Payload);
    }

    [Fact]
    public void WireSnapshotDecodeBufferReusesStablePayloadArray()
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 9,
            Frame = 10,
            PayloadOpCode = ShooterOpCodes.Snapshot.PureStateDelta,
            Payload = new byte[] { 1, 2, 3, 4 },
            EventEpoch = "epoch"
        };
        var encoded = WireRoomGatewayBinary.Serialize(in wire);
        var buffer = new WireStateSyncSnapshotPushDecodeBuffer();

        var first = buffer.Decode(encoded);
        wire.Frame = 11;
        encoded = WireRoomGatewayBinary.Serialize(in wire);
        var second = buffer.Decode(encoded);

        Assert.Same(first.Payload, second.Payload);
        Assert.Equal(11, second.Frame);
        Assert.Equal(wire.Payload, second.Payload);
    }

    [Fact]
    public void RoundTripPreservesPureStateSnapshotEnvelope()
    {
        var settings = new ShooterPureStateSyncSettings(
            1000,
            100,
            120,
            10,
            90,
            3,
            10,
            30,
            90);
        var snapshot = new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            99ul,
            12,
            3456,
            ShooterPureStateSnapshotKinds.Delta,
            10,
            111u,
            222u,
            settings,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    7,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.Update,
                    1,
                    1200,
                    3400,
                    20,
                    -10,
                    0,
                    0,
                    12,
                    ShooterPureStateEntityFlags.Visible)
            },
            new[]
            {
                new ShooterPureStateVisibilityHint(
                    7,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateEntityFlags.Visible,
                    100)
            });

        var bytes = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var restored = ShooterPureStateSyncCodec.Deserialize(bytes);

        Assert.Equal(snapshot.WorldId, restored.WorldId);
        Assert.Equal(snapshot.Frame, restored.Frame);
        Assert.Equal(snapshot.SnapshotKind, restored.SnapshotKind);
        Assert.Equal(snapshot.BaselineFrame, restored.BaselineFrame);
        Assert.Equal(snapshot.Settings.MaxEntityCount, restored.Settings.MaxEntityCount);
        Assert.Equal(10, restored.Settings.NearLodIntervalFrames);
        Assert.Equal(30, restored.Settings.MidLodIntervalFrames);
        Assert.Equal(90, restored.Settings.FarLodIntervalFrames);
        Assert.Single(restored.Entities);
        Assert.Single(restored.VisibilityHints);
        Assert.Equal(ShooterPureStateDeltaKinds.Update, restored.Entities[0].DeltaKind);
    }

    [Fact]
    public void FrameSampleBlockRoundTripsThroughOwnedAndReusableDecoders()
    {
        var snapshot = CreateSnapshot();
        snapshot.Frame = 12;
        snapshot.FrameSamples = new[]
        {
            new ShooterPureStateFrameSample(10, 1000L, 0, 1),
            new ShooterPureStateFrameSample(11, 1100L, 1, 1)
        };
        snapshot.TransformSamples = new[]
        {
            new ShooterPureStateTransformSample(7, ShooterPackedEntityKinds.Projectile, 1000, 2000, 10, 20, 3),
            new ShooterPureStateTransformSample(7, ShooterPackedEntityKinds.Projectile, 1100, 2200, 10, 20, 3)
        };
        snapshot.SetTransientCounts(1, 1, 0, 2, 2);

        var payload = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var owned = ShooterPureStateSyncCodec.Deserialize(payload);
        var reusable = new ShooterPureStateSyncDecodeBuffer().Decode(payload);

        Assert.Equal(2, owned.EffectiveFrameSampleCount);
        Assert.Equal(2, owned.EffectiveTransformSampleCount);
        Assert.Equal(10, owned.FrameSamples[0].Frame);
        Assert.Equal(2200, owned.TransformSamples[1].QuantizedY);
        Assert.Equal(2, reusable.EffectiveFrameSampleCount);
        Assert.Equal(2, reusable.EffectiveTransformSampleCount);
        Assert.Equal(11, reusable.FrameSamples[1].Frame);
    }

    [Fact]
    public void FrameSampleBlockUsesCompressedWireLayoutAndPreservesEveryField()
    {
        var snapshot = CreateSnapshot();
        const int samplesPerFrame = 128;
        const int sampleCount = samplesPerFrame * 2;
        snapshot.Frame = 12;
        snapshot.FrameSamples = new[]
        {
            new ShooterPureStateFrameSample(10, 1000L, 0, samplesPerFrame),
            new ShooterPureStateFrameSample(11, 1100L, samplesPerFrame, samplesPerFrame)
        };
        snapshot.TransformSamples = new ShooterPureStateTransformSample[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            snapshot.TransformSamples[i] = new ShooterPureStateTransformSample(
                (i % samplesPerFrame) + 1,
                (i & 1) == 0 ? ShooterPackedEntityKinds.Enemy : ShooterPackedEntityKinds.Projectile,
                5_000 + i,
                -2_000 + i,
                (i & 1) == 0 ? 100 : -100,
                (i & 1) == 0 ? -50 : 50,
                (byte)((i & 1) == 0 ? 3 : 2));
        }
        snapshot.SetTransientCounts(1, 1, 0, 2, sampleCount);

        var compressed = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var legacyRaw = MemoryPackSerializer.Serialize(snapshot);
        var restored = ShooterPureStateSyncCodec.Deserialize(compressed);

        Assert.Equal(15, compressed[0]);
        Assert.True(compressed.Length < legacyRaw.Length * 0.80d,
            $"Expected compressed block < 80% of raw layout. Compressed={compressed.Length}, raw={legacyRaw.Length}.");
        Assert.Equal(snapshot.TransformSamples, restored.TransformSamples);
        Assert.Equal(snapshot.FrameSamples, restored.FrameSamples);
    }

    [Fact]
    public void ReusableCompressedDecodeDoesNotAllocateAfterWarmup()
    {
        var snapshot = CreateSnapshot();
        const int count = 1_000;
        snapshot.Frame = 12;
        snapshot.FrameSamples = new[] { new ShooterPureStateFrameSample(11, 1100L, 0, count) };
        snapshot.TransformSamples = new ShooterPureStateTransformSample[count];
        for (var i = 0; i < count; i++)
        {
            snapshot.TransformSamples[i] = new ShooterPureStateTransformSample(
                i + 1,
                ShooterPackedEntityKinds.Enemy,
                5_000 + i,
                -2_000 + i,
                100,
                -50,
                ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible);
        }
        snapshot.SetTransientCounts(1, 1, 0, 1, count);
        var payload = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var buffer = new ShooterPureStateSyncDecodeBuffer();
        var warmup = buffer.Decode(payload);
        var transformSamples = warmup.TransformSamples;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            buffer.Decode(payload);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var decoded = buffer.Decode(payload);
        Assert.Same(transformSamples, decoded.TransformSamples);
        Assert.Equal(count, decoded.EffectiveTransformSampleCount);
        Assert.True(allocated < 256, $"Expected allocation-free compressed decode, actual={allocated} bytes.");
    }

    [Fact]
    public void VersionTwoRawFrameSampleBlockRemainsReadable()
    {
        var snapshot = CreateSnapshot();
        snapshot.Version = 2;
        snapshot.Frame = 12;
        snapshot.FrameSamples = new[] { new ShooterPureStateFrameSample(11, 1100L, 0, 1) };
        snapshot.TransformSamples = new[]
        {
            new ShooterPureStateTransformSample(7, ShooterPackedEntityKinds.Enemy, 1_000, -2_000, 100, -50, 3)
        };
        snapshot.SetTransientCounts(1, 1, 0, 1, 1);

        var payload = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var owned = ShooterPureStateSyncCodec.Deserialize(payload);
        var reusable = new ShooterPureStateSyncDecodeBuffer().Decode(payload);

        Assert.Equal(14, payload[0]);
        Assert.Equal(2, owned.Version);
        Assert.Equal(snapshot.TransformSamples, owned.TransformSamples);
        Assert.Equal(snapshot.TransformSamples, reusable.TransformSamples.Take(1));
    }

    [Fact]
    public void ReusableDecoderKeepsFrameSampleArraysStableAfterWarmup()
    {
        var snapshot = CreateSnapshot();
        snapshot.FrameSamples = new[] { new ShooterPureStateFrameSample(19, 19L, 0, 1) };
        snapshot.TransformSamples = new[]
        {
            new ShooterPureStateTransformSample(7, ShooterPackedEntityKinds.Projectile, 1, 2, 3, 4, 3)
        };
        snapshot.SetTransientCounts(1, 1, 0, 1, 1);
        var payload = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var buffer = new ShooterPureStateSyncDecodeBuffer();
        var warmup = buffer.Decode(payload);
        var frameSamples = warmup.FrameSamples;
        var transformSamples = warmup.TransformSamples;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            buffer.Decode(payload);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var decoded = buffer.Decode(payload);
        Assert.Same(frameSamples, decoded.FrameSamples);
        Assert.Same(transformSamples, decoded.TransformSamples);
        Assert.True(allocated < 256, $"Expected allocation-free frame sample decode, actual={allocated} bytes.");
    }

    [Fact]
    public void VersionOneAcknowledgedPayloadStillDecodesWithoutFrameSamples()
    {
        var snapshot = CreateSnapshot();
        var legacy = new VersionOnePureStateSnapshotPayload(
            1,
            snapshot.WorldId,
            snapshot.Frame,
            snapshot.ServerTick,
            snapshot.SnapshotKind,
            snapshot.BaselineFrame,
            snapshot.BaselineHash,
            snapshot.StateHash,
            snapshot.Settings,
            snapshot.Entities,
            snapshot.VisibilityHints,
            Array.Empty<ShooterCommandAcknowledgement>());
        var payload = MemoryPackSerializer.Serialize(legacy);

        var decoded = new ShooterPureStateSyncDecodeBuffer().Decode(payload);

        Assert.Equal(1, decoded.Version);
        Assert.Equal(1, decoded.EffectiveEntityCount);
        Assert.Equal(0, decoded.EffectiveFrameSampleCount);
        Assert.Equal(0, decoded.EffectiveTransformSampleCount);
    }

    [Fact]
    public void InvalidVersionTwoFrameLayoutIsRejectedInsteadOfFallingBackToLegacy()
    {
        var snapshot = CreateSnapshot();
        snapshot.FrameSamples = new[] { new ShooterPureStateFrameSample(snapshot.Frame + 1, 1L, 0, 1) };
        snapshot.TransformSamples = new[]
        {
            new ShooterPureStateTransformSample(7, ShooterPackedEntityKinds.Projectile, 1, 2, 3, 4, 3)
        };
        snapshot.SetTransientCounts(1, 1, 0, 1, 1);
        var payload = ShooterPureStateSyncCodec.Serialize(in snapshot);

        Assert.Throws<MemoryPackSerializationException>(() => ShooterPureStateSyncCodec.Deserialize(payload));
        Assert.Throws<MemoryPackSerializationException>(() => new ShooterPureStateSyncDecodeBuffer().Decode(payload));
    }

    [Fact]
    public void LegacyPayloadWithoutAcknowledgementsDecodesThroughBothApis()
    {
        var snapshot = CreateSnapshot();
        var legacy = new LegacyPureStateSnapshotPayload(
            snapshot.Version,
            snapshot.WorldId,
            snapshot.Frame,
            snapshot.ServerTick,
            snapshot.SnapshotKind,
            snapshot.BaselineFrame,
            snapshot.BaselineHash,
            snapshot.StateHash,
            snapshot.Settings,
            snapshot.Entities,
            snapshot.VisibilityHints);
        var payload = MemoryPackSerializer.Serialize(legacy);

        var buffered = new ShooterPureStateSyncDecodeBuffer().Decode(payload);
        var owned = ShooterPureStateSyncCodec.Deserialize(payload);

        Assert.Equal(snapshot.WorldId, buffered.WorldId);
        Assert.Equal(snapshot.Frame, buffered.Frame);
        Assert.Single(buffered.Entities);
        Assert.Empty(buffered.AcknowledgedCommands);
        Assert.Equal(snapshot.WorldId, owned.WorldId);
        Assert.Equal(snapshot.Frame, owned.Frame);
        Assert.Single(owned.Entities);
        Assert.Empty(owned.AcknowledgedCommands);
    }

    [Fact]
    public void EmptyPayloadUsesDefaultSettings()
    {
        var restored = ShooterPureStateSyncCodec.Deserialize(null!);

        Assert.Equal(ShooterPureStateSyncCodec.CurrentVersion, restored.Version);
        Assert.Equal(ShooterPureStateSnapshotKinds.FullBaseline, restored.SnapshotKind);
        Assert.Equal(10000, restored.Settings.MaxEntityCount);
        Assert.Empty(restored.Entities);
        Assert.Empty(restored.VisibilityHints);
    }

    private static ShooterPureStateSnapshotPayload CreateSnapshot()
    {
        return new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            101ul,
            20,
            20,
            ShooterPureStateSnapshotKinds.Delta,
            18,
            123u,
            456u,
            ShooterPureStateSyncSettings.Default,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    7,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.Update,
                    1,
                    1200,
                    3400,
                    20,
                    -10,
                    0,
                    0,
                    12,
                    ShooterPureStateEntityFlags.Visible)
            },
            new[]
            {
                new ShooterPureStateVisibilityHint(
                    7,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateEntityFlags.Visible,
                    100)
            });
    }
}

[MemoryPackable]
internal partial struct VersionOnePureStateSnapshotPayload
{
    [MemoryPackOrder(0)] public int Version;
    [MemoryPackOrder(1)] public ulong WorldId;
    [MemoryPackOrder(2)] public int Frame;
    [MemoryPackOrder(3)] public long ServerTick;
    [MemoryPackOrder(4)] public int SnapshotKind;
    [MemoryPackOrder(5)] public int BaselineFrame;
    [MemoryPackOrder(6)] public uint BaselineHash;
    [MemoryPackOrder(7)] public uint StateHash;
    [MemoryPackOrder(8)] public ShooterPureStateSyncSettings Settings;
    [MemoryPackOrder(9)] public ShooterPureStateEntityDelta[] Entities;
    [MemoryPackOrder(10)] public ShooterPureStateVisibilityHint[] VisibilityHints;
    [MemoryPackOrder(11)] public ShooterCommandAcknowledgement[] AcknowledgedCommands;

    public VersionOnePureStateSnapshotPayload(
        int version,
        ulong worldId,
        int frame,
        long serverTick,
        int snapshotKind,
        int baselineFrame,
        uint baselineHash,
        uint stateHash,
        ShooterPureStateSyncSettings settings,
        ShooterPureStateEntityDelta[] entities,
        ShooterPureStateVisibilityHint[] visibilityHints,
        ShooterCommandAcknowledgement[] acknowledgedCommands)
    {
        Version = version;
        WorldId = worldId;
        Frame = frame;
        ServerTick = serverTick;
        SnapshotKind = snapshotKind;
        BaselineFrame = baselineFrame;
        BaselineHash = baselineHash;
        StateHash = stateHash;
        Settings = settings;
        Entities = entities;
        VisibilityHints = visibilityHints;
        AcknowledgedCommands = acknowledgedCommands;
    }
}

[MemoryPackable]
internal partial struct LegacyPureStateSnapshotPayload
{
    [MemoryPackOrder(0)] public int Version;
    [MemoryPackOrder(1)] public ulong WorldId;
    [MemoryPackOrder(2)] public int Frame;
    [MemoryPackOrder(3)] public long ServerTick;
    [MemoryPackOrder(4)] public int SnapshotKind;
    [MemoryPackOrder(5)] public int BaselineFrame;
    [MemoryPackOrder(6)] public uint BaselineHash;
    [MemoryPackOrder(7)] public uint StateHash;
    [MemoryPackOrder(8)] public ShooterPureStateSyncSettings Settings;
    [MemoryPackOrder(9)] public ShooterPureStateEntityDelta[] Entities;
    [MemoryPackOrder(10)] public ShooterPureStateVisibilityHint[] VisibilityHints;

    public LegacyPureStateSnapshotPayload(
        int version,
        ulong worldId,
        int frame,
        long serverTick,
        int snapshotKind,
        int baselineFrame,
        uint baselineHash,
        uint stateHash,
        ShooterPureStateSyncSettings settings,
        ShooterPureStateEntityDelta[] entities,
        ShooterPureStateVisibilityHint[] visibilityHints)
    {
        Version = version;
        WorldId = worldId;
        Frame = frame;
        ServerTick = serverTick;
        SnapshotKind = snapshotKind;
        BaselineFrame = baselineFrame;
        BaselineHash = baselineHash;
        StateHash = stateHash;
        Settings = settings;
        Entities = entities;
        VisibilityHints = visibilityHints;
    }
}
