using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;

internal readonly struct ShooterPureStateFrameSampleDiagnostics
{
    public ShooterPureStateFrameSampleDiagnostics(
        int frameCount,
        int transformCount,
        int nearEligibleCount,
        int midEligibleCount,
        int farEligibleCount,
        int nearSelectedCount,
        int midSelectedCount,
        int farSelectedCount,
        int observerControlledSkippedCount,
        int rejectedCount)
    {
        FrameCount = frameCount;
        TransformCount = transformCount;
        NearEligibleCount = nearEligibleCount;
        MidEligibleCount = midEligibleCount;
        FarEligibleCount = farEligibleCount;
        NearSelectedCount = nearSelectedCount;
        MidSelectedCount = midSelectedCount;
        FarSelectedCount = farSelectedCount;
        ObserverControlledSkippedCount = observerControlledSkippedCount;
        RejectedCount = rejectedCount;
    }

    public int FrameCount { get; }
    public int TransformCount { get; }
    public int NearEligibleCount { get; }
    public int MidEligibleCount { get; }
    public int FarEligibleCount { get; }
    public int NearSelectedCount { get; }
    public int MidSelectedCount { get; }
    public int FarSelectedCount { get; }
    public int ObserverControlledSkippedCount { get; }
    public int RejectedCount { get; }
    public int EligibleCount => NearEligibleCount + MidEligibleCount + FarEligibleCount;
    public double SelectionRatio => EligibleCount > 0 ? TransformCount / (double)EligibleCount : 0d;
}

internal sealed class ShooterPureStateFrameSampleRing
{
    private const int PositionScale = 1000;
    private readonly Slot[] _slots;
    private ShooterPureStateFrameSample[] _frameBuffer = Array.Empty<ShooterPureStateFrameSample>();
    private ShooterPureStateTransformSample[] _transformBuffer = Array.Empty<ShooterPureStateTransformSample>();
    private int _start;
    private int _count;
    private int _lastFrame = -1;

