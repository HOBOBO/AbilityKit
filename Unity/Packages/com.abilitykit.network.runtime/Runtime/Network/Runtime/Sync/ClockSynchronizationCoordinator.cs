#nullable enable

using System;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// A protocol-independent round-trip time sample. Clock offset uses local-minus-server
    /// seconds so callers can map server time to local time by adding <see cref="LocalMinusServerOffsetSeconds"/>.
    /// </summary>
    public readonly struct ClockSynchronizationSample
    {
        public ClockSynchronizationSample(double roundTripSeconds, double localMinusServerOffsetSeconds)
        {
            RoundTripSeconds = Math.Max(0d, roundTripSeconds);
            LocalMinusServerOffsetSeconds = localMinusServerOffsetSeconds;
        }

        public double RoundTripSeconds { get; }

        public double LocalMinusServerOffsetSeconds { get; }

        public static ClockSynchronizationSample FromRoundTrip(
            long clientSendTicks,
            long clientReceiveTicks,
            double localTickFrequency,
            long serverNowTicks,
            long serverTickFrequency)
        {
            if (localTickFrequency <= 0d)
                throw new ArgumentOutOfRangeException(nameof(localTickFrequency));
            if (serverTickFrequency <= 0L)
                throw new ArgumentOutOfRangeException(nameof(serverTickFrequency));

            var roundTripSeconds = Math.Max(0d, (clientReceiveTicks - clientSendTicks) / localTickFrequency);
            var localReceiveSeconds = clientReceiveTicks / localTickFrequency;
            var serverAtReceiveSeconds = serverNowTicks / (double)serverTickFrequency + roundTripSeconds * 0.5d;
            return new ClockSynchronizationSample(
                roundTripSeconds,
                localReceiveSeconds - serverAtReceiveSeconds);
        }
    }

    /// <summary>Immutable view of the current smoothed clock synchronization state.</summary>
    public readonly struct ClockSynchronizationEstimate
    {
        public ClockSynchronizationEstimate(
            bool hasSample,
            double localMinusServerOffsetSeconds,
            double roundTripSeconds,
            int sampleCount)
        {
            HasSample = hasSample;
            LocalMinusServerOffsetSeconds = localMinusServerOffsetSeconds;
            RoundTripSeconds = Math.Max(0d, roundTripSeconds);
            SampleCount = Math.Max(0, sampleCount);
        }

        public bool HasSample { get; }

        public double LocalMinusServerOffsetSeconds { get; }

        public double RoundTripSeconds { get; }

        public int SampleCount { get; }
    }

    /// <summary>
    /// Coordinates local frame anchors and smoothed protocol-independent server clock samples.
    /// It accepts different local and server tick frequencies and therefore can sit directly at a gateway boundary.
    /// </summary>
    public sealed class ClockSynchronizationCoordinator
    {
        private readonly double _smoothingFactor;
        private SyncClock _localClock;
        private ClockSynchronizationEstimate _estimate;

        public ClockSynchronizationCoordinator(int tickRate, double smoothingFactor = 0.2d)
        {
            _smoothingFactor = Clamp01(smoothingFactor);
            _localClock = CreateClock(tickRate);
        }

        public SyncTimeAnchor LastLocalAnchor { get; private set; }

        public ClockSynchronizationEstimate Estimate => _estimate;

        public SyncTimeAnchor AdvanceLocal()
        {
            LastLocalAnchor = _localClock.Advance();
            return LastLocalAnchor;
        }

        public ClockSynchronizationEstimate ObserveResponse(
            long clientSendTicks,
            long clientReceiveTicks,
            double localTickFrequency,
            long serverNowTicks,
            long serverTickFrequency)
        {
            var sample = ClockSynchronizationSample.FromRoundTrip(
                clientSendTicks,
                clientReceiveTicks,
                localTickFrequency,
                serverNowTicks,
                serverTickFrequency);
            return Observe(in sample);
        }

        public ClockSynchronizationEstimate Observe(in ClockSynchronizationSample sample)
        {
            _estimate = Smooth(in _estimate, in sample, _smoothingFactor);
            return _estimate;
        }

        public static ClockSynchronizationEstimate Smooth(
            in ClockSynchronizationEstimate current,
            in ClockSynchronizationSample sample,
            double smoothingFactor)
        {
            if (!current.HasSample)
            {
                return new ClockSynchronizationEstimate(
                    true,
                    sample.LocalMinusServerOffsetSeconds,
                    sample.RoundTripSeconds,
                    1);
            }

            var alpha = Clamp01(smoothingFactor);
            var inverse = 1d - alpha;
            return new ClockSynchronizationEstimate(
                true,
                alpha * sample.LocalMinusServerOffsetSeconds
                    + inverse * current.LocalMinusServerOffsetSeconds,
                alpha * sample.RoundTripSeconds
                    + inverse * current.RoundTripSeconds,
                current.SampleCount + 1);
        }

        public void Reset(int tickRate)
        {
            _localClock = CreateClock(tickRate);
            _estimate = default;
            LastLocalAnchor = default;
        }

        public static AuthoritativeTimeAnchorProjection ProjectAuthoritative(
            long startServerTicks,
            long serverTickFrequency,
            int startFrame,
            double fixedDeltaSeconds,
            long serverNowTicks)
        {
            if (serverTickFrequency <= 0L || fixedDeltaSeconds <= 0d || serverNowTicks <= 0L)
                return default;

            var elapsedSeconds = Math.Max(0d, (serverNowTicks - startServerTicks) / (double)serverTickFrequency);
            var catchUpFrames = Math.Max(0, (int)Math.Floor(elapsedSeconds / fixedDeltaSeconds));
            var targetFrame = startFrame + catchUpFrames;
            var anchor = SyncTimeAnchor
                .FromLocalFrame(targetFrame, catchUpFrames, elapsedSeconds)
                .WithAuthoritativeFrame(targetFrame)
                .WithServerTicks(serverNowTicks);
            return new AuthoritativeTimeAnchorProjection(
                true,
                serverNowTicks,
                targetFrame,
                catchUpFrames,
                elapsedSeconds,
                anchor);
        }

        private static SyncClock CreateClock(int tickRate)
        {
            if (tickRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRate));
            return new SyncClock(1d / tickRate, timelineTicksPerStep: 1L);
        }

        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            return value > 1d ? 1d : value;
        }
    }

    public readonly struct AuthoritativeTimeAnchorProjection
    {
        public AuthoritativeTimeAnchorProjection(
            bool anchorValid,
            long serverNowTicks,
            int targetFrame,
            int catchUpFrames,
            double elapsedSeconds,
            SyncTimeAnchor timeAnchor)
        {
            AnchorValid = anchorValid;
            ServerNowTicks = serverNowTicks;
            TargetFrame = targetFrame;
            CatchUpFrames = catchUpFrames;
            ElapsedSeconds = elapsedSeconds;
            TimeAnchor = timeAnchor;
        }

        public bool AnchorValid { get; }

        public long ServerNowTicks { get; }

        public int TargetFrame { get; }

        public int CatchUpFrames { get; }

        public double ElapsedSeconds { get; }

        public SyncTimeAnchor TimeAnchor { get; }
    }
}
