using AbilityKit.Deterministic;
using AbilityKit.HFSM;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class HfsmRuntimeExecutionTests
{
    [Fact]
    public void HierarchyUsesRootFirstTransitionsAndLeafFirstExitOrder()
    {
        var owner = new TestOwner { AllowExit = true };
        var definition = CreateHierarchyDefinition();
        var bindings = new HfsmRuntimeBindings<TestOwner>()
            .RegisterState("root-active", () => new TraceState("root-active"))
            .RegisterState("root-done", () => new TraceState("root-done"))
            .RegisterState("child-idle", () => new TraceState("child-idle"))
            .RegisterState("child-attack", () => new TraceState("child-attack"))
            .RegisterCondition("finish", () => new OwnerFlagCondition(value => value.Finish));
        var runtime = new HfsmRuntime<TestOwner>(owner, definition, bindings);

        runtime.Initialize(0, Fixed64.Zero);
        Assert.Equal(new[] { "root/active", "combat/idle" }, runtime.GetActivePath());

        Assert.True(runtime.Trigger("attack"));
        Assert.Equal(new[] { "root/active", "combat/attack" }, runtime.GetActivePath());

        owner.Trace.Clear();
        owner.Finish = true;
        runtime.Tick(1, HfsmFixtures.Time(1));

        Assert.Equal(new[] { "root/done" }, runtime.GetActivePath());
        Assert.Equal(
            new[] { "exit:child-attack", "exit:root-active", "enter:root-done", "tick:root-done" },
            owner.Trace);
    }

    [Fact]
    public void EqualPriorityTransitionsUseStableOrdinalIdOrder()
    {
        var owner = new TestOwner();
        var definition = HfsmFixtures.Flat(
            HfsmFixtures.State("a"),
            HfsmFixtures.State("b"),
            HfsmFixtures.State("c"));
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition("z-last", "a", "c"));
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition("a-first", "a", "b"));
        var runtime = new HfsmRuntime<TestOwner>(owner, definition, new HfsmRuntimeBindings<TestOwner>());

        runtime.Initialize(0, Fixed64.Zero);
        runtime.Tick(1, Fixed64.Zero);

        Assert.Equal(new[] { "root/b" }, runtime.GetActivePath());
    }

    [Fact]
    public void FixedPointMinimumDurationUsesRawTimeWithoutWallClock()
    {
        var definition = HfsmFixtures.Flat(HfsmFixtures.State("wait"), HfsmFixtures.State("done"));
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition(
            "elapsed",
            "wait",
            "done",
            minimumDurationRaw: Fixed64.One.RawValue));
        var runtime = new HfsmRuntime<TestOwner>(
            new TestOwner(),
            definition,
            new HfsmRuntimeBindings<TestOwner>());

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
        var definition = HfsmFixtures.Flat(
            HfsmFixtures.State("cast", "cast", requiresExitApproval: true),
            HfsmFixtures.State("idle"),
            HfsmFixtures.State("dead"));
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition("finish", "cast", "idle", trigger: "finish"));
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition(
            "death", "", "dead", trigger: "death", fromAny: true, force: true));
        var runtime = new HfsmRuntime<TestOwner>(
            owner,
            definition,
            new HfsmRuntimeBindings<TestOwner>().RegisterState("cast", () => new TraceState("cast")));

        runtime.Initialize(0, Fixed64.Zero);
        Assert.True(runtime.Trigger("finish"));
        Assert.Equal("finish", runtime.CaptureSnapshot().Machines.Single().PendingTransitionId);
        Assert.Equal(new[] { "root/cast" }, runtime.GetActivePath());

        Assert.True(runtime.Trigger("death"));
        Assert.Equal(new[] { "root/dead" }, runtime.GetActivePath());
        Assert.Empty(runtime.CaptureSnapshot().Machines.Single().PendingTransitionId);
    }

    [Fact]
    public void ObserverFailureIsIsolatedButStateFailureFaultsRuntime()
    {
        var safe = new HfsmRuntime<TestOwner>(
            new TestOwner(),
            HfsmFixtures.Flat(HfsmFixtures.State("idle")),
            new HfsmRuntimeBindings<TestOwner>());
        safe.AddObserver(new ThrowingObserver());
        safe.Initialize(0, Fixed64.Zero);
        safe.Tick(1, Fixed64.One);
        Assert.False(safe.IsFaulted);

        var failing = new HfsmRuntime<TestOwner>(
            new TestOwner(),
            HfsmFixtures.Flat(HfsmFixtures.State("bad", "bad")),
            new HfsmRuntimeBindings<TestOwner>().RegisterState("bad", () => new ThrowingState()));
        failing.Initialize(0, Fixed64.Zero);
        Assert.Throws<InvalidOperationException>(() => failing.Tick(1, Fixed64.One));
        Assert.True(failing.IsFaulted);
        Assert.Throws<HfsmRuntimeFaultedException>(() => failing.Tick(2, HfsmFixtures.Time(2)));
    }

    private static HfsmDefinition CreateHierarchyDefinition()
    {
        return new HfsmDefinition
        {
            RootMachineId = "root",
            Machines =
            {
                new HfsmMachineDefinition
                {
                    Id = "root",
                    InitialStateId = "active",
                    States =
                    {
                        HfsmFixtures.State("active", "root-active", childMachine: "combat"),
                        HfsmFixtures.State("done", "root-done"),
                    },
                    Transitions =
                    {
                        HfsmFixtures.Transition("finish", "active", "done", condition: "finish"),
                    },
                },
                new HfsmMachineDefinition
                {
                    Id = "combat",
                    InitialStateId = "idle",
                    States =
                    {
                        HfsmFixtures.State("idle", "child-idle"),
                        HfsmFixtures.State("attack", "child-attack"),
                    },
                    Transitions =
                    {
                        HfsmFixtures.Transition("attack", "idle", "attack", trigger: "attack"),
                    },
                },
            },
        };
    }
}
