using AbilityKit.Benchmarking;
using AbilityKit.Pipeline;

namespace AbilityKit.Runtime.Benchmarks;

public sealed class PipelineSynchronousScenario : BenchmarkScenarioBase
{
    private readonly int _phaseCount;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private readonly BenchmarkPipelineContext _context = new();
    private BenchmarkPipeline? _pipeline;
    private PipelineRuntime? _runtime;
    private PipelineRegistry? _registry;
    private long _completedRuns;

    public PipelineSynchronousScenario(int phaseCount, int batchSize = 1)
    {
        if (phaseCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(phaseCount));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        _phaseCount = phaseCount;
        _batchSize = batchSize;
        _descriptor = new BenchmarkDescriptor(
            $"pipeline.synchronous.phases-{phaseCount}",
            "pipeline",
            "phase",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Capability,
                ["runMode"] = "synchronous-completion",
                ["phaseCount"] = phaseCount.ToString(),
                ["batchSize"] = batchSize.ToString(),
                ["trace"] = "disabled",
                ["contextReuse"] = "single-context"
            });
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void Setup()
    {
        _registry = new PipelineRegistry();
        _runtime = new PipelineRuntime(_registry, NoOpPipelineTraceRecorder.Instance);
        _runtime.Initialize();
        _pipeline = new BenchmarkPipeline { Runtime = _runtime };
        for (var i = 0; i < _phaseCount; i++)
        {
            _pipeline.AddPhase(new AbilityActionPhase<BenchmarkPipelineContext>(
                new AbilityPipelinePhaseId($"benchmark-{i}"),
                static context => context.Counter++));
        }
    }

    public override void IterationSetup()
    {
        _completedRuns = 0;
        _context.Counter = 0;
    }

    public override long Execute(int invocationCount)
    {
        var runCount = checked(invocationCount * _batchSize);
        for (var i = 0; i < runCount; i++)
        {
            _context.Reset();
            var run = _pipeline!.Start(BenchmarkPipelineConfig.Instance, _context);
            run.Tick(0f);
            if (run.State != EAbilityPipelineState.Completed)
                throw new InvalidOperationException($"Pipeline ended in state {run.State}.");
            _completedRuns++;
        }

        return checked((long)runCount * _phaseCount);
    }

    public override void Validate()
    {
        if (_completedRuns > 0 && _context.Counter != _completedRuns * _phaseCount)
            throw new InvalidOperationException("Pipeline phase execution count is inconsistent.");
        if (_registry != null && _registry.ActiveCount != 0)
            throw new InvalidOperationException("Completed pipeline runs remain registered.");
    }

    public override string GetDeterminismDigest() =>
        $"runs={_completedRuns};phases={_context.Counter}";

    public override void Cleanup()
    {
        _runtime?.Shutdown();
    }
}

public sealed class PipelineActiveRunsScenario : BenchmarkScenarioBase
{
    private readonly int _activeRunCount;
    private readonly BenchmarkDescriptor _descriptor;
    private readonly List<IAbilityPipelineRun<BenchmarkPipelineContext>> _runs;
    private PipelineRuntime? _runtime;
    private PipelineRegistry? _registry;
    private long _tickCount;

    public PipelineActiveRunsScenario(int activeRunCount)
    {
        if (activeRunCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(activeRunCount));

        _activeRunCount = activeRunCount;
        _runs = new List<IAbilityPipelineRun<BenchmarkPipelineContext>>(activeRunCount);
        _descriptor = new BenchmarkDescriptor(
            $"pipeline.active-runs.count-{activeRunCount}",
            "pipeline",
            "run-tick",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Capability,
                ["runMode"] = "cross-frame-active",
                ["activeRuns"] = activeRunCount.ToString(),
                ["phaseCount"] = "1",
                ["trace"] = "disabled"
            });
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void Setup()
    {
        _registry = new PipelineRegistry();
        _runtime = new PipelineRuntime(_registry, NoOpPipelineTraceRecorder.Instance);
        _runtime.Initialize();
        var pipeline = new BenchmarkPipeline { Runtime = _runtime };
        pipeline.AddPhase(new BenchmarkTickingPhase());

        for (var i = 0; i < _activeRunCount; i++)
        {
            var run = pipeline.Start(BenchmarkPipelineConfig.Instance, new BenchmarkPipelineContext());
            run.Tick(0f);
            _runs.Add(run);
        }
    }

    public override void IterationSetup()
    {
        _tickCount = 0;
    }

    public override long Execute(int invocationCount)
    {
        for (var invocation = 0; invocation < invocationCount; invocation++)
        {
            for (var i = 0; i < _runs.Count; i++)
                _runs[i].Tick(1f / 30f);
            _tickCount += _runs.Count;
        }

        return checked((long)invocationCount * _runs.Count);
    }

    public override void Validate()
    {
        if (_registry != null && _registry.ActiveCount != _activeRunCount)
            throw new InvalidOperationException(
                $"Expected {_activeRunCount} active runs but found {_registry.ActiveCount}.");
        if (_runs.Any(run => run.State != EAbilityPipelineState.Executing))
            throw new InvalidOperationException("An active pipeline run reached a terminal state.");
    }

    public override string GetDeterminismDigest() => $"active={_activeRunCount};ticks={_tickCount}";

    public override void Cleanup()
    {
        foreach (var run in _runs)
            run.Interrupt();
        _runs.Clear();
        _runtime?.Shutdown();
    }
}

public sealed class BenchmarkPipelineContext : AAbilityPipelineContext
{
    public long Counter { get; set; }
}

internal sealed class BenchmarkPipeline : AbilityPipeline<BenchmarkPipelineContext>
{
    protected override void ReleaseContext(BenchmarkPipelineContext context)
    {
    }
}

internal sealed class BenchmarkTickingPhase : AbilityPipelinePhaseBase<BenchmarkPipelineContext>,
    IAbilityPipelinePhaseInstanceFactory<BenchmarkPipelineContext>
{
    public BenchmarkTickingPhase()
        : base(new AbilityPipelinePhaseId("benchmark-active"))
    {
    }

    protected override void OnExecute(BenchmarkPipelineContext context)
    {
    }

    public override void OnUpdate(BenchmarkPipelineContext context, float deltaTime)
    {
        context.Counter++;
    }

    public IAbilityPipelinePhase<BenchmarkPipelineContext> CreateRunPhase() =>
        new BenchmarkTickingPhase();
}

internal sealed class BenchmarkPipelineConfig : IAbilityPipelineConfig
{
    public static readonly BenchmarkPipelineConfig Instance = new();

    public int ConfigId => 1;

    public string ConfigName => "benchmark";

    public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs { get; } = Array.Empty<IAbilityPhaseConfig>();

    public bool AllowInterrupt => true;

    public bool AllowPause => true;
}
