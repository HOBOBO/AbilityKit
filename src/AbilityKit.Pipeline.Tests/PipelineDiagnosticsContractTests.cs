using Xunit;
using AbilityKit.Pipeline.Editor;

namespace AbilityKit.Pipeline.Tests;

public sealed class PipelineDiagnosticsContractTests
{
    [Fact]
    public void DiagnosticObserversReceiveLifecycleAndCannotBreakExecution()
    {
        var starts = new List<PipelineRunStartedData>();
        var ends = new List<PipelineRunEndedData>();
        var traces = new List<PipelineTraceData>();
        Action<PipelineRunStartedData> throwingStart = _ => throw new InvalidOperationException("observer failed");
        Action<PipelineRunStartedData> captureStart = starts.Add;
        Action<PipelineRunEndedData> captureEnd = ends.Add;
        Action<IPipelineLifeOwner, PipelineTraceData> captureTrace = (_, data) => traces.Add(data);

        PipelineDebugHooks.OnRunStartedDetailed += throwingStart;
        PipelineDebugHooks.OnRunStartedDetailed += captureStart;
        PipelineDebugHooks.OnRunEnded += captureEnd;
        PipelineDebugHooks.OnTrace += captureTrace;

        try
        {
            var pipeline = CreatePipeline();
            pipeline.AddPhase(PipelineGraph.Action<TestContext>(_ => { }));

            var run = pipeline.Start(new TestConfig(), new TestContext());
            run.Tick(0f);

            Assert.Equal(EAbilityPipelineState.Completed, run.State);
            Assert.Single(starts);
            Assert.Single(ends);
            Assert.Same(run, starts[0].Run);
            Assert.Equal(starts[0].Owner.OwnerId, ends[0].Owner.OwnerId);
            Assert.Contains(traces, trace => trace.Type == EPipelineTraceEventType.RunStart);
            Assert.Contains(traces, trace => trace.Type == EPipelineTraceEventType.PhaseStart);
            Assert.Contains(traces, trace => trace.Type == EPipelineTraceEventType.RunEnd);
        }
        finally
        {
            PipelineDebugHooks.OnRunStartedDetailed -= throwingStart;
            PipelineDebugHooks.OnRunStartedDetailed -= captureStart;
            PipelineDebugHooks.OnRunEnded -= captureEnd;
            PipelineDebugHooks.OnTrace -= captureTrace;
        }
    }

    [Fact]
    public void RunsExposeNonGenericControlAndProcessWideUniqueIds()
    {
        var firstPipeline = CreatePipeline();
        firstPipeline.AddPhase(PipelineGraph.Delay<TestContext>(10f));
        var secondPipeline = CreateOtherPipeline();
        secondPipeline.AddPhase(PipelineGraph.Delay<OtherTestContext>(10f));

        var first = firstPipeline.Start(new TestConfig(), new TestContext());
        var second = secondPipeline.Start(new TestConfig(), new OtherTestContext());

        var firstOwner = Assert.IsAssignableFrom<IPipelineLifeOwner>(first);
        var secondOwner = Assert.IsAssignableFrom<IPipelineLifeOwner>(second);
        Assert.NotEqual(firstOwner.OwnerId, secondOwner.OwnerId);

        var control = Assert.IsAssignableFrom<IPipelineRunControl>(first);
        control.Pause();
        Assert.True(control.IsPaused);
        control.Resume();
        Assert.False(control.IsPaused);

        first.Interrupt();
        second.Interrupt();
    }

    [Fact]
    public void PipelineExposesImmutablePhaseDefinitionTree()
    {
        var pipeline = CreatePipeline();
        pipeline.AddPhase(PipelineGraph.Sequence<TestContext>(
            PipelineGraph.Action<TestContext>(_ => { }),
            PipelineGraph.Parallel<TestContext>(
                PipelineGraph.Delay<TestContext>(1f),
                PipelineGraph.WaitUntil<TestContext>(_ => true))));

        var structure = pipeline.CaptureDebugStructure();

        var sequence = Assert.Single(structure);
        Assert.Equal("Sequence", sequence.PhaseId.Value);
        Assert.Equal(2, sequence.Children.Count);
        Assert.Equal("Action", sequence.Children[0].PhaseId.Value);
        Assert.Equal("Parallel", sequence.Children[1].PhaseId.Value);
        Assert.Equal(2, sequence.Children[1].Children.Count);
    }

