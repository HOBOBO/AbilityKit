#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerLifecycleSmokeTests
    {
        private EditorPipelineRegistry _registry = null!;
        private bool _captureEnabled;

        [SetUp]
        public void SetUp()
        {
            _registry = EditorPipelineRegistry.Instance;
            _captureEnabled = PipelineDebuggerUserState.instance.CaptureEnabled;
            PipelineDebuggerUserState.instance.CaptureEnabled = true;
            PipelineEditorInitializer.Uninstall();
            _registry.Shutdown();
            _registry.IsCaptureEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            PipelineEditorInitializer.Uninstall();
            _registry.Shutdown();
            PipelineDebuggerUserState.instance.CaptureEnabled = _captureEnabled;
            PipelineEditorInitializer.Install();
        }

        [Test]
        public void Registry_RequiresInitialization_AndShutdownClearsState()
        {
            TestRun run = CreateRun(101);

            _registry.CaptureRunStarted(run.StartedData);
            Assert.That(_registry.GetEntries(), Is.Empty);

            _registry.Initialize();
            _registry.CaptureRunStarted(run.StartedData);
            Assert.That(_registry.GetEntries(), Has.Count.EqualTo(1));
            Assert.That(_registry.SelectedRunId, Is.EqualTo(101));

            _registry.Shutdown();
            Assert.That(_registry.GetEntries(), Is.Empty);
            Assert.That(_registry.SelectedRunId, Is.Null);

            _registry.CaptureRunStarted(run.StartedData);
            Assert.That(_registry.GetEntries(), Is.Empty);
        }

        [Test]
        public void Initializer_InstallIsIdempotent_AndUninstallStopsHookCapture()
        {
            TestRun first = CreateRun(201);
            PipelineEditorInitializer.Install();
            PipelineEditorInitializer.Install();

            PipelineDebugHooks.NotifyRunStarted(
                first.Owner,
                first.Pipeline,
                first.Config,
                first.Run);

            Assert.That(PipelineEditorInitializer.IsInstalled, Is.True);
            Assert.That(_registry.GetEntries(), Has.Count.EqualTo(1));

            PipelineEditorInitializer.Uninstall();
            Assert.That(PipelineEditorInitializer.IsInstalled, Is.False);
            Assert.That(_registry.GetEntries(), Is.Empty);

            TestRun second = CreateRun(202);
            PipelineDebugHooks.NotifyRunStarted(
                second.Owner,
                second.Pipeline,
                second.Config,
                second.Run);

            Assert.That(_registry.GetEntries(), Is.Empty);
        }

        [Test]
        public void SessionCapture_CopiesGraphLayoutStateTraceAndContextValues()
        {
            TestRun test = CreateRun(301);
            PipelineEditorInitializer.Install();
            PipelineDebugHooks.NotifyRunStarted(
                test.Owner,
                test.Pipeline,
                test.Config,
                test.Run);

            test.Context.SharedData["Counter"] = 9;
            test.Context.Elapsed = 2.5f;
            test.Owner.CurrentPhaseId = new AbilityPipelinePhaseId("child");
            test.Run.PhaseStates = new[]
            {
                new PipelinePhaseDebugState(
                    "root",
                    EPipelineDebugExecutionState.Completed,
                    0,
                    new[] { EPipelineDebugConditionResult.Matched }),
                new PipelinePhaseDebugState("child", EPipelineDebugExecutionState.Active)
            };
            _registry.Refresh();
            PipelineDebugHooks.NotifyTrace(
                test.Owner,
                new PipelineTraceData(
                    7,
                    EPipelineTraceEventType.PhaseStart,
                    new AbilityPipelinePhaseId("child"),
                    EAbilityPipelineState.Executing,
                    "entered child"));

            Assert.That(_registry.TryGetEntry(301, out EditorPipelineRegistry.DebugEntry? entry), Is.True);
            var session = ScriptableObject.CreateInstance<PipelineDebugSessionAsset>();
            try
            {
                session.Capture(entry!, _registry.GetTraceSnapshot(301));

                Assert.That(session.FormatVersion, Is.EqualTo(3));
                Assert.That(session.RunId, Is.EqualTo(301));
                Assert.That(session.OwnerName, Is.EqualTo("Run 301"));
                Assert.That(session.CurrentPhase, Is.EqualTo("child"));
                Assert.That(session.StructureId, Is.EqualTo("test-graph"));
                Assert.That(session.LayoutSource, Is.EqualTo("Test Layout"));
                Assert.That(session.Phases, Has.Count.EqualTo(2));
                Assert.That(session.Phases[0].NodeKey, Is.EqualTo("root"));
                Assert.That(session.Phases[0].ExecutionState, Is.EqualTo("Completed"));
                Assert.That(session.Phases[0].ConditionResults, Is.EqualTo(new[] { "Matched" }));
                Assert.That(session.Phases[1].Path, Is.EqualTo("0/0"));
                Assert.That(session.Phases[1].Depth, Is.EqualTo(1));
                Assert.That(session.Phases[1].HasPosition, Is.True);
                Assert.That(session.Phases[1].Position, Is.EqualTo(new Vector2(120f, 80f)));
                Assert.That(session.Edges, Has.Count.EqualTo(1));
                Assert.That(session.Edges[0].Kind, Is.EqualTo("Child"));
                Assert.That(session.Trace, Has.Count.EqualTo(1));
                Assert.That(session.Trace[0].Sequence, Is.EqualTo(7));
                Assert.That(session.Trace[0].Message, Is.EqualTo("entered child"));
                Assert.That(FindValue(session.InitialContext, "SharedData.Counter"), Is.EqualTo("1"));
                Assert.That(FindValue(session.Context, "SharedData.Counter"), Is.EqualTo("9"));
                Assert.That(session.ElapsedSeconds, Is.EqualTo(2.5d).Within(0.001d));

                test.Context.SharedData["Counter"] = 99;
                test.Run.PhaseStates = Array.Empty<PipelinePhaseDebugState>();
                _registry.Refresh();
                _registry.ClearTrace(301);

                Assert.That(FindValue(session.Context, "SharedData.Counter"), Is.EqualTo("9"));
                Assert.That(session.Phases[0].ExecutionState, Is.EqualTo("Completed"));
                Assert.That(session.Trace, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(session);
            }
        }

        private static string FindValue(
            IReadOnlyList<PipelineDebugValueSnapshot> values,
            string name)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Name == name) return values[i].Value;
            }
            return string.Empty;
        }

        private static TestRun CreateRun(int ownerId)
        {
            var context = new FakeContext
            {
                PipelineState = EAbilityPipelineState.Executing,
                CurrentPhaseId = new AbilityPipelinePhaseId("root"),
                Elapsed = 0.25f
            };
            context.SharedData["Counter"] = 1;
            var owner = new FakeOwner(ownerId)
            {
                CurrentPhaseId = new AbilityPipelinePhaseId("root")
            };
            var pipeline = new FakePipeline();
            var config = new FakeConfig();
            var run = new FakeRun(context);
            return new TestRun(owner, pipeline, config, run, context);
        }

        private sealed class TestRun
        {
            public TestRun(
                FakeOwner owner,
                FakePipeline pipeline,
                FakeConfig config,
                FakeRun run,
                FakeContext context)
            {
                Owner = owner;
                Pipeline = pipeline;
                Config = config;
                Run = run;
                Context = context;
            }

            public FakeOwner Owner { get; }
            public FakePipeline Pipeline { get; }
            public FakeConfig Config { get; }
            public FakeRun Run { get; }
            public FakeContext Context { get; }
            public PipelineRunStartedData StartedData =>
                new PipelineRunStartedData(Owner, Pipeline, Config, Run, Context);
        }

        private sealed class FakeOwner : IPipelineLifeOwner
        {
            public FakeOwner(int ownerId)
            {
                OwnerId = ownerId;
                OwnerName = "Run " + ownerId;
            }

            public int OwnerId { get; }
            public string OwnerName { get; }
            public EAbilityPipelineState State { get; set; } = EAbilityPipelineState.Executing;
            public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
            public bool IsPaused { get; set; }
            public IReadOnlyList<AbilityPipelinePhaseId> ActivePhases =>
                new[] { CurrentPhaseId };
        }

        private sealed class FakePipeline : IPipelineDebugGraphProvider
        {
            public PipelineDebugGraphSnapshot CaptureDebugGraph()
            {
                var child = new PipelinePhaseDebugNode(
                    "child",
                    new AbilityPipelinePhaseId("child"),
                    "ChildPhase",
                    EPipelineDebugNodeKind.Phase,
                    "child summary");
                var root = new PipelinePhaseDebugNode(
                    "root",
                    new AbilityPipelinePhaseId("root"),
                    "RootPhase",
                    EPipelineDebugNodeKind.Composite,
                    "root summary",
                    new[] { child });
                return new PipelineDebugGraphSnapshot(
                    new[] { root },
                    new[]
                    {
                        new PipelinePhaseDebugEdge(
                            "root",
                            "child",
                            EPipelineDebugEdgeKind.Child,
                            "first",
                            0)
                    },
                    "test-graph");
            }
        }

        private sealed class FakeConfig : IAbilityPipelineConfig, IPipelineDebugGraphLayoutProvider
        {
            public int ConfigId => 1;
            public string ConfigName => "Test";
            public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
            public bool AllowInterrupt => true;
            public bool AllowPause => true;

            public PipelineDebugGraphLayout CaptureDebugGraphLayout()
            {
                return new PipelineDebugGraphLayout(
                    "test-graph",
                    new[]
                    {
                        new PipelineDebugNodeLayout("root", 10f, 20f),
                        new PipelineDebugNodeLayout("child", 120f, 80f)
                    },
                    "Test Layout");
            }
        }

        private sealed class FakeRun : IAbilityPipelineRun<FakeContext>, IPipelineDebugStateProvider
        {
            public FakeRun(FakeContext context)
            {
                Context = context;
            }

            public FakeContext Context { get; }
            public EAbilityPipelineState State => Context.PipelineState;
            public bool IsPaused => Context.IsPaused;
            public AbilityPipelinePhaseId CurrentPhaseId => Context.CurrentPhaseId;
            public IReadOnlyList<PipelinePhaseDebugState> PhaseStates { get; set; } =
                Array.Empty<PipelinePhaseDebugState>();

            public PipelineDebugRunState CaptureDebugState() => new PipelineDebugRunState(PhaseStates);
            public void Tick(float deltaTime) => Context.Elapsed += deltaTime;
            public void Pause() => Context.IsPaused = true;
            public void Resume() => Context.IsPaused = false;
            public void Cancel() => Context.IsAborted = true;
            public void Interrupt() => Context.IsAborted = true;
        }

        private sealed class FakeContext : IAbilityPipelineContext
        {
            public object? AbilityInstance => "TestAbility";
            public Dictionary<string, object?> SharedData { get; } = new Dictionary<string, object?>();
            public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
            public EAbilityPipelineState PipelineState { get; set; }
            public bool IsAborted { get; set; }
            public bool IsPaused { get; set; }
            public float StartTime { get; set; }
            public float Elapsed { get; set; }
            public float ElapsedTime => Elapsed;

            public T GetData<T>(string key, T defaultValue = default!) =>
                TryGetData(key, out T value) ? value : defaultValue;

            public void SetData<T>(string key, T value) => SharedData[key] = value;

            public bool TryGetData<T>(string key, out T value)
            {
                if (SharedData.TryGetValue(key, out object? item) && item is T typed)
                {
                    value = typed;
                    return true;
                }
                value = default!;
                return false;
            }

            public bool RemoveData(string key) => SharedData.Remove(key);
            public void ClearData() => SharedData.Clear();

            public void Reset()
            {
                SharedData.Clear();
                CurrentPhaseId = default;
                PipelineState = EAbilityPipelineState.Ready;
                IsAborted = false;
                IsPaused = false;
                StartTime = 0f;
                Elapsed = 0f;
            }
        }
    }
}

#endif
