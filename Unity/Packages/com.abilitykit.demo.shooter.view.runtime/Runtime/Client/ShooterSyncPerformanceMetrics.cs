#nullable enable

using System;
using System.Diagnostics;
using System.Threading;

namespace AbilityKit.Demo.Shooter.View
{
    public readonly struct ShooterDurationMetricSnapshot
    {
        public ShooterDurationMetricSnapshot(
            long sampleCount,
            double averageMilliseconds,
            double p50Milliseconds,
            double p95Milliseconds,
            double p99Milliseconds,
            double maxMilliseconds)
        {
            SampleCount = sampleCount;
            AverageMilliseconds = averageMilliseconds;
            P50Milliseconds = p50Milliseconds;
            P95Milliseconds = p95Milliseconds;
            P99Milliseconds = p99Milliseconds;
            MaxMilliseconds = maxMilliseconds;
        }

        public long SampleCount { get; }
        public double AverageMilliseconds { get; }
        public double P50Milliseconds { get; }
        public double P95Milliseconds { get; }
        public double P99Milliseconds { get; }
        public double MaxMilliseconds { get; }
    }

    /// <summary>
    /// Fixed-size, allocation-free-on-record histogram for sync hot-path timings.
    /// Percentiles are quantized to 0.5 ms and values above 2048 ms share the last bucket.
    /// </summary>
    public sealed class ShooterDurationMetric
    {
        private const long BucketMicroseconds = 500L;
        private const int BucketCount = 4097;
        private readonly long[] _buckets = new long[BucketCount];
        private long _sampleCount;
        private long _totalMicroseconds;
        private long _maxMicroseconds;

        public void RecordElapsedTicks(long elapsedTicks)
        {
            if (elapsedTicks <= 0L)
            {
                RecordMicroseconds(0L);
                return;
            }

            RecordMicroseconds(elapsedTicks * 1_000_000L / Stopwatch.Frequency);
        }

        public void RecordMilliseconds(double milliseconds)
        {
            RecordMicroseconds(milliseconds <= 0d ? 0L : (long)Math.Ceiling(milliseconds * 1000d));
        }

        public ShooterDurationMetricSnapshot Capture()
        {
            var count = Interlocked.Read(ref _sampleCount);
            if (count <= 0L)
            {
                return default;
            }

            return new ShooterDurationMetricSnapshot(
                count,
                Interlocked.Read(ref _totalMicroseconds) / (count * 1000d),
                PercentileMilliseconds(count, 50),
                PercentileMilliseconds(count, 95),
                PercentileMilliseconds(count, 99),
                Interlocked.Read(ref _maxMicroseconds) / 1000d);
        }

        public void Reset()
        {
            Array.Clear(_buckets, 0, _buckets.Length);
            Interlocked.Exchange(ref _sampleCount, 0L);
            Interlocked.Exchange(ref _totalMicroseconds, 0L);
            Interlocked.Exchange(ref _maxMicroseconds, 0L);
        }

        private void RecordMicroseconds(long microseconds)
        {
            var value = Math.Max(0L, microseconds);
            var bucket = (int)Math.Min(BucketCount - 1L, value / BucketMicroseconds);
            Interlocked.Increment(ref _buckets[bucket]);
            Interlocked.Increment(ref _sampleCount);
            Interlocked.Add(ref _totalMicroseconds, value);
            UpdateMax(value);
        }

        private double PercentileMilliseconds(long count, int percentile)
        {
            var target = Math.Max(1L, (count * percentile + 99L) / 100L);
            var cumulative = 0L;
            for (var i = 0; i < _buckets.Length; i++)
            {
                cumulative += Interlocked.Read(ref _buckets[i]);
                if (cumulative >= target)
                {
                    return (i + 1L) * BucketMicroseconds / 1000d;
                }
            }

            return BucketCount * BucketMicroseconds / 1000d;
        }

        private void UpdateMax(long value)
        {
            var observed = Interlocked.Read(ref _maxMicroseconds);
            while (value > observed)
            {
                var previous = Interlocked.CompareExchange(ref _maxMicroseconds, value, observed);
                if (previous == observed)
                {
                    return;
                }

                observed = previous;
            }
        }
    }

