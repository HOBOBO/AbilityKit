using Xunit;
using Progress = AbilityKit.Pipeline.PipelineExecutionProgress<AbilityKit.Pipeline.Tests.PipelineExternalPhaseContractTests.TestContext, AbilityKit.Pipeline.PipelinePhaseProgress>;

namespace AbilityKit.Pipeline.Tests;

/// <summary>
/// 从独立测试程序集仅通过公开 API 验证阶段扩展契约。
/// </summary>
public sealed class PipelineExternalPhaseContractTests
{
    [Fact]
    public void Factory_isolates_overlapping_runs_and_keeps_template_unchanged()
    {
        var pipeline = new TestPipeline();
        var template = new CountingPhase("counter");
        pipeline.AddPhase(template);
        var first = pipeline.Start(new TestConfig(), new TestContext());
        IAbilityPipelineRun<TestContext>? second = null;
        try
        {
            first.Tick(0f);
            first.Tick(1f);
            var firstPhase = Assert.IsType<CountingPhase>(Assert.Single(first.Phases));
            second = pipeline.Start(new TestConfig(), new TestContext());
            var secondPhase = Assert.IsType<CountingPhase>(Assert.Single(second.Phases));
            Assert.NotSame(template, firstPhase);
            Assert.NotSame(template, secondPhase);
            Assert.NotSame(firstPhase, secondPhase);
            Assert.Equal(1, firstPhase.Count);
            Assert.Equal(0, secondPhase.Count);

            first.Pause();
            first.Tick(1f);
            second.Tick(0f);
            second.Tick(1f);
            second.Tick(1f);
            Assert.Equal(1, firstPhase.Count);
            Assert.Equal(2, secondPhase.Count);

            second.Cancel();
            second.Tick(0f);
            Assert.Equal(EAbilityPipelineState.Failed, second.State);
            Assert.Equal(1, firstPhase.Count);
            first.Resume();
            for (var index = 0; index < 3; index++) first.Tick(1f);
            Assert.Equal(EAbilityPipelineState.Completed, first.State);
            Assert.Equal(new long[] { 1, 2, 3, 4 }, first.Context.Ticks);
            Assert.Equal(0, template.Count);
            Assert.False(template.IsComplete);
        }
        finally
        {
            second?.Release();
            first.Release();
        }
    }

