using System.Text.Json;
using AbilityKit.Demo.Shooter.AoiLodBenchmarks;
using Xunit;

namespace AbilityKit.Demo.Shooter.AoiLodBenchmarks.Tests;

public sealed class BenchmarkRunnerTests
{
    [Fact]
    public void FullMatrix_ExpandsAllEntityObserverScenarioCombinations()
    {
        var cases = BenchmarkOptions.ExpandFullMatrix();

        Assert.Equal(18, cases.Count);
        Assert.Equal(new[] { 100, 1000, 10000 }, cases.Select(item => item.Entities).Distinct().Order().ToArray());
        Assert.Equal(new[] { 1, 16, 64 }, cases.Select(item => item.Observers).Distinct().Order().ToArray());
        Assert.All(
            from entities in new[] { 100, 1000, 10000 }
            from observers in new[] { 1, 16, 64 }
            select (entities, observers),
            pair => Assert.Equal(2, cases.Count(item => item.Entities == pair.entities && item.Observers == pair.observers)));
    }

    [Theory]
    [InlineData(BenchmarkScenario.Steady)]
    [InlineData(BenchmarkScenario.Churn)]
    public void FixedSeed_ProducesDeterministicFunctionalMetrics(BenchmarkScenario scenario)
    {
        var benchmarkCase = new BenchmarkCase(100, 1, scenario);
        var options = FastOptions();

        var first = ShooterAoiLodBenchmarkRunner.RunCase(benchmarkCase, options);
        var second = ShooterAoiLodBenchmarkRunner.RunCase(benchmarkCase, options);

        Assert.Equal(first.PayloadBytesPerTick, second.PayloadBytesPerTick);
        Assert.Equal(first.EnterCount, second.EnterCount);
        Assert.Equal(first.LeaveCount, second.LeaveCount);
        Assert.Equal(first.StarvedEntitiesAtEnd, second.StarvedEntitiesAtEnd);
        Assert.Equal(first.MaxUnsentTicks, second.MaxUnsentTicks);
        Assert.Equal(first.DeterminismDigest, second.DeterminismDigest);
    }

    [Fact]
    public void Aggregation_ReportsNormalizedAndDerivedMetrics()
    {
        var metrics = ShooterAoiLodBenchmarkRunner.RunCase(
            new BenchmarkCase(100, 1, BenchmarkScenario.Steady),
            FastOptions());

        Assert.True(metrics.MeanTickMilliseconds > 0);
        Assert.True(metrics.MedianTickMilliseconds > 0);
        Assert.True(metrics.NormalizedNanosecondsPerEntityObserver > 0);
        Assert.True(metrics.ThreadAllocatedBytesPerTick >= 0);
        Assert.True(metrics.AllocatedBytesPerEntityObserver >= 0);
        Assert.InRange(
            metrics.ThreadAllocatedBytesPerTick -
            (metrics.ExportAllocatedBytesPerTick + metrics.SerializationAllocatedBytesPerTick),
            0,
            1);
        Assert.Equal(0, metrics.ObservationAllocatedBytesPerTick);
        Assert.True(metrics.ExportMillisecondsPerTick > 0);
        Assert.True(metrics.SerializationMillisecondsPerTick > 0);
        Assert.Equal(0, metrics.ThreadAllocatedBytesPerTick);
        Assert.Equal(metrics.EnterCount + metrics.LeaveCount, metrics.ChangedCount);
    }

    [Fact]
    public void ThresholdEvaluator_FailsCpuAndDeterministicMetricsAboveLimits()
    {
        var benchmarkCase = new BenchmarkCase(100, 1, BenchmarkScenario.Steady);
        var metrics = new BenchmarkMetrics
        {
            MeanTickMilliseconds = 1,
            MedianTickMilliseconds = 1,
            NormalizedNanosecondsPerEntityObserver = 101,
            ThreadAllocatedBytesPerTick = 100,
            AllocatedBytesPerEntityObserver = 2,
            PayloadBytesPerTick = 1001,
            EnterCount = 1,
            LeaveCount = 0,
            StarvedEntitiesAtEnd = 2,
            MaxUnsentTicks = 3,
            DeterminismDigest = 1
        };
        var thresholds = new Thresholds
        {
            Tier = "test",
            MaxNormalizedNanoseconds = 100,
            MaxAllocatedBytesPerEntityObserver = 1,
            MaxPayloadBytesPerObserverTick = 1000,
            MaxChurnFractionPerTick = 1,
            MaxStarvedEntitiesPerObserver = 1,
            MaxUnsentTicks = 2
        };

        var failures = BenchmarkThresholdEvaluator.Evaluate(benchmarkCase, metrics, FastOptions(), thresholds);

        Assert.Contains(failures, failure => failure.Metric == "cpu.normalizedNanoseconds");
        Assert.Contains(failures, failure => failure.Metric == "allocation.bytesPerEntityObserver");
        Assert.Contains(failures, failure => failure.Metric == "payload.bytesPerObserverTick");
        Assert.Contains(failures, failure => failure.Metric == "aoi.churnFractionPerTick");
        Assert.Contains(failures, failure => failure.Metric == "starvation.entitiesPerObserver");
        Assert.Contains(failures, failure => failure.Metric == "starvation.maxUnsentTicks");
    }

    [Fact]
    public void RealCases_TrackPayloadChurnAndStarvation()
    {
        var options = FastOptions() with { EntityBudget = 8 };
        var steady = ShooterAoiLodBenchmarkRunner.RunCase(new BenchmarkCase(100, 1, BenchmarkScenario.Steady), options);
        var churn = ShooterAoiLodBenchmarkRunner.RunCase(new BenchmarkCase(100, 1, BenchmarkScenario.Churn), options);

        Assert.True(steady.PayloadBytesPerTick > 0);
        Assert.Equal(0, steady.ChangedCount);
        Assert.True(churn.ChangedCount > 0);
        Assert.InRange(steady.MaxUnsentTicks, 0, options.MeasurementIterations * options.TicksPerIteration);
        Assert.InRange(churn.MaxUnsentTicks, 0, options.MeasurementIterations * options.TicksPerIteration);
    }

    [Fact]
    public void Report_IsMachineReadableAndPreservesMetricDefinitions()
    {
        var report = ShooterAoiLodBenchmarkRunner.Run(
            "test",
            new[] { new BenchmarkCase(100, 1, BenchmarkScenario.Steady) },
            FastOptions(),
            Thresholds.Smoke);

        var json = JsonSerializer.Serialize(report, ShooterAoiLodBenchmarkRunner.JsonOptions);
        var restored = JsonSerializer.Deserialize<BenchmarkReport>(json, ShooterAoiLodBenchmarkRunner.JsonOptions);

        Assert.NotNull(restored);
        Assert.Equal(BenchmarkReport.Schema, restored!.SchemaVersion);
        Assert.Equal(6, restored.MetricDefinitions.Count);
        Assert.Single(restored.Results);
        Assert.Equal(report.Results[0].Metrics.DeterminismDigest, restored.Results[0].Metrics.DeterminismDigest);
    }

    private static BenchmarkOptions FastOptions() => new()
    {
        Seed = 0x5A17,
        WarmupIterations = 1,
        MeasurementIterations = 2,
        TicksPerIteration = 6,
        EntityBudget = 32
    };
}