    [Fact]
    public void DiagnosticGraphUsesStablePathsAndCompositeSemantics()
    {
        var pipeline = CreatePipeline();
        pipeline.AddPhase(PipelineGraph.Sequence<TestContext>(
            PipelineGraph.Action<TestContext>(_ => { }),
            PipelineGraph.Action<TestContext>(_ => { })));
        pipeline.AddPhase(PipelineGraph.Parallel<TestContext>(
            PipelineGraph.Delay<TestContext>(1f),
            PipelineGraph.Delay<TestContext>(1f)));

        var graph = pipeline.CaptureDebugGraph();

        Assert.Equal(2, graph.Roots.Count);
        Assert.Equal(EPipelineDebugNodeKind.Sequence, graph.Roots[0].Kind);
        Assert.Equal(EPipelineDebugNodeKind.Parallel, graph.Roots[1].Kind);
        Assert.Equal("0/0", graph.Roots[0].Children[0].NodeKey);
        Assert.Equal("0/1", graph.Roots[0].Children[1].NodeKey);
        Assert.NotEqual(graph.Roots[0].Children[0].NodeKey, graph.Roots[0].Children[1].NodeKey);
        Assert.Contains(graph.Edges, edge => edge.Kind == EPipelineDebugEdgeKind.Flow && edge.SourceNodeKey == "0" && edge.TargetNodeKey == "1");
        Assert.Contains(graph.Edges, edge => edge.Kind == EPipelineDebugEdgeKind.Sequence && edge.Label == "1");
        Assert.False(string.IsNullOrWhiteSpace(graph.StructureId));
    }

    [Fact]
    public void ConditionalGraphExposesBranchesAndCapturedDecisionState()
    {
        var pipeline = CreatePipeline();
        pipeline.AddPhase(PipelineGraph.Conditional<TestContext>(
            new TestCondition(false),
            PipelineGraph.Action<TestContext>(context => context.SetData("branch", "true")),
            PipelineGraph.Action<TestContext>(context => context.SetData("branch", "false"))));

        var graph = pipeline.CaptureDebugGraph();
        var conditional = Assert.Single(graph.Roots);
        Assert.Equal(EPipelineDebugNodeKind.Conditional, conditional.Kind);
        Assert.Equal(2, conditional.Children.Count);
        var conditionEdges = graph.Edges.Where(edge => edge.Kind == EPipelineDebugEdgeKind.Condition).ToArray();
        Assert.Equal(2, conditionEdges.Length);
        Assert.Equal("TestCondition · OnEnter", conditionEdges[0].Label);
        Assert.Equal("Else", conditionEdges[1].Label);

        var run = pipeline.Start(new TestConfig(), new TestContext());
        run.Tick(0f);

        var stateProvider = Assert.IsAssignableFrom<IPipelineDebugStateProvider>(run);
        var activeState = stateProvider.CaptureDebugState();
        var activeRoot = Assert.Single(activeState.Nodes, node => node.NodeKey == "0");
        Assert.Equal(EPipelineDebugExecutionState.Active, activeRoot.State);
        Assert.Equal(1, activeRoot.SelectedChildIndex);

        run.Tick(0f);
        var state = stateProvider.CaptureDebugState();
        var rootState = Assert.Single(state.Nodes, node => node.NodeKey == "0");
        Assert.Equal(EPipelineDebugExecutionState.Completed, rootState.State);
        Assert.Equal(1, rootState.SelectedChildIndex);
        Assert.Equal(EPipelineDebugConditionResult.Rejected, rootState.ChildConditions[0]);
        Assert.Equal(EPipelineDebugConditionResult.Matched, rootState.ChildConditions[1]);
        Assert.Contains(state.Nodes, node => node.NodeKey == "0/0" && node.State == EPipelineDebugExecutionState.Skipped);
        Assert.Contains(state.Nodes, node => node.NodeKey == "0/1" && node.State == EPipelineDebugExecutionState.Completed);
    }

    [Fact]
    public void EditorStoreObservesRunWithoutReplacingRuntimeRegistry()
    {
        var store = EditorPipelineRegistry.Instance;
        store.Shutdown();
        store.Initialize();
        PipelineDebugHooks.OnRunStartedDetailed += store.CaptureRunStarted;
        PipelineDebugHooks.OnTrace += store.CaptureTrace;
        PipelineDebugHooks.OnRunEnded += store.CaptureRunEnded;

        try
        {
            var pipeline = CreatePipeline();
            var runtimeRegistry = pipeline.Runtime.Registry;
            pipeline.AddPhase(PipelineGraph.Action<TestContext>(context => context.SetData("result", 42)));

            var run = pipeline.Start(new TestConfig(), new TestContext());
            var owner = Assert.IsAssignableFrom<IPipelineLifeOwner>(run);
            run.Tick(0f);

            Assert.Same(runtimeRegistry, pipeline.Runtime.Registry);
            Assert.True(store.TryGetEntry(owner.OwnerId, out var entry));
            Assert.NotNull(entry);
            Assert.False(entry.IsActive);
            Assert.Equal(EAbilityPipelineState.Completed, entry.LastState);
            Assert.DoesNotContain(entry.InitialContextValues, value => value.Name == "SharedData.result");
            Assert.Contains(entry.ContextValues, value => value.Name == "SharedData.result" && value.Value == "42");
            Assert.Contains(entry.PhaseStates, node => node.NodeKey == "0" && node.State == EPipelineDebugExecutionState.Completed);
            Assert.Contains(store.GetTraceSnapshot(owner.OwnerId), trace => trace.Type == EPipelineTraceEventType.RunEnd);
        }
        finally
        {
            PipelineDebugHooks.OnRunStartedDetailed -= store.CaptureRunStarted;
            PipelineDebugHooks.OnTrace -= store.CaptureTrace;
            PipelineDebugHooks.OnRunEnded -= store.CaptureRunEnded;
            store.Shutdown();
        }
    }

