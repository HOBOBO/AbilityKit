using AbilityKit.Deterministic;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class RuntimeExecutionTests
{
    [Fact]
    public void HierarchyUsesRootFirstTransitionsAndLeafFirstExitOrder()
    {
        var owner = new TestOwner { AllowExit = true };
        var definition = CreateHierarchyDefinition();
        var bindings = new RuntimeBindings<TestOwner>()
            .RegisterState("root-active", () => new TraceState("root-active"))
            .RegisterState("root-done", () => new TraceState("root-done"))
            .RegisterState("child-idle", () => new TraceState("child-idle"))
            .RegisterState("child-attack", () => new TraceState("child-attack"))
            .RegisterCondition("finish", () => new OwnerFlagCondition(value => value.Finish));
        var runtime = new StateMachineRuntime<TestOwner>(owner, definition, bindings);

        runtime.Initialize(0, Fixed64.Zero);
        Assert.Equal(new[] { "root/active", "combat/idle" }, runtime.GetActivePath());

        Assert.True(runtime.Trigger("attack"));
        Assert.Equal(new[] { "root/active", "combat/attack" }, runtime.GetActivePath());

        owner.Trace.Clear();
        owner.Finish = true;
        runtime.Tick(1, Fixtures.Time(1));

        Assert.Equal(new[] { "root/done" }, runtime.GetActivePath());
        Assert.Equal(
            new[] { "exit:child-attack", "exit:root-active", "enter:root-done", "tick:root-done" },
            owner.Trace);
    }

    [Fact]
    public void EqualPriorityTransitionsUseStableOrdinalIdOrder()
    {
        var owner = new TestOwner();
        var definition = Fixtures.Flat(
            Fixtures.State("a"),
            Fixtures.State("b"),
            Fixtures.State("c"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("z-last", "a", "c"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("a-first", "a", "b"));
        var runtime = new StateMachineRuntime<TestOwner>(owner, definition, new RuntimeBindings<TestOwner>());

        runtime.Initialize(0, Fixed64.Zero);
        runtime.Tick(1, Fixed64.Zero);

        Assert.Equal(new[] { "root/b" }, runtime.GetActivePath());
    }

    [Fact]
    public void FixedPointMinimumDurationUsesRawTimeWithoutWallClock()
    {
        var definition = Fixtures.Flat(Fixtures.State("wait"), Fixtures.State("done"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition(
            "elapsed",
            "wait",
            "done",
            minimumDurationRaw: Fixed64.One.RawValue));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(),
            definition,
            new RuntimeBindings<TestOwner>());

        runtime.Initialize(10, Fixed64.Zero);
        runtime.Tick(11, Fixed64.Half);
        Assert.Equal(new[] { "root/wait" }, runtime.GetActivePath());

        runtime.Tick(12, Fixed64.One);
        Assert.Equal(new[] { "root/done" }, runtime.GetActivePath());
    }

    [Fact]
    public void PendingTransitionWaitsForApprovalAndForcedTriggerOverridesIt()
    {
        var owner = new TestOwner();
        var definition = Fixtures.Flat(
            Fixtures.State("cast", "cast", requiresExitApproval: true),
            Fixtures.State("idle"),
            Fixtures.State("dead"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("finish", "cast", "idle", trigger: "finish"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition(
            "death", "", "dead", trigger: "death", fromAny: true, force: true));
        var runtime = new StateMachineRuntime<TestOwner>(
            owner,
            definition,
            new RuntimeBindings<TestOwner>().RegisterState("cast", () => new TraceState("cast")));

        runtime.Initialize(0, Fixed64.Zero);
        Assert.True(runtime.Trigger("finish"));
        Assert.Equal("finish", runtime.CaptureSnapshot().Machines.Single().PendingTransitionId);
        Assert.Equal(new[] { "root/cast" }, runtime.GetActivePath());

        Assert.True(runtime.Trigger("death"));
        Assert.Equal(new[] { "root/dead" }, runtime.GetActivePath());
        Assert.Empty(runtime.CaptureSnapshot().Machines.Single().PendingTransitionId);
    }

    [Fact]
    public void PendingTriggerContextSurvivesSnapshotRestoreUntilApproval()
    {
        var owner = new TestOwner();
        var definition = Fixtures.Flat(
            Fixtures.State("cast", "cast", requiresExitApproval: true),
            Fixtures.State("idle"));
        definition.Machines[0].Transitions.Add(new TransitionDefinition
        {
            Id = "finish",
            FromStateId = "cast",
            ToStateId = "idle",
            TriggerId = "finish-cast",
            ActionKey = "trace",
        });
        var bindings = new RuntimeBindings<TestOwner>()
            .RegisterState("cast", () => new TraceState("cast"))
            .RegisterAction("trace", () => new TraceTransitionAction());
        var source = new StateMachineRuntime<TestOwner>(owner, definition, bindings);
        source.Initialize(0, Fixed64.Zero);

        Assert.True(source.Trigger("finish-cast"));
        var snapshot = source.CaptureSnapshot();
        Assert.Equal("finish-cast", snapshot.Machines.Single().PendingTriggerId);

        var restored = new StateMachineRuntime<TestOwner>(owner, definition, bindings);
        var observer = new RecordingObserver();
        restored.AddObserver(observer);
        restored.RestoreSnapshot(snapshot);
        owner.AllowExit = true;
        owner.Trace.Clear();
        restored.Tick(1, Fixed64.One);

        Assert.Equal(new[] { "tick:cast", "before:finish-cast", "exit:cast", "after:finish-cast" }, owner.Trace);
        var completed = Assert.Single(observer.Events,
            item => item.Type == RuntimeEventType.TransitionCompleted);
        Assert.Equal("finish-cast", completed.TriggerId);
    }

    [Fact]
    public void ObserverFailureIsIsolatedButStateFailureFaultsRuntime()
    {
        var safe = new StateMachineRuntime<TestOwner>(
            new TestOwner(),
            Fixtures.Flat(Fixtures.State("idle")),
            new RuntimeBindings<TestOwner>());
        safe.AddObserver(new ThrowingObserver());
        safe.Initialize(0, Fixed64.Zero);
        safe.Tick(1, Fixed64.One);
        Assert.False(safe.IsFaulted);

        var failing = new StateMachineRuntime<TestOwner>(
            new TestOwner(),
            Fixtures.Flat(Fixtures.State("bad", "bad")),
            new RuntimeBindings<TestOwner>().RegisterState("bad", () => new ThrowingState()));
        failing.Initialize(0, Fixed64.Zero);
        Assert.Throws<InvalidOperationException>(() => failing.Tick(1, Fixed64.One));
        Assert.True(failing.IsFaulted);
        Assert.Throws<RuntimeFaultedException>(() => failing.Tick(2, Fixtures.Time(2)));
    }

    private static StateMachineDefinition CreateHierarchyDefinition()
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
                        Fixtures.State("active", "root-active", childMachine: "combat"),
                        Fixtures.State("done", "root-done"),
                    },
                    Transitions =
                    {
                        Fixtures.Transition("finish", "active", "done", condition: "finish"),
                    },
                },
                new MachineDefinition
                {
                    Id = "combat",
                    InitialStateId = "idle",
                    States =
                    {
                        Fixtures.State("idle", "child-idle"),
                        Fixtures.State("attack", "child-attack"),
                    },
                    Transitions =
                    {
                        Fixtures.Transition("attack", "idle", "attack", trigger: "attack"),
                    },
                },
            },
        };
    }
}
