using AbilityKit.Pipeline;
using Xunit;
using Progress = AbilityKit.Pipeline.PipelineExecutionProgress<AbilityKit.Pipeline.Tests.PipelineExecutionProgressTests.TestContext, AbilityKit.Pipeline.PipelinePhaseProgress>;

namespace AbilityKit.Pipeline.Tests;

public sealed class PipelineExecutionProgressTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Composites_preserve_order_and_elapsed_values_without_reentering(bool race)
    {
        var condition = new CountingCondition();
        var choice = new AbilityConditionalPhase<TestContext>(new AbilityPipelinePhaseId("choice"));
        choice.AddBranch(condition, new AbilityDelayPhase<TestContext>(0.75f));
        AbilityCompositePhase<TestContext> group = race
            ? new AbilityRacePhase<TestContext>(new AbilityPipelinePhaseId("group"))
            : new AbilityParallelPhase<TestContext>(new AbilityPipelinePhaseId("group"));
        group.AddSubPhase(new AbilityDelayPhase<TestContext>(0.25f));
        group.AddSubPhase(choice);
        var sequence = new AbilitySequencePhase<TestContext>(new AbilityPipelinePhaseId("sequence"));
        sequence.AddSubPhase(group);
        sequence.AddSubPhase(new AbilityDelayPhase<TestContext>(2f));
        var pipeline = new TestPipeline();
        pipeline.AddPhase(sequence);
        var config = new TestConfig();
        var run = pipeline.Start(config, new TestContext());
        run.Tick(0f);
        run.Tick(race ? 0.125f : 0.25f);
        run.Pause();
        var saved = Progress.Capture(run, config);
        var groupState = Assert.Single(saved.Phases).Children[0];
        Assert.Equal(race ? new[] { 0, 0, 1 } : new[] { 0, 1 }, groupState.Integers);
        var choiceState = groupState.Children[1];
        Assert.Equal(0, choiceState.Integers[1]);
        Assert.Equal((int)EPipelineDebugConditionResult.Matched, choiceState.Integers[2]);

        run.Resume();
        run.Tick(0.125f);
        var expected = Progress.Capture(run, config);
        Assert.Equal(race ? 0.125 : 0.25, choiceState.Children[0].Numbers[0]);
        var checks = condition.Count;
        Progress.Restore(run, config, saved);
        Assert.Equal(checks, condition.Count);
        run.Tick(1f);
        AssertRun(saved, Progress.Capture(run, config));
        run.Resume();
        run.Tick(0.125f);
        AssertRun(expected, Progress.Capture(run, config));
        run.Release();
    }

    [Fact]
    public void Repeat_resumes_interval_and_replays_only_the_next_action()
    {
        var repeat = new AbilityRepeatPhase<TestContext>(3) { RepeatInterval = 0.5f };
        repeat.SetRepeatAction((context, index) => context.Actions.Add(index));
        var pipeline = new TestPipeline();
        pipeline.AddPhase(repeat);
        var config = new TestConfig();
        var context = new TestContext();
        var run = pipeline.Start(config, context);
        run.Tick(0f);
        run.Tick(0.25f);
        var saved = Progress.Capture(run, config);
        Assert.Equal(new[] { 1, 1, 0 }, saved.Phases[0].Integers);
        Assert.Equal(0.25, saved.Phases[0].Numbers[1]);
        run.Tick(0.125f);
        var next = Progress.Capture(run, config);
        run.Tick(0.125f);
        var fired = Progress.Capture(run, config);
        Assert.Equal(new[] { 0, 1 }, context.Actions);

        var restoredContext = new TestContext();
        var restored = pipeline.AttachRun(config, restoredContext, attached => Progress.Restore(attached, config, saved));
        Assert.Empty(restoredContext.Actions);
        restored.Tick(0.125f);
        Assert.Empty(restoredContext.Actions);
        AssertRun(next, Progress.Capture(restored, config));
        restored.Tick(0.125f);
        Assert.Equal(new[] { 1 }, restoredContext.Actions);
        AssertRun(fired, Progress.Capture(restored, config));
        restored.Release();
        run.Release();
    }

    [Fact]
    public void Invalid_late_child_does_not_run_prepare_or_change_any_progress()
    {
        var pipeline = new TestPipeline();
        pipeline.AddPhase(new AbilityDelayPhase<TestContext>(1f));
        pipeline.AddPhase(new AbilityDelayPhase<TestContext>(2f));
        var config = new TestConfig();
        var run = pipeline.Start(config, new TestContext());
        run.Tick(0f);
        var corrupt = Progress.Capture(run, config);
        corrupt.Phases[1].PhaseId = "wrong";
        run.Tick(0.25f);
        var before = Progress.Capture(run, config);
        var prepared = false;
        Assert.Throws<InvalidOperationException>(() => Progress.Restore(run, config, corrupt,
            prepare: _ => prepared = true));
        Assert.False(prepared);
        AssertRun(before, Progress.Capture(run, config));
        run.Release();
    }

    private static void AssertRun(PipelineRunProgress<PipelinePhaseProgress> expected,
        PipelineRunProgress<PipelinePhaseProgress> actual)
    {
        Assert.Equal(expected.CurrentPhaseIndex, actual.CurrentPhaseIndex);
        Assert.Equal(expected.HasCurrentPhase, actual.HasCurrentPhase);
        Assert.Equal(expected.IsPaused, actual.IsPaused);
        Assert.Equal(expected.IsCancelled, actual.IsCancelled);
        Assert.Equal(expected.Phases.Length, actual.Phases.Length);
        for (var index = 0; index < expected.Phases.Length; index++) AssertNode(expected.Phases[index], actual.Phases[index]);
    }

    private static void AssertNode(PipelinePhaseProgress expected, PipelinePhaseProgress actual)
    {
        Assert.Equal(expected.PhaseId, actual.PhaseId);
        Assert.Equal(expected.PhaseType, actual.PhaseType);
        Assert.Equal(expected.IsComplete, actual.IsComplete);
        Assert.Equal(expected.Integers, actual.Integers);
        Assert.Equal(expected.Numbers, actual.Numbers);
        Assert.Equal(expected.Children.Length, actual.Children.Length);
        for (var index = 0; index < expected.Children.Length; index++) AssertNode(expected.Children[index], actual.Children[index]);
    }

    public sealed class TestContext : AAbilityPipelineContext
    {
        public List<int> Actions { get; } = new();
    }

    private sealed class TestPipeline : AbilityPipeline<TestContext>
    {
        protected override void ReleaseContext(TestContext context) { }
    }

    private sealed class CountingCondition : IAbilityConditionNode
    {
        public EConditionCheckStrategy CheckStrategy => EConditionCheckStrategy.OnEnter;
        public int Count { get; private set; }
        public bool Evaluate(IAbilityPipelineContext context)
        {
            Count++;
            return true;
        }
    }

    private sealed class TestConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 17;
        public string ConfigName => "progress";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }
}
