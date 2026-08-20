#nullable enable

using System;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>Network measurements used to size frame-based buffers.</summary>
    public readonly struct NetworkBufferSizingSample
    {
        public NetworkBufferSizingSample(
            int tickRate,
            double roundTripSeconds,
            double jitterSeconds,
            double packetLossRate,
            bool isBufferStarved = false)
            : this(
                tickRate,
                hasRoundTripSample: true,
                roundTripSeconds,
                jitterSeconds,
                packetLossRate,
                isBufferStarved)
        {
        }

        public NetworkBufferSizingSample(
            int tickRate,
            bool hasRoundTripSample,
            double roundTripSeconds,
            double jitterSeconds,
            double packetLossRate,
            bool isBufferStarved = false)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));

            TickRate = tickRate;
            HasRoundTripSample = hasRoundTripSample;
            RoundTripSeconds = NormalizeNonNegative(roundTripSeconds);
            JitterSeconds = NormalizeNonNegative(jitterSeconds);
            PacketLossRate = Clamp01(packetLossRate);
            IsBufferStarved = isBufferStarved;
        }

        public int TickRate { get; }

        public bool HasRoundTripSample { get; }

        public double RoundTripSeconds { get; }

        public double JitterSeconds { get; }

        public double PacketLossRate { get; }

        public bool IsBufferStarved { get; }

        public static NetworkBufferSizingSample FromDiagnostics(
            in NetworkDiagnosticsSnapshot diagnostics,
            int tickRate,
            double jitterSeconds = 0d,
            double packetLossRate = 0d,
            bool isBufferStarved = false)
        {
            var hasRtt = diagnostics.EstimatedRttMs >= 0d;
            return new NetworkBufferSizingSample(
                tickRate,
                hasRtt,
                hasRtt ? diagnostics.EstimatedRttMs / 1000d : 0d,
                jitterSeconds,
                packetLossRate,
                isBufferStarved);
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value)) return 0d;
            if (value < 0d) return 0d;
            return value > 1d ? 1d : value;
        }

        private static double NormalizeNonNegative(double value)
        {
            if (double.IsNaN(value) || value <= 0d) return 0d;
            return double.IsPositiveInfinity(value) ? double.MaxValue : value;
        }
    }

    /// <summary>
    /// Smooths RTT, RTT variation and loss observations into buffer sizing samples.
    /// </summary>
    public sealed class NetworkBufferMetricsEstimator
    {
        private readonly int _tickRate;
        private readonly double _smoothingFactor;
        private double _previousRawRoundTripSeconds;
        private double _smoothedRoundTripSeconds;
        private double _smoothedJitterSeconds;
        private double _smoothedPacketLossRate;

        public NetworkBufferMetricsEstimator(int tickRate, double smoothingFactor = 0.2d)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));
            _tickRate = tickRate;
            _smoothingFactor = Clamp01(smoothingFactor);
        }

        public int SampleCount { get; private set; }

        public bool HasSample => SampleCount > 0;

        public NetworkBufferSizingSample Current => new NetworkBufferSizingSample(
            _tickRate,
            HasSample,
            _smoothedRoundTripSeconds,
            _smoothedJitterSeconds,
            _smoothedPacketLossRate);

        public NetworkBufferSizingSample Observe(
            double roundTripSeconds,
            double packetLossRate = 0d,
            bool isBufferStarved = false)
        {
            var rtt = NormalizeNonNegative(roundTripSeconds);
            var loss = Clamp01(packetLossRate);
            if (!HasSample)
            {
                _previousRawRoundTripSeconds = rtt;
                _smoothedRoundTripSeconds = rtt;
                _smoothedJitterSeconds = 0d;
                _smoothedPacketLossRate = loss;
            }
            else
            {
                var jitter = Math.Abs(rtt - _previousRawRoundTripSeconds);
                _smoothedRoundTripSeconds = Smooth(_smoothedRoundTripSeconds, rtt);
                _smoothedJitterSeconds = Smooth(_smoothedJitterSeconds, jitter);
                _smoothedPacketLossRate = Smooth(_smoothedPacketLossRate, loss);
                _previousRawRoundTripSeconds = rtt;
            }

            SampleCount++;
            return new NetworkBufferSizingSample(
                _tickRate,
                _smoothedRoundTripSeconds,
                _smoothedJitterSeconds,
                _smoothedPacketLossRate,
                isBufferStarved);
        }

        public NetworkBufferSizingSample Observe(
            in ClockSynchronizationEstimate estimate,
            double packetLossRate = 0d,
            bool isBufferStarved = false)
        {
            if (!estimate.HasSample)
            {
                return new NetworkBufferSizingSample(
                    _tickRate,
                    HasSample,
                    _smoothedRoundTripSeconds,
                    _smoothedJitterSeconds,
                    _smoothedPacketLossRate,
                    isBufferStarved);
            }

            return Observe(estimate.RoundTripSeconds, packetLossRate, isBufferStarved);
        }

        public void Reset()
        {
            SampleCount = 0;
            _previousRawRoundTripSeconds = 0d;
            _smoothedRoundTripSeconds = 0d;
            _smoothedJitterSeconds = 0d;
            _smoothedPacketLossRate = 0d;
        }

        private double Smooth(double current, double sample)
        {
            return _smoothingFactor * sample + (1d - _smoothingFactor) * current;
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value)) return 0d;
            if (value < 0d) return 0d;
            return value > 1d ? 1d : value;
        }

        private static double NormalizeNonNegative(double value)
        {
            if (double.IsNaN(value) || value <= 0d) return 0d;
            return double.IsPositiveInfinity(value) ? double.MaxValue : value;
        }
    }

    /// <summary>Parameters for converting network quality into a retained frame count.</summary>
    public sealed class NetworkBufferCapacityPolicyOptions
    {
        public NetworkBufferCapacityPolicyOptions(
            int minCapacity,
            int maxCapacity,
            double roundTripCoverage,
            double jitterMultiplier,
            int safetyFrames,
            int packetLossFrameScale,
            int starvationBoostFrames,
            int shrinkThresholdFrames,
            int shrinkDelaySamples,
            int maxShrinkFramesPerUpdate)
        {
            if (minCapacity < 0) throw new ArgumentOutOfRangeException(nameof(minCapacity));
            if (maxCapacity < minCapacity) throw new ArgumentOutOfRangeException(nameof(maxCapacity));
            if (!IsFiniteNonNegative(roundTripCoverage))
                throw new ArgumentOutOfRangeException(nameof(roundTripCoverage));
            if (!IsFiniteNonNegative(jitterMultiplier))
                throw new ArgumentOutOfRangeException(nameof(jitterMultiplier));
            if (safetyFrames < 0) throw new ArgumentOutOfRangeException(nameof(safetyFrames));
            if (packetLossFrameScale < 0) throw new ArgumentOutOfRangeException(nameof(packetLossFrameScale));
            if (starvationBoostFrames < 0) throw new ArgumentOutOfRangeException(nameof(starvationBoostFrames));
            if (shrinkThresholdFrames < 0) throw new ArgumentOutOfRangeException(nameof(shrinkThresholdFrames));
            if (shrinkDelaySamples < 0) throw new ArgumentOutOfRangeException(nameof(shrinkDelaySamples));
            if (maxShrinkFramesPerUpdate <= 0) throw new ArgumentOutOfRangeException(nameof(maxShrinkFramesPerUpdate));

            MinCapacity = minCapacity;
            MaxCapacity = maxCapacity;
            RoundTripCoverage = roundTripCoverage;
            JitterMultiplier = jitterMultiplier;
            SafetyFrames = safetyFrames;
            PacketLossFrameScale = packetLossFrameScale;
            StarvationBoostFrames = starvationBoostFrames;
            ShrinkThresholdFrames = shrinkThresholdFrames;
            ShrinkDelaySamples = shrinkDelaySamples;
            MaxShrinkFramesPerUpdate = maxShrinkFramesPerUpdate;
        }

        public int MinCapacity { get; }
        public int MaxCapacity { get; }
        public double RoundTripCoverage { get; }
        public double JitterMultiplier { get; }
        public int SafetyFrames { get; }
        public int PacketLossFrameScale { get; }
        public int StarvationBoostFrames { get; }
        public int ShrinkThresholdFrames { get; }
        public int ShrinkDelaySamples { get; }
        public int MaxShrinkFramesPerUpdate { get; }

        public static NetworkBufferCapacityPolicyOptions PredictionHistoryDefault => new NetworkBufferCapacityPolicyOptions(
            8, 256, 1d, 2d, 2, 16, 8, 2, 8, 2);

        public static NetworkBufferCapacityPolicyOptions InterpolationDelayDefault => new NetworkBufferCapacityPolicyOptions(
            0, 12, 0.5d, 2d, 1, 8, 2, 1, 8, 1);

        public static NetworkBufferCapacityPolicyOptions ServerInputDefault => new NetworkBufferCapacityPolicyOptions(
            2, 64, 0.5d, 2d, 2, 8, 4, 2, 12, 1);

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }

    /// <summary>
    /// Expands immediately but requires sustained lower demand before shrinking gradually.
    /// </summary>
    public sealed class AdaptiveNetworkBufferCapacityPolicy : IBufferCapacityPolicy<NetworkBufferSizingSample>
    {
        public AdaptiveNetworkBufferCapacityPolicy(NetworkBufferCapacityPolicyOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public NetworkBufferCapacityPolicyOptions Options { get; }

        public int LastRawTargetCapacity { get; private set; }

        public int PendingShrinkSamples { get; private set; }

        public int GetTargetCapacity(NetworkBufferSizingSample sample, int currentCapacity)
        {
            var current = Math.Max(0, currentCapacity);
            var rawTarget = CalculateRawTarget(sample, current);
            LastRawTargetCapacity = rawTarget;

            if (current == int.MaxValue || current > Options.MaxCapacity)
            {
                PendingShrinkSamples = 0;
                return rawTarget;
            }

            if (rawTarget > current)
            {
                PendingShrinkSamples = 0;
                return rawTarget;
            }

            if (rawTarget == current || current - rawTarget < Options.ShrinkThresholdFrames)
            {
                PendingShrinkSamples = 0;
                return current;
            }

            PendingShrinkSamples++;
            if (PendingShrinkSamples < Options.ShrinkDelaySamples)
            {
                return current;
            }

            PendingShrinkSamples = 0;
            return Math.Max(rawTarget, current - Options.MaxShrinkFramesPerUpdate);
        }

        public void Reset()
        {
            LastRawTargetCapacity = 0;
            PendingShrinkSamples = 0;
        }

        private int CalculateRawTarget(NetworkBufferSizingSample sample, int currentCapacity)
        {
            if (!sample.HasRoundTripSample)
            {
                if (!sample.IsBufferStarved) return currentCapacity;
                return Clamp((long)currentCapacity + Options.StarvationBoostFrames);
            }

            var roundTripSeconds = Options.RoundTripCoverage == 0d
                ? 0d
                : sample.RoundTripSeconds * Options.RoundTripCoverage;
            var jitterSeconds = Options.JitterMultiplier == 0d
                ? 0d
                : sample.JitterSeconds * Options.JitterMultiplier;
            var coveredSeconds = roundTripSeconds + jitterSeconds;
            if (double.IsInfinity(coveredSeconds)
                || coveredSeconds * sample.TickRate >= Options.MaxCapacity)
            {
                return Options.MaxCapacity;
            }

            var networkFrames = (long)Math.Ceiling(coveredSeconds * sample.TickRate);
            var lossFrames = (long)Math.Ceiling(sample.PacketLossRate * Options.PacketLossFrameScale);
            var starvationFrames = sample.IsBufferStarved ? Options.StarvationBoostFrames : 0;
            return Clamp(networkFrames + lossFrames + Options.SafetyFrames + starvationFrames);
        }

        private int Clamp(long capacity)
        {
            if (capacity < Options.MinCapacity) return Options.MinCapacity;
            return capacity > Options.MaxCapacity ? Options.MaxCapacity : (int)capacity;
        }
    }

    /// <summary>Optional network-driven binding for a retained-frame capacity.</summary>
    public sealed class AdaptiveNetworkBufferController
    {
        private readonly BufferCapacityController<NetworkBufferSizingSample> _controller;

        public AdaptiveNetworkBufferController(
            IBufferCapacityControl capacityControl,
            NetworkBufferCapacityPolicyOptions? options = null)
        {
            Policy = new AdaptiveNetworkBufferCapacityPolicy(
                options ?? NetworkBufferCapacityPolicyOptions.PredictionHistoryDefault);
            if (Policy.Options.MinCapacity <= 0)
            {
                throw new ArgumentException(
                    "Retained buffer capacity must have a positive minimum.",
                    nameof(options));
            }

            _controller = new BufferCapacityController<NetworkBufferSizingSample>(
                capacityControl,
                Policy,
                Policy.Options.MinCapacity,
                Policy.Options.MaxCapacity);
        }

        public AdaptiveNetworkBufferCapacityPolicy Policy { get; }

        public int CurrentCapacity => _controller.CurrentCapacity;

        public int TargetCapacity => _controller.LastTargetCapacity;

        public bool Observe(NetworkBufferSizingSample sample)
        {
            if (!sample.HasRoundTripSample) return false;
            return _controller.Update(sample);
        }
    }

    /// <summary>Optional capability for changing a frame playback delay at runtime.</summary>
    public interface IFrameDelayControl
    {
        int DelayFrames { get; }
        bool TrySetDelayFrames(int delayFrames);
    }

    /// <summary>Optional capability for changing a timeline delay expressed in timeline ticks.</summary>
    public interface ITimelineDelayControl
    {
        long TicksPerSecond { get; }
        long DelayTicks { get; }
        long TargetDelayTicks { get; }
        bool TrySetDelayTicks(long delayTicks);
    }

    /// <summary>Network-driven binding for interpolation or jitter-buffer frame delay.</summary>
    public sealed class AdaptiveNetworkFrameDelayController
    {
        private readonly IFrameDelayControl _delayControl;

        public AdaptiveNetworkFrameDelayController(
            IFrameDelayControl delayControl,
            NetworkBufferCapacityPolicyOptions? options = null)
        {
            _delayControl = delayControl ?? throw new ArgumentNullException(nameof(delayControl));
            Policy = new AdaptiveNetworkBufferCapacityPolicy(
                options ?? NetworkBufferCapacityPolicyOptions.InterpolationDelayDefault);
            TargetDelayFrames = _delayControl.DelayFrames;
        }

        public AdaptiveNetworkBufferCapacityPolicy Policy { get; }

        public int CurrentDelayFrames => _delayControl.DelayFrames;

        public int TargetDelayFrames { get; private set; }

        public bool Observe(NetworkBufferSizingSample sample)
        {
            if (!sample.HasRoundTripSample) return false;
            var target = Policy.GetTargetCapacity(sample, _delayControl.DelayFrames);
            TargetDelayFrames = target;
            return target != _delayControl.DelayFrames
                && _delayControl.TrySetDelayFrames(target);
        }
    }

    /// <summary>Converts the policy's frame target into a timeline-specific tick delay.</summary>
    public sealed class AdaptiveNetworkTimelineDelayController
    {
        private readonly ITimelineDelayControl _delayControl;

        public AdaptiveNetworkTimelineDelayController(
            ITimelineDelayControl delayControl,
            NetworkBufferCapacityPolicyOptions? options = null)
        {
            _delayControl = delayControl ?? throw new ArgumentNullException(nameof(delayControl));
            if (_delayControl.TicksPerSecond <= 0L)
                throw new ArgumentException("Timeline tick frequency must be positive.", nameof(delayControl));

            Policy = new AdaptiveNetworkBufferCapacityPolicy(
                options ?? NetworkBufferCapacityPolicyOptions.InterpolationDelayDefault);
        }

        public AdaptiveNetworkBufferCapacityPolicy Policy { get; }

        public long CurrentDelayTicks => _delayControl.DelayTicks;

        public int TargetDelayFrames { get; private set; }

        public long TargetDelayTicks { get; private set; }

        public bool Observe(NetworkBufferSizingSample sample)
        {
            if (!sample.HasRoundTripSample) return false;

            var currentFrames = ToFrames(
                _delayControl.TargetDelayTicks,
                _delayControl.TicksPerSecond,
                sample.TickRate);
            TargetDelayFrames = Policy.GetTargetCapacity(sample, currentFrames);
            TargetDelayTicks = ToTicks(
                TargetDelayFrames,
                _delayControl.TicksPerSecond,
                sample.TickRate);
            return TargetDelayTicks != _delayControl.TargetDelayTicks
                && _delayControl.TrySetDelayTicks(TargetDelayTicks);
        }

        private static int ToFrames(long delayTicks, long ticksPerSecond, int tickRate)
        {
            if (delayTicks <= 0L) return 0;
            var frames = Math.Ceiling(delayTicks * (double)tickRate / ticksPerSecond);
            return frames >= int.MaxValue ? int.MaxValue : (int)frames;
        }

        private static long ToTicks(int frames, long ticksPerSecond, int tickRate)
        {
            if (frames <= 0) return 0L;
            var ticks = Math.Ceiling(frames * (double)ticksPerSecond / tickRate);
            return ticks >= long.MaxValue ? long.MaxValue : (long)ticks;
        }
    }
}