    [Fact]
    public void EditorStoreAppliesConfiguredTraceAndHistoryCapacities()
    {
        var store = EditorPipelineRegistry.Instance;
        store.Shutdown();
        store.ConfigureStorage(1, 16);
        store.Initialize();
        PipelineDebugHooks.OnRunStartedDetailed += store.CaptureRunStarted;
        PipelineDebugHooks.OnTrace += store.CaptureTrace;
        PipelineDebugHooks.OnRunEnded += store.CaptureRunEnded;

        try
        {
            var firstPipeline = CreatePipeline();
            for (int i = 0; i < 10; i++)
            {
                firstPipeline.AddPhase(PipelineGraph.Action<TestContext>(_ => { }));
            }

            var first = firstPipeline.Start(new TestConfig(), new TestContext());
            var firstOwner = Assert.IsAssignableFrom<IPipelineLifeOwner>(first);
            first.Tick(0f);

            Assert.True(store.TryGetEntry(firstOwner.OwnerId, out var firstEntry));
            Assert.NotNull(firstEntry);
            Assert.Equal(16, firstEntry.Trace.Capacity);
            Assert.Equal(16, store.GetTraceSnapshot(firstOwner.OwnerId).Count);

            var secondPipeline = CreatePipeline();
            secondPipeline.AddPhase(PipelineGraph.Action<TestContext>(_ => { }));
            var second = secondPipeline.Start(new TestConfig(), new TestContext());
            var secondOwner = Assert.IsAssignableFrom<IPipelineLifeOwner>(second);
            second.Tick(0f);

            Assert.False(store.TryGetEntry(firstOwner.OwnerId, out _));
            Assert.True(store.TryGetEntry(secondOwner.OwnerId, out _));
            Assert.Single(store.GetEntries());
        }
        finally
        {
            PipelineDebugHooks.OnRunStartedDetailed -= store.CaptureRunStarted;
            PipelineDebugHooks.OnTrace -= store.CaptureTrace;
            PipelineDebugHooks.OnRunEnded -= store.CaptureRunEnded;
            store.ConfigureStorage(128, 2048);
            store.Shutdown();
        }
    }

    [Fact]
    public void EditorStoreUsesMatchingProvidedGraphLayoutAndRejectsStaleLayout()
    {
        var store = EditorPipelineRegistry.Instance;
        store.Shutdown();
        store.Initialize();
        PipelineDebugHooks.OnRunStartedDetailed += store.CaptureRunStarted;

        try
        {
            var pipeline = CreatePipeline();
            pipeline.AddPhase(PipelineGraph.Delay<TestContext>(10f));
            string structureId = pipeline.CaptureDebugGraph().StructureId;
            var matching = new LayoutConfig(structureId);
            var run = pipeline.Start(matching, new TestContext());
            var owner = Assert.IsAssignableFrom<IPipelineLifeOwner>(run);

            Assert.True(store.TryGetEntry(owner.OwnerId, out var entry));
            Assert.NotNull(entry!.GraphLayout);
            Assert.False(entry.HasGraphLayoutMismatch);
            Assert.Equal("authored-layout", entry.GraphLayout!.SourceName);
            run.Interrupt();

            var stalePipeline = CreatePipeline();
            stalePipeline.AddPhase(PipelineGraph.Delay<TestContext>(10f));
            var staleRun = stalePipeline.Start(new LayoutConfig("STALE"), new TestContext());
            var staleOwner = Assert.IsAssignableFrom<IPipelineLifeOwner>(staleRun);
            Assert.True(store.TryGetEntry(staleOwner.OwnerId, out var staleEntry));
            Assert.Null(staleEntry!.GraphLayout);
            Assert.True(staleEntry.HasGraphLayoutMismatch);
            staleRun.Interrupt();
        }
        finally
        {
            PipelineDebugHooks.OnRunStartedDetailed -= store.CaptureRunStarted;
            store.Shutdown();
        }
    }

