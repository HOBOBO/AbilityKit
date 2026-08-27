using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class SparseSnapshotTrackBufferTests
{
    [Fact]
    public void OrdinarySamplesDoNotEnableSparsePlayback()
    {
        var buffer = CreateBuffer();
        var sample = new Pose3(1f, 2f, 3f);

        buffer.Observe(7, 10L, in sample, SnapshotDeliveryHints.None);

        Assert.False(buffer.TrySample(7, 10d, out _, out var kind));
        Assert.Equal(SparseSnapshotSampleKind.None, kind);
    }

    [Fact]
    public void SparseUpdateInterpolatesFromPreviouslyObservedBaseline()
    {
        var buffer = CreateBuffer();
        var baseline = new Pose3(0f, 10f, -10f);
        var sparse = new Pose3(10f, 20f, 0f);
        buffer.Observe(7, 0L, in baseline, SnapshotDeliveryHints.None);
        buffer.Observe(7, 10L, in sparse, SnapshotDeliveryHints.SparseUpdate);

        Assert.True(buffer.TrySample(7, 5.5d, out var sampled, out var kind));

        Assert.Equal(SparseSnapshotSampleKind.Interpolated, kind);
        Assert.Equal(5.5f, sampled.X);
        Assert.Equal(15.5f, sampled.Y);
        Assert.Equal(-4.5f, sampled.Z);
    }

    [Fact]
    public void SparseTrackHoldsLatestSampleAfterItsTimeline()
    {
        var buffer = CreateBuffer();
        var sample = new Pose3(10f, 20f, 30f);
        buffer.Observe(7, 10L, in sample, SnapshotDeliveryHints.SparseUpdate);

        Assert.True(buffer.TrySample(7, 25d, out var sampled, out var kind));

        Assert.Equal(SparseSnapshotSampleKind.Held, kind);
        Assert.Equal(sample, sampled);
    }

    [Fact]
    public void SparseTrackExtrapolatesVelocityAndClampsAtConfiguredHorizon()
    {
        var buffer = new SparseSnapshotTrackBuffer<int, Pose3>(
            static (in Pose3 from, in Pose3 to, float alpha) => new Pose3(
                Lerp(from.X, to.X, alpha),
                Lerp(from.Y, to.Y, alpha),
                Lerp(from.Z, to.Z, alpha)),
            static (in Pose3 sample, double delta) => sample with
            {
                X = sample.X + (sample.Z * (float)delta)
            })
        {
            MaxExtrapolationTicks = 3d
        };
        var sample = new Pose3(10f, 20f, 2f);
        buffer.Observe(7, 10L, in sample, SnapshotDeliveryHints.SparseUpdate);

        Assert.True(buffer.TrySample(7, 12d, out var withinHorizon, out var withinKind));
        Assert.True(buffer.TrySample(7, 30d, out var clamped, out var clampedKind));

        Assert.Equal(SparseSnapshotSampleKind.Extrapolated, withinKind);
        Assert.Equal(14f, withinHorizon.X);
        Assert.Equal(SparseSnapshotSampleKind.Extrapolated, clampedKind);
        Assert.Equal(16f, clamped.X);
    }

    [Fact]
    public void DiscontinuityHintDisablesExtrapolation()
    {
        var buffer = new SparseSnapshotTrackBuffer<int, Pose3>(
            static (in Pose3 from, in Pose3 to, float alpha) => to,
            static (in Pose3 sample, double delta) => sample with { X = sample.X + 100f })
        {
            MaxExtrapolationTicks = 3d
        };
        var sample = new Pose3(10f, 20f, 2f);
        buffer.Observe(
            7,
            10L,
            in sample,
            SnapshotDeliveryHints.SparseUpdate | SnapshotDeliveryHints.Teleport);

        Assert.True(buffer.TrySample(7, 12d, out var held, out var kind));

        Assert.Equal(SparseSnapshotSampleKind.Held, kind);
        Assert.Equal(sample, held);
    }

    [Fact]
    public void OlderSampleCannotReplaceLatestTrackState()
    {
        var buffer = CreateBuffer();
        var latest = new Pose3(10f, 0f, 0f);
        var older = new Pose3(-100f, 0f, 0f);
        Assert.True(buffer.Observe(7, 10L, in latest, SnapshotDeliveryHints.SparseUpdate));
        Assert.False(buffer.Observe(7, 5L, in older, SnapshotDeliveryHints.SparseUpdate));

        Assert.True(buffer.TrySample(7, 20d, out var sampled, out _));
        Assert.Equal(10f, sampled.X);
    }

    [Theory]
    [InlineData(SnapshotDeliveryHints.Teleport)]
    [InlineData(SnapshotDeliveryHints.NoInterpolation)]
    public void DiscontinuityHintStepsInsteadOfInterpolating(SnapshotDeliveryHints discontinuity)
    {
        var buffer = CreateBuffer();
        var from = new Pose3(0f, 0f, 0f);
        var to = new Pose3(100f, 0f, 0f);
        buffer.Observe(7, 0L, in from, SnapshotDeliveryHints.None);
        buffer.Observe(7, 10L, in to, SnapshotDeliveryHints.SparseUpdate | discontinuity);

        Assert.True(buffer.TrySample(7, 5d, out var beforeTarget, out var beforeKind));
        Assert.True(buffer.TrySample(7, 10d, out var atTarget, out var targetKind));

        Assert.Equal(SparseSnapshotSampleKind.Held, beforeKind);
        Assert.Equal(0f, beforeTarget.X);
        Assert.Equal(SparseSnapshotSampleKind.Held, targetKind);
        Assert.Equal(100f, atTarget.X);
    }

    [Fact]
    public void RemoveClearAndKeyEnumeratorExposeBoundedLifecycle()
    {
        var buffer = CreateBuffer();
        var sample = new Pose3(1f, 2f, 3f);
        buffer.Observe(7, 10L, in sample, SnapshotDeliveryHints.SparseUpdate);
        buffer.Observe(9, 10L, in sample, SnapshotDeliveryHints.SparseUpdate);

        var keys = new HashSet<int>();
        var enumerator = buffer.GetKeyEnumerator();
        while (enumerator.MoveNext()) keys.Add(enumerator.Current);

        Assert.Equal(new HashSet<int> { 7, 9 }, keys);
        Assert.True(buffer.Remove(7));
        Assert.Equal(1, buffer.Count);
        buffer.Clear();
        Assert.Equal(0, buffer.Count);
    }

    [Theory]
    [InlineData(SnapshotDeliveryHints.None, true, true)]
    [InlineData(SnapshotDeliveryHints.PredictedOwner, false, true)]
    [InlineData(SnapshotDeliveryHints.PredictedOwner, true, false)]
    public void DeliveryPolicyProtectsOnlyLocallyControlledPredictedOwner(
        SnapshotDeliveryHints hints,
        bool isLocallyControlled,
        bool expected)
    {
        Assert.Equal(
            expected,
            SnapshotDeliveryPolicy.ShouldApplyAuthoritativeTransform(hints, isLocallyControlled));
    }

    private static SparseSnapshotTrackBuffer<int, Pose3> CreateBuffer()
    {
        return new SparseSnapshotTrackBuffer<int, Pose3>(static (in Pose3 from, in Pose3 to, float alpha) =>
            new Pose3(
                Lerp(from.X, to.X, alpha),
                Lerp(from.Y, to.Y, alpha),
                Lerp(from.Z, to.Z, alpha)));
    }

    private static float Lerp(float from, float to, float alpha)
    {
        return from + ((to - from) * alpha);
    }

    private readonly record struct Pose3(float X, float Y, float Z);
}
