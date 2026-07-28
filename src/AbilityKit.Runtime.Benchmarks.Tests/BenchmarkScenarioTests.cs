using AbilityKit.Benchmarking;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using Xunit;

namespace AbilityKit.Runtime.Benchmarks.Tests;

public sealed class BenchmarkScenarioTests
{
    private static readonly BenchmarkRunOptions SingleSample = new(0, 1, 1);

    [Fact]
    public void Arguments_ParseFiltersAndOverrides()
    {
        var arguments = BenchmarkArguments.Parse(new[]
        {
            "--profile", "full",
            "--module", "pipeline",
            "--case", "active-runs",
            "--output", "result.json",
            "--warmup", "3",
            "--measurement", "7",
            "--invocations", "11"
        });

        Assert.Equal("full", arguments.Profile);
        Assert.Equal("pipeline", arguments.Module);
        Assert.Equal("active-runs", arguments.CaseFilter);
        Assert.Equal("result.json", arguments.Output);
        Assert.Equal(new BenchmarkRunOptions(3, 7, 11), arguments.CreateOptions());
        Assert.True(arguments.Matches(new PipelineActiveRunsScenario(10)));
        Assert.False(arguments.Matches(new PipelineSynchronousScenario(2)));
        Assert.False(arguments.Matches(new TriggerDispatchScenario(1, false, false)));
    }

    [Fact]
    public void Catalog_HasStableUniqueIdsAndExpectedModules()
    {
        var smoke = BenchmarkScenarioCatalog.Create("smoke");
        var full = BenchmarkScenarioCatalog.Create("full");

        Assert.Equal(7, smoke.Count);
        Assert.Equal(12, full.Count);
        Assert.Equal(smoke.Count, smoke.Select(item => item.Descriptor.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(full.Count, full.Select(item => item.Descriptor.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(new[] { "pipeline", "triggering" }, smoke.Select(item => item.Descriptor.Module).Distinct().Order().ToArray());
        Assert.All(smoke, item => Assert.NotEmpty(item.Descriptor.Workload));
    }

    [Theory]
    [InlineData("--unknown", "value")]
    [InlineData("--module", "other")]
    public void Arguments_RejectUnsupportedInput(string name, string value)
    {
        Assert.Throws<ArgumentException>(() => BenchmarkArguments.Parse(new[] { name, value }));
    }

    [Fact]
    public void PipelineSynchronousScenario_CompletesAllPhasesAndUnregistersRuns()
    {
        var result = BenchmarkRunner.RunScenario(new PipelineSynchronousScenario(phaseCount: 2, batchSize: 2), SingleSample);

        Assert.Equal(4, result.Summary.TotalOperations);
        Assert.Equal("runs=2;phases=4", result.DeterminismDigest);
    }

    [Fact]
    public void PipelineActiveRunsScenario_KeepsRunsActiveAcrossTicks()
    {
        var result = BenchmarkRunner.RunScenario(new PipelineActiveRunsScenario(activeRunCount: 10), SingleSample);

        Assert.Equal(10, result.Summary.TotalOperations);
        Assert.Equal("active=10;ticks=10", result.DeterminismDigest);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void TriggerDispatchScenario_ExecutesEveryFanout(bool queued, bool reuseControl)
    {
        var result = BenchmarkRunner.RunScenario(
            new TriggerDispatchScenario(fanout: 4, queued, reuseControl, batchSize: 2),
            SingleSample);

        Assert.Equal(8, result.Summary.TotalOperations);
        Assert.StartsWith("executions=8;checksum=", result.DeterminismDigest);
    }

    [Fact]
    public void TriggerRunner_CustomCueStillReceivesLifecycleCallbacks()
    {
        var eventBus = new EventBus();
        var runner = new TriggerRunner<TriggerBenchmarkContext>(eventBus, new FunctionRegistry(), new ActionRegistry());
        var cue = new CountingCue();
        var trigger = new CueTrigger(cue);
        var key = new EventKey<BenchmarkTriggerEvent>(42);
        using var registration = runner.Register(key, trigger);
        var evt = new BenchmarkTriggerEvent(7);

        eventBus.Publish(key, in evt, new ExecutionControl());

        Assert.Equal(1, trigger.ExecuteCount);
        Assert.Equal(1, cue.ConditionPassedCount);
        Assert.Equal(1, cue.BeforeActionCount);
        Assert.Equal(1, cue.ExecutedCount);
    }

    private sealed class CueTrigger : ITrigger<BenchmarkTriggerEvent, TriggerBenchmarkContext>
    {
        public CueTrigger(ITriggerCue cue)
        {
            Cue = cue;
        }

        public ITriggerCue Cue { get; }

        public int ExecuteCount { get; private set; }

        public bool Evaluate(in BenchmarkTriggerEvent args, in ExecCtx<TriggerBenchmarkContext> ctx) => true;

        public void Execute(in BenchmarkTriggerEvent args, in ExecCtx<TriggerBenchmarkContext> ctx) => ExecuteCount++;
    }

    private sealed class CountingCue : ITriggerCue
    {
        public int ConditionPassedCount { get; private set; }

        public int BeforeActionCount { get; private set; }

        public int ExecutedCount { get; private set; }

        public void OnConditionPassed(in TriggerCueContext context) => ConditionPassedCount++;

        public void OnConditionFailed(in TriggerCueContext context)
        {
        }

        public void OnBeforeAction(in TriggerCueContext context, int actionIndex) => BeforeActionCount++;

        public void OnExecuted(in TriggerCueContext context) => ExecutedCount++;

        public void OnInterrupted(in TriggerCueContext context)
        {
        }

        public void OnSkipped(in TriggerCueContext context)
        {
        }
    }
}
