using System.Text.Json;
using AbilityKit.Benchmarking;
using Xunit;

namespace AbilityKit.Runtime.Benchmarks.Tests;

public sealed class BenchmarkRunnerTests
{
    [Fact]
    public void RunScenario_SeparatesLifecycleFromMeasuredOperations()
    {
        var scenario = new CountingScenario(operationsPerInvocation: 2);

        var result = BenchmarkRunner.RunScenario(
            scenario,
            new BenchmarkRunOptions(WarmupIterations: 1, MeasurementIterations: 4, InvocationsPerIteration: 3));

        Assert.Equal(1, scenario.SetupCount);
        Assert.Equal(5, scenario.IterationSetupCount);
        Assert.Equal(5, scenario.ExecuteCount);
        Assert.Equal(2, scenario.ValidateCount);
        Assert.Equal(1, scenario.CleanupCount);
        Assert.Equal(4, result.Samples.Count);
        Assert.All(result.Samples, sample => Assert.Equal(6, sample.Operations));
        Assert.Equal(24, result.Summary.TotalOperations);
        Assert.Equal(4, result.Summary.SampleCount);
        Assert.True(result.Summary.MeanNanosecondsPerOperation >= 0);
        Assert.True(result.Summary.P95NanosecondsPerOperation >= result.Summary.MedianNanosecondsPerOperation);
        Assert.True(result.Summary.P99NanosecondsPerOperation >= result.Summary.P95NanosecondsPerOperation);
        Assert.True(result.Summary.MaxNanosecondsPerOperation >= result.Summary.P99NanosecondsPerOperation);
        Assert.True(result.Summary.MeanAllocatedBytesPerOperation >= 0);
        Assert.Equal("executions=5", result.DeterminismDigest);
        Assert.Equal("measurement", result.Result);
    }

    [Fact]
    public void RunScenario_CleansUpWhenExecutionFails()
    {
        var scenario = new CountingScenario(operationsPerInvocation: 0);

        var exception = Assert.Throws<InvalidOperationException>(() => BenchmarkRunner.RunScenario(
            scenario,
            new BenchmarkRunOptions(WarmupIterations: 0, MeasurementIterations: 1, InvocationsPerIteration: 1)));

        Assert.Contains("completed no logical operations", exception.Message);
        Assert.Equal(1, scenario.CleanupCount);
    }

    [Fact]
    public void Report_JsonRoundTripPreservesStableSchemaAndSamples()
    {
        var report = BenchmarkRunner.Run(
            "test",
            new[] { new CountingScenario(operationsPerInvocation: 1) },
            new BenchmarkRunOptions(WarmupIterations: 0, MeasurementIterations: 2, InvocationsPerIteration: 1));

        var json = JsonSerializer.Serialize(report, BenchmarkRunner.JsonOptions);
        var restored = JsonSerializer.Deserialize<BenchmarkReport>(json, BenchmarkRunner.JsonOptions);

        Assert.NotNull(restored);
        Assert.Equal(BenchmarkReport.Schema, restored!.SchemaVersion);
        Assert.Equal("test", restored.Profile);
        Assert.Equal(BenchmarkRunner.MetricDefinitions.Count, restored.MetricDefinitions.Count);
        Assert.Single(restored.Results);
        Assert.Equal(2, restored.Results[0].Samples.Count);
        Assert.Equal(report.Results[0].DeterminismDigest, restored.Results[0].DeterminismDigest);
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 1, 0)]
    public void Options_RejectInvalidIterationCounts(int warmup, int measurement, int invocations)
    {
        var options = new BenchmarkRunOptions(warmup, measurement, invocations);

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    private sealed class CountingScenario : BenchmarkScenarioBase
    {
        private readonly int _operationsPerInvocation;

        public CountingScenario(int operationsPerInvocation)
        {
            _operationsPerInvocation = operationsPerInvocation;
        }

        public override BenchmarkDescriptor Descriptor { get; } = new(
            "test.counting",
            "test",
            "operation",
            new Dictionary<string, string>());

        public int SetupCount { get; private set; }

        public int IterationSetupCount { get; private set; }

        public int ExecuteCount { get; private set; }

        public int ValidateCount { get; private set; }

        public int CleanupCount { get; private set; }

        public override void Setup() => SetupCount++;

        public override void IterationSetup() => IterationSetupCount++;

        public override long Execute(int invocationCount)
        {
            ExecuteCount++;
            return (long)invocationCount * _operationsPerInvocation;
        }

        public override void Validate() => ValidateCount++;

        public override string GetDeterminismDigest() => $"executions={ExecuteCount}";

        public override void Cleanup() => CleanupCount++;
    }
}
