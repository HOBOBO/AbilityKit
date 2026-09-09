using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class AdvancedRuntimeExecutionTests
{
    [Fact]
    public void NestedExitTransitionApprovesPendingParentTransitionOnly()
    {
        var owner = new TestOwner();
        var definition = CreateExitHierarchy();
        var runtime = new StateMachineRuntime<TestOwner>(
            owner,
            definition,
            new RuntimeBindings<TestOwner>()
                .RegisterCondition("finish", () => new OwnerFlagCondition(item => item.Finish))
                .RegisterAction("child-exit", () => new TraceTransitionAction()));
        runtime.Initialize(0, Fixed64.Zero);

        runtime.Tick(1, Fixed64.One);
        Assert.Equal(new[] { "root/active", "child/inside" }, runtime.GetActivePath());

        owner.Finish = true;
        owner.Trace.Clear();
        runtime.Tick(2, Fixtures.Time(2));

        Assert.Equal(new[] { "root/done" }, runtime.GetActivePath());
        Assert.Equal(new[] { "before:", "after:" }, owner.Trace);
    }

    [Fact]
    public void GhostStatesResolveConsecutiveLocalTransitionsAndTickFinalState()
    {
        var owner = new TestOwner();
        var definition = Fixtures.Flat(
            Fixtures.State("a", "a"),
            Fixtures.State("b", "b", ghost: true),
            Fixtures.State("c", "c", ghost: true),
            Fixtures.State("d", "d"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("a-b", "a", "b"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("b-c", "b", "c"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("c-d", "c", "d"));
        var bindings = new RuntimeBindings<TestOwner>()
            .RegisterState("a", () => new TraceState("a"))
            .RegisterState("b", () => new TraceState("b"))
            .RegisterState("c", () => new TraceState("c"))
            .RegisterState("d", () => new TraceState("d"));
        var runtime = new StateMachineRuntime<TestOwner>(owner, definition, bindings);
        runtime.Initialize(0, Fixed64.Zero);
        owner.Trace.Clear();

        runtime.Tick(1, Fixed64.One);

        Assert.Equal(new[]
        {
            "exit:a", "enter:b", "exit:b", "enter:c", "exit:c", "enter:d", "tick:d",
        }, owner.Trace);
        Assert.Equal(new[] { "root/d" }, runtime.GetActivePath());
    }

    [Fact]
    public void InitialGhostStateResolvesAndCyclesAreBounded()
    {
        var definition = Fixtures.Flat(
            Fixtures.State("ghost", ghost: true),
            Fixtures.State("ready"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("ready", "ghost", "ready"));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());

        runtime.Initialize(0, Fixed64.Zero);
        Assert.Equal(new[] { "root/ready" }, runtime.GetActivePath());

        var cycle = Fixtures.Flat(Fixtures.State("loop", ghost: true));
        cycle.Machines[0].Transitions.Add(Fixtures.Transition("loop", "loop", "loop"));
        var cyclicRuntime = new StateMachineRuntime<TestOwner>(
            new TestOwner(), cycle, new RuntimeBindings<TestOwner>());
        Assert.Throws<InvalidOperationException>(() => cyclicRuntime.Initialize(0, Fixed64.Zero));
        Assert.True(cyclicRuntime.IsFaulted);
    }

    [Fact]
    public void ParallelStateBroadcastsLifecycleAndUsesConfiguredExitPolicy()
    {
        var owner = new TestOwner();
        var firstReady = true;
        var secondReady = false;
        var parallel = Fixtures.State("parallel", requiresExitApproval: true);
        parallel.ParallelBehaviorKeys.AddRange(new[] { "first", "second" });
        parallel.ParallelExitPolicy = ParallelExitPolicy.All;
        var definition = Fixtures.Flat(parallel, Fixtures.State("done"));
        definition.Machines[0].Transitions.Add(
            Fixtures.Transition("done", "parallel", "done", trigger: "done"));
        var runtime = new StateMachineRuntime<TestOwner>(
            owner,
            definition,
            new RuntimeBindings<TestOwner>()
                .RegisterState("first", () => new GateState("first", () => firstReady))
                .RegisterState("second", () => new GateState("second", () => secondReady)));
        runtime.Initialize(0, Fixed64.Zero);
        Assert.True(runtime.Trigger("done"));
        Assert.Equal(new[] { "root/parallel" }, runtime.GetActivePath());

        secondReady = true;
        runtime.Tick(1, Fixed64.One);

        Assert.Equal(new[] { "root/done" }, runtime.GetActivePath());
        Assert.Equal(new[]
        {
            "enter:first", "enter:second",
            "exit-requested:first", "exit-requested:second",
            "tick:first", "tick:second", "exit:first", "exit:second",
        }, owner.Trace);
    }

    [Fact]
    public void ParallelStateSnapshotsAllStatefulChildren()
    {
        var first = new CounterState();
        var second = new CounterState();
        var parallel = Fixtures.State("parallel");
        parallel.ParallelBehaviorKeys.AddRange(new[] { "first", "second" });
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(),
            Fixtures.Flat(parallel),
            new RuntimeBindings<TestOwner>()
                .RegisterState("first", () => first)
                .RegisterState("second", () => second));
        runtime.Initialize(0, Fixed64.Zero);
        runtime.Tick(1, Fixed64.One);
        var snapshot = runtime.CaptureSnapshot();
        runtime.Tick(2, Fixtures.Time(2));

        runtime.RestoreSnapshot(snapshot);

        Assert.Equal(1, first.Count);
        Assert.Equal(1, second.Count);
    }

    private static StateMachineDefinition CreateExitHierarchy()
    {
        return new StateMachineDefinition
        {
            RootMachineId = "root",
            Machines =
            {
                new MachineDefinition
                {
                    Id = "root",
                    InitialStateId = "active",
                    States =
                    {
                        Fixtures.State("active", requiresExitApproval: true, childMachine: "child"),
                        Fixtures.State("done"),
                    },
                    Transitions =
                    {
                        Fixtures.Transition("finish", "active", "done", condition: "finish"),
                    },
                },
                new MachineDefinition
                {
                    Id = "child",
                    InitialStateId = "inside",
                    States = { Fixtures.State("inside") },
                    Transitions =
                    {
                        new TransitionDefinition
                        {
                            Id = "exit",
                            FromStateId = "inside",
                            ExitMachine = true,
                            ActionKey = "child-exit",
                        },
                    },
                },
            },
        };
    }

    private sealed class GateState : RuntimeStateBase<TestOwner>
    {
        private readonly string _id;
        private readonly Func<bool> _canExit;

        public GateState(string id, Func<bool> canExit)
        {
            _id = id;
            _canExit = canExit;
        }

        public override void OnEnter(TestOwner owner, in TickContext context) => owner.Trace.Add("enter:" + _id);
        public override void OnTick(TestOwner owner, in TickContext context) => owner.Trace.Add("tick:" + _id);
        public override void OnExitRequested(TestOwner owner, in TickContext context) =>
            owner.Trace.Add("exit-requested:" + _id);
        public override bool CanExit(TestOwner owner, in TickContext context) => _canExit();
        public override void OnExit(TestOwner owner, in TickContext context) => owner.Trace.Add("exit:" + _id);
    }
}