    [Fact]
    public void Custom_progress_resumes_in_a_new_run_without_replaying_entry_actions()
    {
        var pipeline = new TestPipeline();
        pipeline.AddPhase(new CountingPhase("counter"));
        var starts = 0;
        pipeline.Events.OnPipelineStart += _ => starts++;
        var config = new TestConfig();
        var original = pipeline.Start(config, new TestContext());
        IAbilityPipelineRun<TestContext>? resumed = null;
        try
        {
            original.Tick(0f);
            original.Tick(1f);
            original.Tick(1f);
            var saved = Progress.Capture(original, config, CapturePhase);
            original.Tick(1f);
            Assert.Equal(2, Assert.Single(Assert.Single(saved.Phases).Longs));

            var context = new TestContext();
            resumed = pipeline.AttachRun(config, context, run =>
                Progress.Restore(run, config, saved, ValidatePhase, RestorePhase));
            Assert.Equal(1, starts);
            Assert.Equal(0, context.Entries);
            Assert.Empty(context.Ticks);
            Assert.Equal(2, Assert.IsType<CountingPhase>(Assert.Single(resumed.Phases)).Count);

            resumed.Tick(1f);
            resumed.Tick(1f);
            original.Tick(1f);
            Assert.Equal(EAbilityPipelineState.Completed, original.State);
            Assert.Equal(original.State, resumed.State);
            Assert.Equal(original.Context.Ticks.Skip(2), context.Ticks);
            Assert.Equal(0, context.Entries);
            Assert.Equal(1, original.Context.Entries);
        }
        finally
        {
            resumed?.Release();
            original.Release();
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(5)]
    public void Invalid_custom_payload_is_rejected_before_any_progress_is_written(long count)
    {
        var pipeline = new TestPipeline();
        pipeline.AddPhase(new CountingPhase("counter"));
        pipeline.AddPhase(new CountingPhase("later"));
        var config = new TestConfig();
        var run = pipeline.Start(config, new TestContext());
        try
        {
            run.Tick(0f);
            run.Tick(1f);
            var saved = Progress.Capture(run, config, CapturePhase);
            saved.Phases[1].Longs[0] = count;
            run.Tick(1f);
            var writes = 0;
            var preparations = 0;
            Assert.Throws<InvalidOperationException>(() => Progress.Restore(run, config, saved,
                ValidatePhase, (phase, state) =>
                {
                    writes++;
                    return RestorePhase(phase, state);
                }, _ => preparations++));
            Assert.Equal(0, writes);
            Assert.Equal(0, preparations);
            Assert.Equal(2, Assert.IsType<CountingPhase>(run.Phases[0]).Count);
            Assert.Equal(0, Assert.IsType<CountingPhase>(run.Phases[1]).Count);
            Assert.Equal(0, run.CurrentPhaseIndex);
            Assert.True(run.HasCurrentPhase);
            Assert.Equal(1, run.Context.Entries);
        }
        finally
        {
            run.Release();
        }
    }

    private static PipelinePhaseProgress CapturePhase(IAbilityPipelinePhase<TestContext> phase)
    {
        var counter = Assert.IsType<CountingPhase>(phase);
        return new PipelinePhaseProgress
        {
            PhaseId = counter.PhaseId.Value,
            PhaseType = counter.GetType().FullName!,
            IsComplete = counter.IsComplete,
            Longs = new[] { counter.Count }
        };
    }

    private static bool ValidatePhase(IAbilityPipelinePhase<TestContext> phase, PipelinePhaseProgress state)
    {
        var counter = Assert.IsType<CountingPhase>(phase);
        if (state.Integers.Length != 0 || state.Numbers.Length != 0 || state.Longs.Length != 1 ||
            state.Longs[0] < 0 || state.Longs[0] > counter.Limit ||
            state.IsComplete != (state.Longs[0] == counter.Limit))
            throw new InvalidOperationException("Invalid counter progress.");
        return true;
    }

    private static bool RestorePhase(IAbilityPipelinePhase<TestContext> phase, PipelinePhaseProgress state)
    {
        var counter = Assert.IsType<CountingPhase>(phase);
        counter.RestoreCount(state.Longs[0], state.IsComplete);
        return true;
    }

    /// <summary>
    /// 模拟包外阶段：不可变规则随工厂复制，计数由每次运行独立持有。
    /// </summary>
    private sealed class CountingPhase(string id) : AbilityPipelinePhaseBase<TestContext>(id),
        IAbilityPipelinePhaseInstanceFactory<TestContext>
    {
        public int Limit => 4;
        public long Count { get; private set; }

        public IAbilityPipelinePhase<TestContext> CreateRunPhase() => new CountingPhase(PhaseId.Value);

        protected override void OnExecute(TestContext context) => context.Entries++;

        public override void OnUpdate(TestContext context, float deltaTime)
        {
            Count++;
            context.Ticks.Add(Count);
            if (Count == Limit) Complete(context);
        }

        public override void Reset()
        {
            base.Reset();
            Count = 0;
        }

        public void RestoreCount(long count, bool complete)
        {
            Count = count;
            SetComplete(complete);
        }
    }

    /// <summary>
    /// 记录正常生命周期产生的效果，恢复路径只写阶段进度。
    /// </summary>
    public sealed class TestContext : AAbilityPipelineContext
    {
        public int Entries { get; set; }
        public List<long> Ticks { get; } = new();
    }

    private sealed class TestPipeline : AbilityPipeline<TestContext>
    {
        protected override void ReleaseContext(TestContext context) { }
    }

    private sealed class TestConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 23;
        public string ConfigName => "external-counter";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }
}