    public ShooterPureStateFrameSampleRing(int capacity = 8)
    {
        _slots = new Slot[Math.Max(2, capacity)];
        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i] = new Slot();
        }
    }

    public void Capture(int frame, long serverTick, ShooterPureStateTransformSample[] samples, int count)
    {
        if (frame <= _lastFrame || samples == null)
        {
            return;
        }

        count = Math.Max(0, Math.Min(count, samples.Length));
        var index = (_start + _count) % _slots.Length;
        if (_count == _slots.Length)
        {
            index = _start;
            _start = (_start + 1) % _slots.Length;
        }
        else
        {
            _count++;
        }

        var slot = _slots[index];
        slot.EnsureCapacity(count);
        Array.Copy(samples, 0, slot.Transforms, 0, count);
        slot.Frame = frame;
        slot.ServerTick = serverTick;
        slot.Count = count;
        _lastFrame = frame;
    }

    public void AttachTo(
        ref ShooterPureStateSnapshotPayload payload,
        int blockFrameCount,
        int maxTransformsPerFrame,
        ShooterPureStateInterestScope? interestScope)
    {
        AttachTo(
            ref payload,
            blockFrameCount,
            maxTransformsPerFrame,
            interestScope,
            ShooterPureStateSampleDensityPolicy.FullDensity,
            out _);
    }

    public void AttachTo(
        ref ShooterPureStateSnapshotPayload payload,
        int blockFrameCount,
        int maxTransformsPerFrame,
        ShooterPureStateInterestScope? interestScope,
        ShooterPureStateSampleDensityPolicy densityPolicy,
        out ShooterPureStateFrameSampleDiagnostics diagnostics)
    {
        diagnostics = default;
        payload.FrameSamples = _frameBuffer;
        payload.TransformSamples = _transformBuffer;
        payload.SetTransientCounts(
            payload.EffectiveEntityCount,
            payload.EffectiveVisibilityHintCount,
            payload.EffectiveAcknowledgedCommandCount,
            frameSampleCount: 0,
            transformSampleCount: 0);

        var historicalLimit = Math.Max(0, Math.Min(_slots.Length, blockFrameCount - 1));
        if (historicalLimit == 0 || _count == 0)
        {
            return;
        }

        EnsureFrameCapacity(historicalLimit);
        var first = Math.Max(0, _count - historicalLimit - 1);
        var perFrameTransformLimit = Math.Max(0, maxTransformsPerFrame);
        var blockTransformCapacity = (long)perFrameTransformLimit * historicalLimit;
        var blockTransformLimit = (int)Math.Min(
            densityPolicy.MaxHistoricalTransformsPerBlock,
            Math.Min(int.MaxValue, blockTransformCapacity));
        var rotatesSelectionWindow = densityPolicy.MaxHistoricalTransformsPerBlock < int.MaxValue;
        var blockOrdinal = Math.Max(0, payload.Frame - 1) / Math.Max(1, blockFrameCount);
        var frameCount = 0;
        var transformCount = 0;
        var nearEligibleCount = 0;
        var midEligibleCount = 0;
        var farEligibleCount = 0;
        var nearSelectedCount = 0;
        var midSelectedCount = 0;
        var farSelectedCount = 0;
        var observerControlledSkippedCount = 0;
        var rejectedCount = 0;
        for (var i = first; i < _count && frameCount < historicalLimit; i++)
        {
            var slot = _slots[(_start + i) % _slots.Length];
            if (slot.Frame >= payload.Frame)
            {
                continue;
            }

            var offset = transformCount;
            var perFrameCount = 0;
            var blockRemaining = Math.Max(0, blockTransformLimit - transformCount);
            var remainingFrameSlots = Math.Max(1, historicalLimit - frameCount);
            var fairFrameLimit = blockRemaining / remainingFrameSlots;
            if ((blockRemaining % remainingFrameSlots) != 0)
            {
                fairFrameLimit++;
            }

            var frameLimit = Math.Min(perFrameTransformLimit, fairFrameLimit);
            EnsureTransformCapacity(transformCount + Math.Min(slot.Count, frameLimit));
            var historicalAge = payload.Frame - slot.Frame;
            for (var tierValue = (int)SampleTier.Near;
                 tierValue <= (int)SampleTier.Far;
                 tierValue++)
            {
                var requestedTier = (SampleTier)tierValue;
                var stride = ResolveStride(requestedTier, in densityPolicy);
                var includesFrame = IncludesHistoricalFrame(stride, historicalAge);
                var selectionStart = rotatesSelectionWindow && slot.Count > 0
                    ? (int)(((long)blockOrdinal * blockTransformLimit) % slot.Count)
                    : 0;
                for (var sampleIndex = 0;
                     sampleIndex < slot.Count;
                     sampleIndex++)
                {
                    var rotatedIndex = selectionStart + sampleIndex;
                    if (rotatedIndex >= slot.Count)
                    {
                        rotatedIndex -= slot.Count;
                    }

                    var sample = slot.Transforms[rotatedIndex];
                    if (!TryResolveTier(in sample, interestScope, in densityPolicy, out var tier, out var rejection))
                    {
                        if (requestedTier == SampleTier.Near)
                        {
                            if (rejection == SampleRejection.ObserverControlled)
                            {
                                observerControlledSkippedCount++;
                            }
                            else
                            {
                                rejectedCount++;
                            }
                        }

                        continue;
                    }

                    if (tier != requestedTier)
                    {
                        continue;
                    }

                    IncrementTierCount(
                        tier,
                        ref nearEligibleCount,
                        ref midEligibleCount,
                        ref farEligibleCount);
                    if (!includesFrame || perFrameCount >= frameLimit)
                    {
                        continue;
                    }

                    _transformBuffer[transformCount++] = sample;
                    perFrameCount++;
                    IncrementTierCount(
                        tier,
                        ref nearSelectedCount,
                        ref midSelectedCount,
                        ref farSelectedCount);
                }
            }

            _frameBuffer[frameCount++] = new ShooterPureStateFrameSample(
                slot.Frame,
                slot.ServerTick,
                offset,
                perFrameCount);
        }

        if (frameCount == 0)
        {
            return;
        }

        diagnostics = new ShooterPureStateFrameSampleDiagnostics(
            frameCount,
            transformCount,
            nearEligibleCount,
            midEligibleCount,
            farEligibleCount,
            nearSelectedCount,
            midSelectedCount,
            farSelectedCount,
            observerControlledSkippedCount,
            rejectedCount);

        payload.FrameSamples = _frameBuffer;
        payload.TransformSamples = _transformBuffer;
        payload.SetTransientCounts(
            payload.EffectiveEntityCount,
            payload.EffectiveVisibilityHintCount,
            payload.EffectiveAcknowledgedCommandCount,
            frameCount,
            transformCount);
    }

    public void Clear()
    {
        _start = 0;
        _count = 0;
        _lastFrame = -1;
        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i].Count = 0;
        }
    }

    private static bool TryResolveTier(
        in ShooterPureStateTransformSample sample,
        ShooterPureStateInterestScope? interestScope,
        in ShooterPureStateSampleDensityPolicy densityPolicy,
        out SampleTier tier,
        out SampleRejection rejection)
    {
        const byte visibleAndAlive = ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible;
        if ((sample.Flags & visibleAndAlive) != visibleAndAlive)
        {
            tier = default;
            rejection = SampleRejection.Inactive;
            return false;
        }

        if (!interestScope.HasValue || !interestScope.Value.HasRadius)
        {
            tier = SampleTier.Near;
            rejection = SampleRejection.None;
            return true;
        }

        var scope = interestScope.Value;
        if (scope.ObserverPlayerId > 0 &&
            sample.EntityKind == ShooterPackedEntityKinds.Player &&
            sample.EntityId == scope.ObserverPlayerId)
        {
            tier = default;
            rejection = SampleRejection.ObserverControlled;
            return false;
        }

        var radius = Math.Max(scope.VisibleRadius, scope.BoundaryRadius);
        var x = sample.QuantizedX / (float)PositionScale;
        var y = sample.QuantizedY / (float)PositionScale;
        var dx = x - scope.CenterX;
        var dy = y - scope.CenterY;
        var distanceSquared = (dx * dx) + (dy * dy);
        if (distanceSquared > radius * radius)
        {
            tier = default;
            rejection = SampleRejection.OutsideAoi;
            return false;
        }

        var nearRadius = scope.VisibleRadius * densityPolicy.NearRadiusRatio;
        var midRadius = scope.VisibleRadius * densityPolicy.MidRadiusRatio;
        tier = distanceSquared <= nearRadius * nearRadius
            ? SampleTier.Near
            : distanceSquared <= midRadius * midRadius
                ? SampleTier.Mid
                : SampleTier.Far;
        rejection = SampleRejection.None;
        return true;
    }

    private static int ResolveStride(
        SampleTier tier,
        in ShooterPureStateSampleDensityPolicy densityPolicy)
    {
        return tier switch
        {
            SampleTier.Near => densityPolicy.NearHistoricalStride,
            SampleTier.Mid => densityPolicy.MidHistoricalStride,
            _ => densityPolicy.FarHistoricalStride
        };
    }

    private static bool IncludesHistoricalFrame(int stride, int historicalAge)
    {
        return stride > 0 && historicalAge > 0 && ((historicalAge - 1) % stride) == 0;
    }

    private static void IncrementTierCount(
        SampleTier tier,
        ref int nearCount,
        ref int midCount,
        ref int farCount)
    {
        switch (tier)
        {
            case SampleTier.Near:
                nearCount++;
                break;
            case SampleTier.Mid:
                midCount++;
                break;
            default:
                farCount++;
                break;
        }
    }

    private void EnsureFrameCapacity(int count)
    {
        if (_frameBuffer.Length < count)
        {
            _frameBuffer = new ShooterPureStateFrameSample[Math.Max(count, _frameBuffer.Length * 2 + 2)];
        }
    }

    private void EnsureTransformCapacity(int count)
    {
        if (_transformBuffer.Length >= count)
        {
            return;
        }

        var capacity = Math.Max(16, _transformBuffer.Length);
        while (capacity < count)
        {
            capacity = checked(capacity * 2);
        }

        var next = new ShooterPureStateTransformSample[capacity];
        if (_transformBuffer.Length > 0)
        {
            Array.Copy(_transformBuffer, 0, next, 0, _transformBuffer.Length);
        }

        _transformBuffer = next;
    }

    private sealed class Slot
    {
        public int Frame;
        public long ServerTick;
        public ShooterPureStateTransformSample[] Transforms = Array.Empty<ShooterPureStateTransformSample>();
        public int Count;

        public void EnsureCapacity(int count)
        {
            if (Transforms.Length >= count)
            {
                return;
            }

            var capacity = Math.Max(16, Transforms.Length);
            while (capacity < count)
            {
                capacity = checked(capacity * 2);
            }

            Transforms = new ShooterPureStateTransformSample[capacity];
        }
    }

    private enum SampleTier
    {
        Near = 0,
        Mid = 1,
        Far = 2
    }

    private enum SampleRejection
    {
        None = 0,
        Inactive = 1,
        ObserverControlled = 2,
        OutsideAoi = 3
    }
}
