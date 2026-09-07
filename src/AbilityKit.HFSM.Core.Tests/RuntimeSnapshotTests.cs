using AbilityKit.Deterministic;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class RuntimeSnapshotTests
{
    [Fact]
    public void RestoreRecoversStructureClockAndStatePayloadWithoutLifecycleCallbacks()
    {
        var counter = new CounterState();
        var definition = Fixtures.Flat(Fixtures.State("count", "counter"));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(),
            definition,
            new RuntimeBindings<TestOwner>().RegisterState("counter", () => counter));

        runtime.Initialize(0, Fixed64.Zero);
        runtime.Tick(1, Fixed64.One);
        runtime.Tick(2, Fixtures.Time(2));
        var snapshot = runtime.CaptureSnapshot();
        runtime.Tick(3, Fixtures.Time(3));
        Assert.Equal(3, counter.Count);

        runtime.RestoreSnapshot(snapshot);

        Assert.Equal(2, counter.Count);
        Assert.Equal(1, counter.EnterCount);
        Assert.Equal(0, counter.ExitCount);
        Assert.Equal(2, runtime.CurrentFrame);
        Assert.Equal(Fixtures.Time(2).RawValue, runtime.CurrentTime.RawValue);
        Assert.Equal(new[] { "root/count" }, runtime.GetActivePath());
    }

    [Fact]
    public void InvalidSnapshotIsRejectedBeforeStructuralMutation()
    {
        var definition = Fixtures.Flat(Fixtures.State("idle"), Fixtures.State("done"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("go", "idle", "done", trigger: "go"));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());
        runtime.Initialize(0, Fixed64.Zero);
        var snapshot = runtime.CaptureSnapshot();
        snapshot.Machines[0].ActiveStateId = "missing";

        Assert.Throws<InvalidOperationException>(() => runtime.RestoreSnapshot(snapshot));
        Assert.Equal(new[] { "root/idle" }, runtime.GetActivePath());
        Assert.False(runtime.IsFaulted);
    }

    [Fact]
    public void DefinitionHashPreventsCrossDefinitionRestore()
    {
        var firstDefinition = Fixtures.Flat(Fixtures.State("idle"));
        var first = new StateMachineRuntime<TestOwner>(
            new TestOwner(), firstDefinition, new RuntimeBindings<TestOwner>());
        first.Initialize(0, Fixed64.Zero);

        var secondDefinition = Fixtures.Flat(Fixtures.State("idle"));
        secondDefinition.Machines[0].RememberLastState = true;
        var second = new StateMachineRuntime<TestOwner>(
            new TestOwner(), secondDefinition, new RuntimeBindings<TestOwner>());

        Assert.Throws<InvalidOperationException>(() => second.RestoreSnapshot(first.CaptureSnapshot()));
    }

    [Fact]
    public void RestoreRejectsPreviousSnapshotProtocolVersion()
    {
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(),
            Fixtures.Flat(Fixtures.State("idle")),
            new RuntimeBindings<TestOwner>());
        var snapshot = runtime.CaptureSnapshot();
        snapshot.SnapshotVersion = 1;

        Assert.Throws<InvalidOperationException>(() => runtime.RestoreSnapshot(snapshot));
    }

    [Fact]
    public void RuntimeCompilesAnImmutableSemanticCopyOfDefinition()
    {
        var definition = Fixtures.Flat(Fixtures.State("idle"), Fixtures.State("done"));
        definition.Machines[0].Transitions.Add(Fixtures.Transition("go", "idle", "done", trigger: "go"));
        var runtime = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());

        definition.Machines[0].Transitions[0].ToStateId = "idle";
        runtime.Initialize(0, Fixed64.Zero);
        runtime.Trigger("go");

        Assert.Equal(new[] { "root/done" }, runtime.GetActivePath());
    }
}