    [Fact]
    public void PinnedHistorySurvivesPruningAndClearHistory()
    {
        var store = EditorPipelineRegistry.Instance;
        store.Shutdown();
        store.ConfigureStorage(0, 64);
        store.Initialize();
        PipelineDebugHooks.OnRunStartedDetailed += store.CaptureRunStarted;
        PipelineDebugHooks.OnTrace += store.CaptureTrace;
        PipelineDebugHooks.OnRunEnded += store.CaptureRunEnded;

        try
        {
            var firstPipeline = CreatePipeline();
            firstPipeline.AddPhase(PipelineGraph.Delay<TestContext>(10f));
            var first = firstPipeline.Start(new TestConfig(), new TestContext());
            var firstOwner = Assert.IsAssignableFrom<IPipelineLifeOwner>(first);
            store.SetPinned(firstOwner.OwnerId, true);
            first.Interrupt();

            Assert.True(store.TryGetEntry(firstOwner.OwnerId, out var pinned));
            Assert.True(pinned!.IsPinned);

            var secondPipeline = CreatePipeline();
            secondPipeline.AddPhase(PipelineGraph.Action<TestContext>(_ => { }));
            var second = secondPipeline.Start(new TestConfig(), new TestContext());
            var secondOwner = Assert.IsAssignableFrom<IPipelineLifeOwner>(second);
            second.Tick(0f);

            Assert.False(store.TryGetEntry(secondOwner.OwnerId, out _));
            store.ClearHistory();
            Assert.True(store.TryGetEntry(firstOwner.OwnerId, out _));
            Assert.Equal(1, store.GetStats().Pinned);
        }
        finally
        {
            PipelineDebugHooks.OnRunStartedDetailed -= store.CaptureRunStarted;
            PipelineDebugHooks.OnTrace -= store.CaptureTrace;
            PipelineDebugHooks.OnRunEnded -= store.CaptureRunEnded;
            store.ConfigureStorage(128, 2048);
            store.Shutdown();
        }
    }

    [Fact]
    public void PhaseErrorTraceContainsTheActualFailureMessage()
    {
        var traces = new List<PipelineTraceData>();
        Action<IPipelineLifeOwner, PipelineTraceData> capture = (_, data) => traces.Add(data);
        PipelineDebugHooks.OnTrace += capture;

        try
        {
            var pipeline = CreatePipeline();
            pipeline.AddPhase(PipelineGraph.Action<TestContext>(_ => throw new InvalidOperationException("missing target")));

            var run = pipeline.Start(new TestConfig(), new TestContext());
            run.Tick(0f);

            Assert.Equal(EAbilityPipelineState.Failed, run.State);
            var error = Assert.Single(traces, item => item.Type == EPipelineTraceEventType.PhaseError);
            Assert.Equal("missing target", error.Message);
        }
        finally
        {
            PipelineDebugHooks.OnTrace -= capture;
        }
    }

    private static TestPipeline CreatePipeline()
    {
        var runtime = new PipelineRuntime(new PipelineRegistry(), NoOpPipelineTraceRecorder.Instance);
        runtime.Initialize();
        return new TestPipeline { Runtime = runtime };
    }

    private static OtherTestPipeline CreateOtherPipeline()
    {
        var runtime = new PipelineRuntime(new PipelineRegistry(), NoOpPipelineTraceRecorder.Instance);
        runtime.Initialize();
        return new OtherTestPipeline { Runtime = runtime };
    }

    private sealed class TestPipeline : AbilityPipeline<TestContext>
    {
        protected override void ReleaseContext(TestContext context)
        {
        }
    }

    private sealed class OtherTestPipeline : AbilityPipeline<OtherTestContext>
    {
        protected override void ReleaseContext(OtherTestContext context)
        {
        }
    }

    private sealed class TestContext : AAbilityPipelineContext;
    private sealed class OtherTestContext : AAbilityPipelineContext;

    private sealed class TestConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 1;
        public string ConfigName => "diagnostics-test";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }

    private sealed class TestCondition(bool result) : IAbilityConditionNode
    {
        public EConditionCheckStrategy CheckStrategy => EConditionCheckStrategy.OnEnter;
        public bool Evaluate(IAbilityPipelineContext context) => result;
    }

    private sealed class LayoutConfig(string structureId) : IAbilityPipelineConfig, IPipelineDebugGraphLayoutProvider
    {
        public int ConfigId => 2;
        public string ConfigName => "layout-test";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;

        public PipelineDebugGraphLayout CaptureDebugGraphLayout()
        {
            return new PipelineDebugGraphLayout(
                structureId,
                new[] { new PipelineDebugNodeLayout("0", 120f, 80f) },
                "authored-layout");
        }
    }
}