    public readonly struct ShooterSyncFramePerformanceDiagnostics
    {
        public ShooterSyncFramePerformanceDiagnostics(
            long frameCount,
            long hitchCount,
            long totalAllocatedBytes,
            long maxAllocatedBytes,
            in ShooterDurationMetricSnapshot frame,
            in ShooterDurationMetricSnapshot launcher,
            in ShooterDurationMetricSnapshot sessionTick,
            in ShooterDurationMetricSnapshot presentationBuild,
            in ShooterDurationMetricSnapshot viewRender)
        {
            FrameCount = frameCount;
            HitchCount = hitchCount;
            TotalAllocatedBytes = totalAllocatedBytes;
            MaxAllocatedBytes = maxAllocatedBytes;
            Frame = frame;
            Launcher = launcher;
            SessionTick = sessionTick;
            PresentationBuild = presentationBuild;
            ViewRender = viewRender;
        }

        public long FrameCount { get; }
        public long HitchCount { get; }
        public long TotalAllocatedBytes { get; }
        public long MaxAllocatedBytes { get; }
        public double AverageAllocatedBytes => FrameCount > 0L ? TotalAllocatedBytes / (double)FrameCount : 0d;
        public ShooterDurationMetricSnapshot Frame { get; }
        public ShooterDurationMetricSnapshot Launcher { get; }
        public ShooterDurationMetricSnapshot SessionTick { get; }
        public ShooterDurationMetricSnapshot PresentationBuild { get; }
        public ShooterDurationMetricSnapshot ViewRender { get; }
    }

    public sealed class ShooterSyncFramePerformanceCollector
    {
        private readonly long _hitchThresholdTicks;
        private readonly ShooterDurationMetric _frame = new();
        private readonly ShooterDurationMetric _launcher = new();
        private readonly ShooterDurationMetric _sessionTick = new();
        private readonly ShooterDurationMetric _presentationBuild = new();
        private readonly ShooterDurationMetric _viewRender = new();
        private long _frameCount;
        private long _hitchCount;
        private long _totalAllocatedBytes;
        private long _maxAllocatedBytes;

        public ShooterSyncFramePerformanceCollector(double hitchThresholdMilliseconds = 33.333d)
        {
            if (hitchThresholdMilliseconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(hitchThresholdMilliseconds));
            }

            _hitchThresholdTicks = Math.Max(
                1L,
                (long)Math.Ceiling(hitchThresholdMilliseconds * Stopwatch.Frequency / 1000d));
        }

        public ShooterSyncFramePerformanceDiagnostics Diagnostics
        {
            get
            {
                var frame = _frame.Capture();
                var launcher = _launcher.Capture();
                var sessionTick = _sessionTick.Capture();
                var presentationBuild = _presentationBuild.Capture();
                var viewRender = _viewRender.Capture();
                return new ShooterSyncFramePerformanceDiagnostics(
                    Interlocked.Read(ref _frameCount),
                    Interlocked.Read(ref _hitchCount),
                    Interlocked.Read(ref _totalAllocatedBytes),
                    Interlocked.Read(ref _maxAllocatedBytes),
                    in frame,
                    in launcher,
                    in sessionTick,
                    in presentationBuild,
                    in viewRender);
            }
        }

        public void RecordFrame(
            long frameElapsedTicks,
            long launcherElapsedTicks,
            long sessionTickElapsedTicks,
            long presentationBuildElapsedTicks,
            long viewRenderElapsedTicks,
            long allocatedBytes)
        {
            _frame.RecordElapsedTicks(frameElapsedTicks);
            _launcher.RecordElapsedTicks(launcherElapsedTicks);
            _sessionTick.RecordElapsedTicks(sessionTickElapsedTicks);
            _presentationBuild.RecordElapsedTicks(presentationBuildElapsedTicks);
            _viewRender.RecordElapsedTicks(viewRenderElapsedTicks);
            Interlocked.Increment(ref _frameCount);
            if (frameElapsedTicks >= _hitchThresholdTicks)
            {
                Interlocked.Increment(ref _hitchCount);
            }

            var allocation = Math.Max(0L, allocatedBytes);
            Interlocked.Add(ref _totalAllocatedBytes, allocation);
            UpdateMaxAllocatedBytes(allocation);
        }

        public void Reset()
        {
            _frame.Reset();
            _launcher.Reset();
            _sessionTick.Reset();
            _presentationBuild.Reset();
            _viewRender.Reset();
            Interlocked.Exchange(ref _frameCount, 0L);
            Interlocked.Exchange(ref _hitchCount, 0L);
            Interlocked.Exchange(ref _totalAllocatedBytes, 0L);
            Interlocked.Exchange(ref _maxAllocatedBytes, 0L);
        }

        private void UpdateMaxAllocatedBytes(long value)
        {
            var observed = Interlocked.Read(ref _maxAllocatedBytes);
            while (value > observed)
            {
                var previous = Interlocked.CompareExchange(ref _maxAllocatedBytes, value, observed);
                if (previous == observed)
                {
                    return;
                }

                observed = previous;
            }
        }
    }
}
