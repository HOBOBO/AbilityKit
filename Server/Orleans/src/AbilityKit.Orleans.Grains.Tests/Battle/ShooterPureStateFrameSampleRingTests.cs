using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class ShooterPureStateFrameSampleRingTests
{
    [Fact]
    public void AttachTo_FlattensTwoHistoricalFramesBeforeAuthority()
    {
        var ring = new ShooterPureStateFrameSampleRing();
        ring.Capture(1, 100L, CreateSamples(1000), 2);
        ring.Capture(2, 200L, CreateSamples(2000), 2);
        ring.Capture(3, 300L, CreateSamples(3000), 2);
        var payload = CreatePayload(frame: 3);

        ring.AttachTo(ref payload, blockFrameCount: 3, maxTransformsPerFrame: 8, interestScope: null);

        Assert.Equal(2, payload.EffectiveFrameSampleCount);
        Assert.Equal(4, payload.EffectiveTransformSampleCount);
        Assert.Equal(1, payload.FrameSamples[0].Frame);
        Assert.Equal(0, payload.FrameSamples[0].TransformOffset);
        Assert.Equal(2, payload.FrameSamples[0].TransformCount);
        Assert.Equal(2, payload.FrameSamples[1].Frame);
        Assert.Equal(2, payload.FrameSamples[1].TransformOffset);
        Assert.Equal(2000, payload.TransformSamples[2].QuantizedX);
    }

    [Fact]
    public void AttachTo_AppliesObserverBoundaryWithoutLeakingFarTransforms()
    {
        var ring = new ShooterPureStateFrameSampleRing();
        ring.Capture(1, 100L, CreateSamples(1000), 2);
        ring.Capture(2, 200L, CreateSamples(2000), 2);
        var payload = CreatePayload(frame: 2);
        var scope = new ShooterPureStateInterestScope(99, 0f, 0f, 2f, 3f);

        ring.AttachTo(ref payload, blockFrameCount: 3, maxTransformsPerFrame: 8, scope);

        Assert.Equal(1, payload.EffectiveFrameSampleCount);
        Assert.Equal(1, payload.EffectiveTransformSampleCount);
        Assert.Equal(1, payload.TransformSamples[0].EntityId);
    }

    [Fact]
    public void AttachTo_AppliesNearMidFarHistoricalDensity()
    {
        var ring = new ShooterPureStateFrameSampleRing();
        var samples = CreateDensitySamples();
        ring.Capture(1, 100L, samples, samples.Length);
        ring.Capture(2, 200L, samples, samples.Length);
        ring.Capture(3, 300L, samples, samples.Length);
        var payload = CreatePayload(frame: 3);
        var scope = new ShooterPureStateInterestScope(99, 0f, 0f, 24f, 30f);

        ring.AttachTo(
            ref payload,
            blockFrameCount: 3,
            maxTransformsPerFrame: 8,
            scope,
            ShooterPureStateSampleDensityPolicy.MassBattle,
            out var diagnostics);

        Assert.Equal(2, payload.EffectiveFrameSampleCount);
        Assert.Equal(3, payload.EffectiveTransformSampleCount);
        Assert.Equal(1, payload.FrameSamples[0].TransformCount);
        Assert.Equal(2, payload.FrameSamples[1].TransformCount);
        Assert.Equal(new[] { 10, 10, 20 }, payload.TransformSamples
            .Take(payload.EffectiveTransformSampleCount)
            .Select(sample => sample.EntityId)
            .ToArray());
        Assert.Equal(2, diagnostics.NearEligibleCount);
        Assert.Equal(2, diagnostics.MidEligibleCount);
        Assert.Equal(2, diagnostics.FarEligibleCount);
        Assert.Equal(2, diagnostics.NearSelectedCount);
        Assert.Equal(1, diagnostics.MidSelectedCount);
        Assert.Equal(0, diagnostics.FarSelectedCount);
        Assert.Equal(0.5d, diagnostics.SelectionRatio, precision: 3);
    }

    [Fact]
    public void AttachTo_WhenBudgetIsLimited_PrioritizesNearOverEarlierMidSamples()
    {
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        var samples = new[]
        {
            new ShooterPureStateTransformSample(20, ShooterPackedEntityKinds.Enemy, 15_000, 0, 0, 0, 0, 0, flags),
            new ShooterPureStateTransformSample(10, ShooterPackedEntityKinds.Enemy, 5_000, 0, 0, 0, 0, 0, flags),
            new ShooterPureStateTransformSample(30, ShooterPackedEntityKinds.Enemy, 22_000, 0, 0, 0, 0, 0, flags)
        };
        var ring = new ShooterPureStateFrameSampleRing();
        ring.Capture(1, 100L, samples, samples.Length);
        var payload = CreatePayload(frame: 2);
        var scope = new ShooterPureStateInterestScope(99, 0f, 0f, 24f, 30f);

        ring.AttachTo(
            ref payload,
            blockFrameCount: 2,
            maxTransformsPerFrame: 1,
            scope,
            ShooterPureStateSampleDensityPolicy.MassBattle,
            out var diagnostics);

        Assert.Equal(1, payload.EffectiveTransformSampleCount);
        Assert.Equal(10, payload.TransformSamples[0].EntityId);
        Assert.Equal(1, diagnostics.NearSelectedCount);
        Assert.Equal(0, diagnostics.MidSelectedCount);
    }

    [Fact]
    public void AttachTo_MassBattleCapsAndSharesTheWholeHistoricalBlockBudget()
    {
        var ring = new ShooterPureStateFrameSampleRing();
        var samples = CreateDistributedSamples(1_000);
        ring.Capture(1, 100L, samples, samples.Length);
        ring.Capture(2, 200L, samples, samples.Length);
        ring.Capture(3, 300L, samples, samples.Length);
        var payload = CreatePayload(frame: 3);
        var scope = new ShooterPureStateInterestScope(99_999, 0f, 0f, 24f, 30f);

        ring.AttachTo(
            ref payload,
            blockFrameCount: 3,
            maxTransformsPerFrame: 1_000,
            scope,
            ShooterPureStateSampleDensityPolicy.MassBattle,
            out var diagnostics);

        Assert.Equal(2, payload.EffectiveFrameSampleCount);
        Assert.Equal(32, payload.EffectiveTransformSampleCount);
        Assert.Equal(16, payload.FrameSamples[0].TransformCount);
        Assert.Equal(16, payload.FrameSamples[1].TransformCount);
        Assert.Equal(32, diagnostics.NearSelectedCount);
    }

    [Fact]
    public void AttachTo_SmoothMassBattleKeepsDenseNearAndMidHistory()
    {
        var ring = new ShooterPureStateFrameSampleRing();
        var samples = CreateDistributedSamples(1_000);
        CaptureThreeFrames(ring, samples);
        var payload = CreatePayload(frame: 3);
        var scope = new ShooterPureStateInterestScope(99_999, 0f, 0f, 24f, 30f);

        ring.AttachTo(
            ref payload,
            blockFrameCount: 3,
            maxTransformsPerFrame: 2_048,
            scope,
            ShooterPureStateSampleDensityPolicy.SmoothMassBattle,
            out var diagnostics);

        Assert.Equal(2, payload.EffectiveFrameSampleCount);
        Assert.Equal(1_400, payload.EffectiveTransformSampleCount);
        Assert.Equal(400, payload.FrameSamples[0].TransformCount);
        Assert.Equal(1_000, payload.FrameSamples[1].TransformCount);
        Assert.Equal(800, diagnostics.NearSelectedCount);
        Assert.Equal(300, diagnostics.MidSelectedCount);
        Assert.Equal(300, diagnostics.FarSelectedCount);

        var compressedBytes = ShooterPureStateSyncCodec.Serialize(in payload);
        var rawPayload = payload;
        rawPayload.Version = 2;
        var rawBytes = ShooterPureStateSyncCodec.Serialize(in rawPayload);
        Assert.True(
            compressedBytes.Length <= rawBytes.Length * 0.55d,
            $"Expected compressed history <= 55% of v2 raw history. Compressed={compressedBytes.Length}, raw={rawBytes.Length}.");
    }

    [Fact]
    public void AttachTo_MassBattleRotatesTheHistoricalEntityWindowAcrossBlocks()
    {
        var ring = new ShooterPureStateFrameSampleRing();
        var samples = CreateDistributedSamples(1_000);
        var scope = new ShooterPureStateInterestScope(99_999, 0f, 0f, 24f, 30f);

        CaptureThreeFrames(ring, samples);
        var firstBlock = CreatePayload(frame: 3);
        ring.AttachTo(
            ref firstBlock,
            blockFrameCount: 3,
            maxTransformsPerFrame: 1_000,
            scope,
            ShooterPureStateSampleDensityPolicy.MassBattle,
            out _);
        var firstOlderIds = GetFrameEntityIds(in firstBlock, frameIndex: 0);
        var firstNewerIds = GetFrameEntityIds(in firstBlock, frameIndex: 1);

        ring.Capture(4, 400L, samples, samples.Length);
        ring.Capture(5, 500L, samples, samples.Length);
        ring.Capture(6, 600L, samples, samples.Length);
        var secondBlock = CreatePayload(frame: 6);
        ring.AttachTo(
            ref secondBlock,
            blockFrameCount: 3,
            maxTransformsPerFrame: 1_000,
            scope,
            ShooterPureStateSampleDensityPolicy.MassBattle,
            out _);

        var secondOlderIds = GetFrameEntityIds(in secondBlock, frameIndex: 0);
        var secondNewerIds = GetFrameEntityIds(in secondBlock, frameIndex: 1);
        Assert.Equal(Enumerable.Range(1, 16), firstOlderIds);
        Assert.Equal(firstOlderIds, firstNewerIds);
        Assert.Equal(Enumerable.Range(33, 16), secondOlderIds);
        Assert.Equal(secondOlderIds, secondNewerIds);
        Assert.Empty(firstOlderIds.Intersect(secondOlderIds));
    }

    [Fact]
    public void AttachTo_SkipsObserverControlledPlayerFromHistoricalSamples()
    {
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        var samples = new[]
        {
            new ShooterPureStateTransformSample(7, ShooterPackedEntityKinds.Player, 1_000, 0, 0, 0, 0, 0, flags),
            new ShooterPureStateTransformSample(8, ShooterPackedEntityKinds.Enemy, 2_000, 0, 0, 0, 0, 0, flags)
        };
        var ring = new ShooterPureStateFrameSampleRing();
        ring.Capture(1, 100L, samples, samples.Length);
        var payload = CreatePayload(frame: 2);
        var scope = new ShooterPureStateInterestScope(7, 0f, 0f, 24f, 30f);

        ring.AttachTo(
            ref payload,
            blockFrameCount: 2,
            maxTransformsPerFrame: 8,
            scope,
            ShooterPureStateSampleDensityPolicy.MassBattle,
            out var diagnostics);

        Assert.Equal(1, payload.EffectiveTransformSampleCount);
        Assert.Equal(8, payload.TransformSamples[0].EntityId);
        Assert.Equal(1, diagnostics.ObserverControlledSkippedCount);
    }

    [Fact]
    public void AttachTo_WhenOnlyFarSamplesRemain_KeepsEmptyFrameDescriptor()
    {
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        var samples = new[]
        {
            new ShooterPureStateTransformSample(30, ShooterPackedEntityKinds.Enemy, 22_000, 0, 0, 0, 0, 0, flags)
        };
        var ring = new ShooterPureStateFrameSampleRing();
        ring.Capture(1, 100L, samples, samples.Length);
        var payload = CreatePayload(frame: 2);
        var scope = new ShooterPureStateInterestScope(99, 0f, 0f, 24f, 30f);

        ring.AttachTo(
            ref payload,
            blockFrameCount: 2,
            maxTransformsPerFrame: 8,
            scope,
            ShooterPureStateSampleDensityPolicy.MassBattle,
            out var diagnostics);

        Assert.Equal(1, payload.EffectiveFrameSampleCount);
        Assert.Equal(0, payload.FrameSamples[0].TransformCount);
        Assert.Equal(0, payload.EffectiveTransformSampleCount);
        Assert.Equal(1, diagnostics.FarEligibleCount);
    }

    [Fact]
    public void Clear_DropsSamplesFromPreviousWorld()
    {
        var ring = new ShooterPureStateFrameSampleRing();
        ring.Capture(1, 100L, CreateSamples(1000), 2);
        ring.Clear();
        var payload = CreatePayload(frame: 2);

        ring.AttachTo(ref payload, blockFrameCount: 3, maxTransformsPerFrame: 8, interestScope: null);

        Assert.Equal(0, payload.EffectiveFrameSampleCount);
        Assert.Equal(0, payload.EffectiveTransformSampleCount);
    }

    [Fact]
    public void CaptureAndAttach_DoNotAllocateAfterCapacityWarmup()
    {
        var ring = new ShooterPureStateFrameSampleRing();
        var samples = CreateDistributedSamples(1_200);
        var payload = CreatePayload(frame: 3);
        var scope = new ShooterPureStateInterestScope(99_999, 0f, 0f, 24f, 30f);
        for (var frame = 1; frame <= 10; frame++)
        {
            ring.Capture(frame, frame * 100L, samples, samples.Length);
            payload.Frame = frame;
            ring.AttachTo(
                ref payload,
                blockFrameCount: 3,
                maxTransformsPerFrame: 1_200,
                scope,
                ShooterPureStateSampleDensityPolicy.MassBattle,
                out _);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 11; frame <= 74; frame++)
        {
            ring.Capture(frame, frame * 100L, samples, samples.Length);
            payload.Frame = frame;
            ring.AttachTo(
                ref payload,
                blockFrameCount: 3,
                maxTransformsPerFrame: 1_200,
                scope,
                ShooterPureStateSampleDensityPolicy.MassBattle,
                out _);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 256L, $"Frame sample ring allocated {allocated} bytes after warmup.");
    }

    [Fact]
    public void Serialize_OneThousandUnits_DensityBlockReducesPayloadAgainstFullBlock()
    {
        const int unitCount = 1_000;
        var transforms = CreateDistributedSamples(unitCount);
        var entities = CreateEntityDeltas(transforms);
        var scope = new ShooterPureStateInterestScope(99_999, 0f, 0f, 24f, 30f);
        var single = CreatePayload(frame: 3, entities);
        var singleBytes = ShooterPureStateSyncCodec.Serialize(in single);

        var fullRing = new ShooterPureStateFrameSampleRing();
        CaptureThreeFrames(fullRing, transforms);
        var full = CreatePayload(frame: 3, entities);
        fullRing.AttachTo(
            ref full,
            blockFrameCount: 3,
            maxTransformsPerFrame: unitCount,
            scope,
            ShooterPureStateSampleDensityPolicy.FullDensity,
            out var fullDiagnostics);
        var fullBytes = ShooterPureStateSyncCodec.Serialize(in full);

        var lodRing = new ShooterPureStateFrameSampleRing();
        CaptureThreeFrames(lodRing, transforms);
        var lod = CreatePayload(frame: 3, entities);
        lodRing.AttachTo(
            ref lod,
            blockFrameCount: 3,
            maxTransformsPerFrame: unitCount,
            scope,
            ShooterPureStateSampleDensityPolicy.MassBattle,
            out var lodDiagnostics);
        var lodBytes = ShooterPureStateSyncCodec.Serialize(in lod);

        Assert.True(singleBytes.Length < lodBytes.Length);
        Assert.True(lodBytes.Length < fullBytes.Length);
        Assert.True(
            lodBytes.Length <= fullBytes.Length * 0.80d,
            $"Expected density block <= 80% of full block. Single={singleBytes.Length}, LOD={lodBytes.Length}, Full={fullBytes.Length}.");
        Assert.True(lodDiagnostics.TransformCount < fullDiagnostics.TransformCount * 0.05d);
        Assert.Equal(2_000, fullDiagnostics.TransformCount);
        Assert.Equal(32, lodDiagnostics.TransformCount);
    }

    private static ShooterPureStateTransformSample[] CreateSamples(int nearX)
    {
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        return new[]
        {
            new ShooterPureStateTransformSample(1, ShooterPackedEntityKinds.Player, nearX, 0, 100, 0, 100, 0, flags),
            new ShooterPureStateTransformSample(2, ShooterPackedEntityKinds.Enemy, 100_000, 0, 0, 0, 0, 0, flags)
        };
    }

    private static ShooterPureStateTransformSample[] CreateDensitySamples()
    {
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        return new[]
        {
            new ShooterPureStateTransformSample(10, ShooterPackedEntityKinds.Enemy, 5_000, 0, 0, 0, 0, 0, flags),
            new ShooterPureStateTransformSample(20, ShooterPackedEntityKinds.Enemy, 15_000, 0, 0, 0, 0, 0, flags),
            new ShooterPureStateTransformSample(30, ShooterPackedEntityKinds.Enemy, 22_000, 0, 0, 0, 0, 0, flags)
        };
    }

    private static ShooterPureStateTransformSample[] CreateDistributedSamples(int count)
    {
        const byte flags = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        var samples = new ShooterPureStateTransformSample[count];
        var nearCount = count * 4 / 10;
        var midCount = count * 3 / 10;
        for (var i = 0; i < count; i++)
        {
            var quantizedX = i < nearCount
                ? 5_000
                : i < nearCount + midCount
                    ? 15_000
                    : 22_000;
            samples[i] = new ShooterPureStateTransformSample(
                i + 1,
                ShooterPackedEntityKinds.Enemy,
                quantizedX,
                i % 1_000,
                100,
                0,
                100,
                0,
                flags);
        }

        return samples;
    }

    private static ShooterPureStateEntityDelta[] CreateEntityDeltas(
        ShooterPureStateTransformSample[] transforms)
    {
        var entities = new ShooterPureStateEntityDelta[transforms.Length];
        for (var i = 0; i < transforms.Length; i++)
        {
            var transform = transforms[i];
            entities[i] = new ShooterPureStateEntityDelta(
                transform.EntityId,
                transform.EntityKind,
                ShooterPureStateEntityLayers.Combat,
                ShooterPureStateDeltaKinds.Update,
                0,
                transform.QuantizedX,
                transform.QuantizedY,
                transform.QuantizedVelocityX,
                transform.QuantizedVelocityY,
                transform.QuantizedFacingX,
                transform.QuantizedFacingY,
                100,
                0,
                0,
                transform.Flags);
        }

        return entities;
    }

    private static void CaptureThreeFrames(
        ShooterPureStateFrameSampleRing ring,
        ShooterPureStateTransformSample[] transforms)
    {
        ring.Capture(1, 100L, transforms, transforms.Length);
        ring.Capture(2, 200L, transforms, transforms.Length);
        ring.Capture(3, 300L, transforms, transforms.Length);
    }

    private static int[] GetFrameEntityIds(
        in ShooterPureStateSnapshotPayload payload,
        int frameIndex)
    {
        var frame = payload.FrameSamples[frameIndex];
        return payload.TransformSamples
            .Skip(frame.TransformOffset)
            .Take(frame.TransformCount)
            .Select(sample => sample.EntityId)
            .ToArray();
    }

    private static ShooterPureStateSnapshotPayload CreatePayload(
        int frame,
        ShooterPureStateEntityDelta[]? entities = null)
    {
        return new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            99UL,
            frame,
            frame * 100L,
            ShooterPureStateSnapshotKinds.Delta,
            0,
            0u,
            1u,
            ShooterPureStateSyncSettings.Default,
            entities ?? Array.Empty<ShooterPureStateEntityDelta>(),
            Array.Empty<ShooterPureStateVisibilityHint>());
    }
}
