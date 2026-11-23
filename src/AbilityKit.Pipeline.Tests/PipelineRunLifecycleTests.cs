using Xunit;

namespace AbilityKit.Pipeline.Tests;

public sealed class PipelineRunLifecycleTests
{
    [Fact]
    public void Successful_run_notifies_completion_before_releasing_context_and_unregistering()
    {
        var fixture = new RunFixture();
        fixture.Pipeline.AddPhase(new AbilityActionPhase<TestContext>(new AbilityPipelinePhaseId("action"),
            _ => fixture.Journal.Add("action")));

        var run = fixture.Start();
        Assert.Equal(new[] { "start", "register", "trace:RunStart" }, fixture.Journal);
        Assert.Same(run, Assert.Single(fixture.Registry.GetActiveOwners()));

        run.Tick(0f);
        run.Tick(1f);
        run.Release();

        Assert.Equal(new[]
        {
            "start", "register", "trace:RunStart", "phase-start", "trace:PhaseStart", "action",
            "phase-complete", "trace:PhaseComplete", "complete", "trace:RunEnd", "release-context", "unregister"
        }, fixture.Journal);
        fixture.AssertTerminal(run, EAbilityPipelineState.Completed);
        Assert.Same(fixture.Context, Assert.Single(fixture.Pipeline.ReleasedContexts));
    }

    [Fact]
    public void Execution_failure_reports_the_original_error_and_closes_the_run_once()
    {
        var fixture = new RunFixture();
        var failure = new InvalidOperationException("execute failed");
        fixture.Pipeline.AddPhase(new AbilityActionPhase<TestContext>(new AbilityPipelinePhaseId("throw"),
            _ => throw failure));
        fixture.Pipeline.AddPhase(new AbilityActionPhase<TestContext>(new AbilityPipelinePhaseId("unreached"),
            _ => fixture.Journal.Add("unexpected-next-phase")));

        var run = fixture.Start();
        fixture.Journal.Clear();
        run.Tick(0f);
        run.Tick(1f);
        run.Release();

        Assert.Equal(new[]
        {
            "phase-start", "trace:PhaseStart", "phase-error", "error", "failed",
            "trace:PhaseError", "trace:RunEnd", "release-context", "unregister"
        }, fixture.Journal);
        Assert.Same(failure, Assert.Single(fixture.PhaseErrors));
        Assert.Same(failure, Assert.Single(fixture.Errors));
        Assert.Same(failure, Assert.Single(fixture.Failures));
        fixture.AssertTerminal(run, EAbilityPipelineState.Failed);
        Assert.Same(fixture.Context, Assert.Single(fixture.Pipeline.ReleasedContexts));
    }

    [Fact]
    public void Active_phase_update_and_error_handler_failures_are_reported_together()
    {
        var fixture = new RunFixture();
        var updateError = new InvalidOperationException("update failed");
        var handlerError = new InvalidOperationException("handler failed");
        var phase = new ActivePhase("active", fixture.Journal, updateError, handlerError);
        fixture.Pipeline.AddPhase(phase);
        var run = fixture.Start();
        run.Tick(0f);
        Assert.Equal(EAbilityPipelineState.Executing, run.State);
        Assert.True(run.HasCurrentPhase);
        fixture.Journal.Clear();

        run.Tick(0.5f);
        run.Tick(0.5f);

        Assert.Equal(new[]
        {
            "update:active", "handle-error:active", "phase-error", "error", "failed",
            "trace:PhaseError", "trace:RunEnd", "release-context", "unregister"
        }, fixture.Journal);
        var failure = Assert.IsType<AggregateException>(Assert.Single(fixture.Failures));
        Assert.Collection(failure.InnerExceptions, error => Assert.Same(updateError, error),
            error => Assert.Same(handlerError, error));
        Assert.Same(failure, Assert.Single(fixture.PhaseErrors));
        Assert.Same(failure, Assert.Single(fixture.Errors));
        fixture.AssertTerminal(run, EAbilityPipelineState.Failed);
        Assert.Same(fixture.Context, Assert.Single(fixture.Pipeline.ReleasedContexts));
    }

