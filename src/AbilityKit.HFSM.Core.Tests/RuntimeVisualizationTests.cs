using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Visualization;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

/// <summary>
/// Covers the runtime -> FsmSnapshot projection that the editor Runtime Monitor consumes.
/// Before this, the deterministic runtime exposed no visualization data at all.
/// </summary>
public sealed class RuntimeVisualizationTests
{
    [Fact]
    public void SnapshotExposesStructureActivePathAndTransitions()
    {
        var definition = Fixtures.Flat(Fixtures.State("a"), Fixtures.State("b"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("go", "a", "b"));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());
        runtime.Initialize(0, Fixed64.Zero);

        var snapshot = ((IVisualizationProvider)runtime).GetSnapshot();

        Assert.Contains(snapshot.states, state => state.path == "root" && state.isStateMachine);
        Assert.Contains(snapshot.states, state => state.path == "root/a" && !state.isStateMachine);
        Assert.Contains(snapshot.states, state => state.path == "root/b");
        Assert.Equal(new[] { "root/a" }, snapshot.activeStatePaths);
        Assert.Contains(snapshot.states, state => state.path == "root/a" && state.isActive);
        Assert.Contains(snapshot.states, state => state.path == "root/b" && !state.isActive);

        var transition = Assert.Single(snapshot.transitions);
        Assert.Equal("root/a", transition.fromPath);
        Assert.Equal("root/b", transition.toPath);
        Assert.True(transition.canTransition);
    }

    [Fact]
    public void CompletedTransitionIsRecordedWithFromState()
    {
        var definition = Fixtures.Flat(Fixtures.State("a"), Fixtures.State("b"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("go", "a", "b"));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());
        runtime.Initialize(0, Fixed64.Zero);

        var provider = (IVisualizationProvider)runtime;
        provider.GetSnapshot();

        runtime.Tick(1, Fixtures.Time(2));

        var snapshot = provider.GetSnapshot();
        Assert.Equal(new[] { "root/b" }, snapshot.activeStatePaths);

        var record = Assert.Single(snapshot.history);
        Assert.Equal("root/a", record.fromPath);
        Assert.Equal("root/b", record.toPath);
        Assert.Contains(snapshot.states, state => state.path == "root/b" && state.enterCount == 1);
    }

    [Fact]
    public void ActiveDurationUsesRuntimeClockNotNullWallClock()
    {
        var owner = new TestOwner { Finish = false };
        var definition = Fixtures.Flat(Fixtures.State("a"), Fixtures.State("b"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("go", "a", "b", condition: "finish"));
        var bindings = new RuntimeBindings<TestOwner>()
            .RegisterCondition("finish", () => new OwnerFlagCondition(value => value.Finish));
        var runtime = new StateMachineRuntime<TestOwner>(owner, definition, bindings);
        runtime.Initialize(10, Fixed64.Zero);
        runtime.Tick(11, Fixtures.Time(3));

        var snapshot = ((IVisualizationProvider)runtime).GetSnapshot();

        Assert.Equal(new[] { "root/a" }, snapshot.activeStatePaths);
        var active = Assert.Single(snapshot.states, state => state.path == "root/a");
        Assert.True(active.activeDuration >= 3f, $"expected >= 3, got {active.activeDuration}");
    }

    [Fact]
    public void UninitializedRuntimeStillReportsStaticStructure()
    {
        var definition = Fixtures.Flat(Fixtures.State("a"), Fixtures.State("b"));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());

        var snapshot = ((IVisualizationProvider)runtime).GetSnapshot();

        Assert.Equal(3, snapshot.states.Count);
        Assert.Empty(snapshot.activeStatePaths);
    }

    [Fact]
    public void SnapshotCarriesRuntimeFrameTimeAndDefinitionHash()
    {
        var definition = Fixtures.Flat(Fixtures.State("a"), Fixtures.State("b"));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());
        runtime.Initialize(7, Fixtures.Time(3));

        var snapshot = ((IVisualizationProvider)runtime).GetSnapshot();

        Assert.Equal(7, runtime.CurrentFrame);
        Assert.Equal(7, snapshot.frame);
        Assert.Equal(Fixtures.Time(3).RawValue, snapshot.timeRaw);
        Assert.Equal(runtime.DefinitionHash, snapshot.definitionHash);
        Assert.NotEqual(0L, snapshot.definitionHash);
    }
}
