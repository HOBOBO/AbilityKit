using AbilityKit.Demo.Shooter.View;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Client;

public sealed class ShooterSyncPerformanceMetricsTests
{
    [Fact]
    public void DurationMetricReportsDistributionWithoutRetainingSamples()
    {
        var metric = new ShooterDurationMetric();
        for (var i = 1; i <= 100; i++)
        {
            metric.RecordMilliseconds(i);
        }

        var snapshot = metric.Capture();

        Assert.Equal(100, snapshot.SampleCount);
        Assert.Equal(50.5d, snapshot.AverageMilliseconds, 3);
        Assert.InRange(snapshot.P50Milliseconds, 50d, 51d);
        Assert.InRange(snapshot.P95Milliseconds, 95d, 96d);
        Assert.InRange(snapshot.P99Milliseconds, 99d, 100d);
        Assert.Equal(100d, snapshot.MaxMilliseconds, 3);
    }

    [Fact]
    public void FrameCollectorReportsHitchesStagesAndAllocations()
    {
        var collector = new ShooterSyncFramePerformanceCollector(hitchThresholdMilliseconds: 20d);

        collector.RecordFrame(
            MillisecondsToTicks(10d),
            MillisecondsToTicks(2d),
            MillisecondsToTicks(1d),
            MillisecondsToTicks(3d),
            MillisecondsToTicks(4d),
            allocatedBytes: 128L);
        collector.RecordFrame(
            MillisecondsToTicks(30d),
            MillisecondsToTicks(5d),
            MillisecondsToTicks(2d),
            MillisecondsToTicks(8d),
            MillisecondsToTicks(15d),
            allocatedBytes: 512L);

        var diagnostics = collector.Diagnostics;

        Assert.Equal(2, diagnostics.FrameCount);
        Assert.Equal(1, diagnostics.HitchCount);
        Assert.Equal(640, diagnostics.TotalAllocatedBytes);
        Assert.Equal(512, diagnostics.MaxAllocatedBytes);
        Assert.Equal(320d, diagnostics.AverageAllocatedBytes);
        Assert.InRange(diagnostics.Frame.P95Milliseconds, 30d, 30.5d);
        Assert.InRange(diagnostics.Launcher.MaxMilliseconds, 4.9d, 5.1d);
        Assert.InRange(diagnostics.ViewRender.MaxMilliseconds, 14.9d, 15.1d);
    }

    private static long MillisecondsToTicks(double milliseconds)
    {
        return (long)Math.Ceiling(milliseconds * System.Diagnostics.Stopwatch.Frequency / 1000d);
    }
}