    [Fact]
    public void Pause_and_resume_preserve_the_active_phase_without_reentering_it()
    {
        var fixture = new RunFixture();
        fixture.Pipeline.AddPhase(new ActivePhase("active", fixture.Journal));
        var run = fixture.Start();
        run.Tick(0f);
        Assert.Equal(new AbilityPipelinePhaseId("active"), run.CurrentPhaseId);
        fixture.Journal.Clear();

        run.Pause();
        run.Pause();
        run.Tick(1f);

        Assert.True(run.IsPaused);
        Assert.True(fixture.Context.IsPaused);
        Assert.True(run.HasCurrentPhase);
        Assert.Equal(new[] { "pause", "trace:Pause" }, fixture.Journal);

        run.Resume();
        run.Resume();
        run.Tick(0.25f);

        Assert.False(run.IsPaused);
        Assert.False(fixture.Context.IsPaused);
        Assert.Equal(new AbilityPipelinePhaseId("active"), run.CurrentPhaseId);
        Assert.Equal(new[] { "pause", "trace:Pause", "resume", "trace:Resume", "update:active" }, fixture.Journal);
        run.Release();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_waits_for_tick_and_does_not_invoke_phase_interruption(bool paused)
    {
        var fixture = new RunFixture();
        fixture.Pipeline.AddPhase(new ActivePhase("active", fixture.Journal));
        var run = fixture.Start();
        run.Tick(0f);
        if (paused) run.Pause();
        fixture.Journal.Clear();

        run.Cancel();
        Assert.True(run.IsCancelled);
        Assert.Equal(EAbilityPipelineState.Executing, run.State);
        Assert.Empty(fixture.Journal);
        Assert.Same(run, Assert.Single(fixture.Registry.GetActiveOwners()));

        run.Tick(1f);
        run.Tick(1f);

        Assert.Equal(new[] { "error", "failed", "trace:RunEnd", "release-context", "unregister" }, fixture.Journal);
        Assert.Null(Assert.Single(fixture.Errors));
        Assert.Single(fixture.Failures);
        fixture.AssertTerminal(run, EAbilityPipelineState.Failed);
        Assert.Same(fixture.Context, Assert.Single(fixture.Pipeline.ReleasedContexts));
    }

    [Fact]
    public void Interruptible_composite_interrupts_only_active_children_once()
    {
        var fixture = new RunFixture();
        var race = new AbilityRacePhase<TestContext>(new AbilityPipelinePhaseId("race"));
        race.AddSubPhase(new ActivePhase("first", fixture.Journal));
        race.AddSubPhase(new ActivePhase("second", fixture.Journal));
        race.AddSubPhase(new ActivePhase("skipped", fixture.Journal, shouldExecute: false));
        fixture.Pipeline.AddPhase(race);
        var run = fixture.Start();
        run.Tick(0f);
        Assert.True(run.HasCurrentPhase);
        Assert.DoesNotContain("execute:skipped", fixture.Journal);
        fixture.Journal.Clear();

        run.Interrupt();
        run.Interrupt();
        run.Tick(1f);

        Assert.Equal(new[]
        {
            "interrupt:first", "interrupt:second", "interrupt", "trace:Interrupt", "error", "failed",
            "trace:RunEnd", "release-context", "unregister"
        }, fixture.Journal);
        Assert.True(fixture.Context.IsAborted);
        fixture.AssertTerminal(run, EAbilityPipelineState.Failed);
        Assert.Same(fixture.Context, Assert.Single(fixture.Pipeline.ReleasedContexts));
    }

    [Fact]
    public void Discarding_an_attached_run_unregisters_without_replaying_business_callbacks_or_releasing_context()
    {
        var fixture = new RunFixture();
        fixture.Pipeline.AddPhase(new ActivePhase("restored", fixture.Journal));

        var run = fixture.Pipeline.AttachRun(new TestConfig(), fixture.Context, attached =>
        {
            fixture.Journal.Add("attach");
            attached.SetExecutionPosition(0, hasCurrentPhase: true, isPaused: false, isCancelled: false);
        });
        Assert.Equal(new[] { "attach", "register" }, fixture.Journal);
        Assert.Same(run, Assert.Single(fixture.Registry.GetActiveOwners()));

        run.Release();
        run.Release();
        run.Tick(1f);

        Assert.Equal(new[] { "attach", "register", "unregister" }, fixture.Journal);
        Assert.Empty(fixture.Registry.GetActiveOwners());
        Assert.Empty(fixture.Pipeline.ReleasedContexts);
    }

    [Fact]
    public void Failed_attach_propagates_the_error_without_registering_or_releasing_context()
    {
        var fixture = new RunFixture();
        fixture.Pipeline.AddPhase(new ActivePhase("restored", fixture.Journal));
        var initializationError = new InvalidOperationException("attach failed");

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Pipeline.AttachRun(
            new TestConfig(), fixture.Context, _ =>
            {
                fixture.Journal.Add("attach");
                throw initializationError;
            }));

        Assert.Same(initializationError, error);
        Assert.Equal(new[] { "attach" }, fixture.Journal);
        Assert.Empty(fixture.Registry.GetActiveOwners());
        Assert.Empty(fixture.Pipeline.ReleasedContexts);
    }

