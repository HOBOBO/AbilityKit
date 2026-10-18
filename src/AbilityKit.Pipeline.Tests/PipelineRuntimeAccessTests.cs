using Xunit;

namespace AbilityKit.Pipeline.Tests;

public sealed class PipelineRuntimeAccessTests
{
    [Fact]
    public void Attach_initializes_phase_progress_without_start_events_or_replaying_actions()
    {
        var pipeline = new TestPipeline();
        var actions = 0;
        var starts = 0;
        pipeline.Events.OnPipelineStart += _ => starts++;
        pipeline.AddPhase(new AbilityActionPhase<TestContext>(new AbilityPipelinePhaseId("before"), _ => actions++));
        pipeline.AddPhase(new AbilityDelayPhase<TestContext>(new AbilityPipelinePhaseId("delay"), 2f));
        pipeline.AddPhase(new AbilityActionPhase<TestContext>(new AbilityPipelinePhaseId("after"), _ => actions++));

        var original = pipeline.Start(new TestConfig(), new TestContext());
        original.Tick(0f);
        original.Tick(1f);
        Assert.Equal(1, actions);
        var elapsed = Assert.IsType<AbilityDelayPhase<TestContext>>(original.Phases[1]).ElapsedTime;
        original.Release();

        var attached = pipeline.AttachRun(new TestConfig(), new TestContext(), run =>
        {
            Assert.IsType<AbilityDelayPhase<TestContext>>(run.Phases[1]).ElapsedTime = elapsed;
            run.SetExecutionPosition(1, true, false, false);
        });
        Assert.Equal(1, starts);
        attached.Tick(1f);
        Assert.Equal(EAbilityPipelineState.Completed, attached.State);
        Assert.Equal(2, actions);
    }

    [Fact]
    public void Run_exposes_distinct_composite_phase_instances()
    {
        var pipeline = new TestPipeline();
        var parallel = new AbilityParallelPhase<TestContext>(new AbilityPipelinePhaseId("parallel"));
        parallel.AddSubPhase(new AbilityDelayPhase<TestContext>(1f));
        parallel.AddSubPhase(new AbilityDelayPhase<TestContext>(2f));
        pipeline.AddPhase(parallel);

        var first = pipeline.Start(new TestConfig(), new TestContext());
        var second = pipeline.Start(new TestConfig(), new TestContext());
        Assert.NotSame(first.Phases[0], second.Phases[0]);
        first.Tick(0f);
        first.Tick(1f);
        var running = Assert.IsType<AbilityParallelPhase<TestContext>>(first.Phases[0]);
        Assert.Single(running.ActivePhaseIndices);
        Assert.Equal(1, running.ActivePhaseIndices[0]);
        Assert.Empty(Assert.IsType<AbilityParallelPhase<TestContext>>(second.Phases[0]).ActivePhaseIndices);
        first.Release();
        second.Release();
    }

    [Fact]
    public void Failed_attach_does_not_publish_start_event()
    {
        var pipeline = new TestPipeline();
        pipeline.AddPhase(new AbilityDelayPhase<TestContext>(1f));
        var starts = 0;
        pipeline.Events.OnPipelineStart += _ => starts++;
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.AttachRun(
            new TestConfig(), new TestContext(), run => run.SetExecutionPosition(2, true, false, false)));
        Assert.Equal(0, starts);
    }

    private sealed class TestPipeline : AbilityPipeline<TestContext>
    {
        protected override void ReleaseContext(TestContext context) { }
    }

    private sealed class TestContext : AAbilityPipelineContext;

    private sealed class TestConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 1;
        public string ConfigName => "runtime-access-test";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }
}
