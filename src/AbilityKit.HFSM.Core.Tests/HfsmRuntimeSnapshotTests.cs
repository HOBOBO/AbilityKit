using AbilityKit.Deterministic;
using AbilityKit.HFSM;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class HfsmRuntimeSnapshotTests
{
    [Fact]
    public void RestoreRecoversStructureClockAndStatePayloadWithoutLifecycleCallbacks()
    {
        var counter = new CounterState();
        var definition = HfsmFixtures.Flat(HfsmFixtures.State("count", "counter"));
        var runtime = new HfsmRuntime<TestOwner>(
            new TestOwner(),
            definition,
            new HfsmRuntimeBindings<TestOwner>().RegisterState("counter", () => counter));

        runtime.Initialize(0, Fixed64.Zero);
        runtime.Tick(1, Fixed64.One);
        runtime.Tick(2, HfsmFixtures.Time(2));
        var snapshot = runtime.CaptureSnapshot();
        runtime.Tick(3, HfsmFixtures.Time(3));
        Assert.Equal(3, counter.Count);

        runtime.RestoreSnapshot(snapshot);

        Assert.Equal(2, counter.Count);
        Assert.Equal(1, counter.EnterCount);
        Assert.Equal(0, counter.ExitCount);
        Assert.Equal(2, runtime.CurrentFrame);
        Assert.Equal(HfsmFixtures.Time(2).RawValue, runtime.CurrentTime.RawValue);
        Assert.Equal(new[] { "root/count" }, runtime.GetActivePath());
    }

    [Fact]
    public void InvalidSnapshotIsRejectedBeforeStructuralMutation()
    {
        var definition = HfsmFixtures.Flat(HfsmFixtures.State("idle"), HfsmFixtures.State("done"));
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition("go", "idle", "done", trigger: "go"));
        var runtime = new HfsmRuntime<TestOwner>(
            new TestOwner(), definition, new HfsmRuntimeBindings<TestOwner>());
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
        var firstDefinition = HfsmFixtures.Flat(HfsmFixtures.State("idle"));
        var first = new HfsmRuntime<TestOwner>(
            new TestOwner(), firstDefinition, new HfsmRuntimeBindings<TestOwner>());
        first.Initialize(0, Fixed64.Zero);

        var secondDefinition = HfsmFixtures.Flat(HfsmFixtures.State("idle"));
        secondDefinition.Machines[0].RememberLastState = true;
        var second = new HfsmRuntime<TestOwner>(
            new TestOwner(), secondDefinition, new HfsmRuntimeBindings<TestOwner>());

        Assert.Throws<InvalidOperationException>(() => second.RestoreSnapshot(first.CaptureSnapshot()));
    }

    [Fact]
    public void RestoreRejectsPreviousSnapshotProtocolVersion()
    {
        var runtime = new HfsmRuntime<TestOwner>(
            new TestOwner(),
            HfsmFixtures.Flat(HfsmFixtures.State("idle")),
            new HfsmRuntimeBindings<TestOwner>());
        var snapshot = runtime.CaptureSnapshot();
        snapshot.SnapshotVersion = 1;

        Assert.Throws<InvalidOperationException>(() => runtime.RestoreSnapshot(snapshot));
    }

    [Fact]
    public void RuntimeCompilesAnImmutableSemanticCopyOfDefinition()
    {
        var definition = HfsmFixtures.Flat(HfsmFixtures.State("idle"), HfsmFixtures.State("done"));
        definition.Machines[0].Transitions.Add(HfsmFixtures.Transition("go", "idle", "done", trigger: "go"));
        var runtime = new HfsmRuntime<TestOwner>(
            new TestOwner(), definition, new HfsmRuntimeBindings<TestOwner>());

        definition.Machines[0].Transitions[0].ToStateId = "idle";
        runtime.Initialize(0, Fixed64.Zero);
        runtime.Trigger("go");

        Assert.Equal(new[] { "root/done" }, runtime.GetActivePath());
    }
}