    private sealed class RunFixture
    {
        public RunFixture()
        {
            Registry = new RecordingRegistry(Journal);
            Pipeline = new TestPipeline(Journal) { Runtime = new PipelineRuntime(Registry, new RecordingTrace(Journal)) };
            var events = Pipeline.Events;
            events.OnPipelineStart += _ => Journal.Add("start");
            events.OnPipelineComplete += _ => Journal.Add("complete");
            events.OnPipelinePause += _ => Journal.Add("pause");
            events.OnPipelineResume += _ => Journal.Add("resume");
            events.OnPipelineInterrupt += (_, _) => Journal.Add("interrupt");
            events.OnPhaseStart += (_, _) => Journal.Add("phase-start");
            events.OnPhaseComplete += (_, _) => Journal.Add("phase-complete");
            events.OnPhaseError += (_, _, error) =>
            {
                Journal.Add("phase-error");
                PhaseErrors.Add(error);
            };
            events.OnPipelineError += (_, error) =>
            {
                Journal.Add("error");
                Errors.Add(error);
            };
            events.OnPipelineFailed += (_, error) =>
            {
                Journal.Add("failed");
                Failures.Add(error);
            };
        }

        public List<string> Journal { get; } = new();
        public List<Exception> PhaseErrors { get; } = new();
        public List<Exception?> Errors { get; } = new();
        public List<Exception> Failures { get; } = new();
        public TestContext Context { get; } = new();
        public TestPipeline Pipeline { get; }
        public RecordingRegistry Registry { get; }

        public IAbilityPipelineRun<TestContext> Start() => Pipeline.Start(new TestConfig(), Context);

        public void AssertTerminal(IAbilityPipelineRun<TestContext> run, EAbilityPipelineState state)
        {
            Assert.Equal(state, run.State);
            Assert.Equal(state, Context.PipelineState);
            Assert.Empty(Registry.GetActiveOwners());
        }
    }

    private sealed class TestPipeline(List<string> journal) : AbilityPipeline<TestContext>
    {
        public List<TestContext> ReleasedContexts { get; } = new();

        protected override void ReleaseContext(TestContext context)
        {
            journal.Add("release-context");
            ReleasedContexts.Add(context);
        }
    }

    private sealed class TestContext : AAbilityPipelineContext;

    private sealed class TestConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 1;
        public string ConfigName => "lifecycle-test";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }

    private sealed class ActivePhase(string name, List<string> journal, Exception? updateError = null,
        Exception? handlerError = null, bool shouldExecute = true) : AbilityPipelinePhaseBase<TestContext>(new AbilityPipelinePhaseId(name)),
        IInterruptiblePhase<TestContext>, IAbilityPipelinePhaseInstanceFactory<TestContext>
    {
        public override bool ShouldExecute(TestContext context) => shouldExecute;

        protected override void OnExecute(TestContext context) => journal.Add($"execute:{name}");

        public override void OnUpdate(TestContext context, float deltaTime)
        {
            journal.Add($"update:{name}");
            if (updateError is not null) throw updateError;
        }

        public override void HandleError(TestContext context, Exception exception)
        {
            journal.Add($"handle-error:{name}");
            if (handlerError is not null) throw handlerError;
        }

        public void OnInterrupt(TestContext context) => journal.Add($"interrupt:{name}");

        public IAbilityPipelinePhase<TestContext> CreateRunPhase() =>
            new ActivePhase(name, journal, updateError, handlerError, shouldExecute);
    }

    private sealed class RecordingTrace(List<string> journal) : IPipelineTraceRecorder
    {
        public bool IsEnabled => true;
        public void Record(IPipelineLifeOwner owner, PipelineTraceData data) => journal.Add($"trace:{data.Type}");
        public IPipelineRunTrace? GetTrace(int ownerId) => throw new NotSupportedException();
        public IReadOnlyList<PipelineTraceEvent> GetSnapshot(int ownerId) => throw new NotSupportedException();
    }

    private sealed class RecordingRegistry(List<string> journal) : IPipelineRegistry
    {
        private readonly List<IPipelineLifeOwner> _active = new();

        public int ActiveCount => _active.Count;

        public void Register(IPipelineLifeOwner owner)
        {
            journal.Add("register");
            _active.Add(owner);
        }

        public void Unregister(IPipelineLifeOwner owner)
        {
            journal.Add("unregister");
            _active.Remove(owner);
        }

        public IReadOnlyList<IPipelineLifeOwner> GetActiveOwners() => _active;
        public void InterruptAll() => throw new NotSupportedException();
        public IReadOnlyList<IPipelineLifeOwner> GetOwnersByPhase(AbilityPipelinePhaseId phaseId) => throw new NotSupportedException();
        public int FillOwnersByPhase(AbilityPipelinePhaseId phaseId, IList<IPipelineLifeOwner> results) => throw new NotSupportedException();
        public PipelineRegistryOwnerListLease RentOwnersByPhase(AbilityPipelinePhaseId phaseId) => throw new NotSupportedException();
        public IReadOnlyList<IPipelineLifeOwner> GetOwnersByState(EAbilityPipelineState state) => throw new NotSupportedException();
        public int FillOwnersByState(EAbilityPipelineState state, IList<IPipelineLifeOwner> results) => throw new NotSupportedException();
        public PipelineRegistryOwnerListLease RentOwnersByState(EAbilityPipelineState state) => throw new NotSupportedException();
    }
}
