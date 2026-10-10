using System;
using System.Collections.Generic;
using AbilityKit.Game.View.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class PhaseStateFeatureBindingTests
{
    [Fact]
    public void Enter_ClearsBeforeInstallingFeatures()
    {
        var events = new List<string>();
        var ctx = new TestContext(2);
        var plan = new PhaseFeaturePlan<TestContext, IPhaseFeature<TestContext>>()
            .Add("first", (in TestContext c) => new TestFeature("first", c.Value, events))
            .Add("second", (in TestContext c) => new TestFeature("second", c.Value, events));
        var binding = new PhaseStateFeatureBinding<TestContext, IPhaseFeature<TestContext>>(
            "test",
            feature => feature.OnAttach(in ctx),
            plan,
            clearBeforeEnter: true,
            clear: (in TestContext c) => events.Add($"clear:{c.Value}"));

        var installed = binding.Enter(in ctx);

        Assert.Equal(2, installed);
        Assert.Equal(new[] { "clear:2", "attach:first:2", "attach:second:2" }, events);
    }

    [Fact]
    public void Enter_WithFeatureIds_InstallsSelectedFeaturesInRequestedOrder()
    {
        var events = new List<string>();
        var failures = new List<string>();
        var ctx = new TestContext(5);
        var plan = new PhaseFeaturePlan<TestContext, IPhaseFeature<TestContext>>()
            .Add("first", (in TestContext c) => new TestFeature("first", c.Value, events))
            .Add("second", (in TestContext c) => new TestFeature("second", c.Value, events))
            .Add("third", (in TestContext c) => new TestFeature("third", c.Value, events));
        var binding = new PhaseStateFeatureBinding<TestContext, IPhaseFeature<TestContext>>(
            "test",
            feature => feature.OnAttach(in ctx),
            plan,
            new[] { "third", "missing", "first" },
            fail: failures.Add);

        var installed = binding.Enter(in ctx);

        Assert.Equal(2, installed);
        Assert.Equal(new[] { "attach:third:5", "attach:first:5" }, events);
        Assert.Equal(new[] { "Phase feature id not registered: missing" }, failures);
    }

    [Fact]
    public void Enter_WithEmptyFeatureIds_InstallsNothingAndRunsCompletion()
    {
        var events = new List<string>();
        var ctx = new TestContext(5);
        var plan = new PhaseFeaturePlan<TestContext, IPhaseFeature<TestContext>>()
            .Add("first", (in TestContext c) => new TestFeature("first", c.Value, events));
        var binding = new PhaseStateFeatureBinding<TestContext, IPhaseFeature<TestContext>>(
            "test",
            feature => feature.OnAttach(in ctx),
            plan,
            Array.Empty<string>(),
            afterEnter: (in TestContext c, int count) => events.Add($"after:{c.Value}:{count}"));

        var installed = binding.Enter(in ctx);

        Assert.Equal(0, installed);
        Assert.Equal(new[] { "after:5:0" }, events);
    }

    [Fact]
    public void Enter_RunsHooksAroundFeatureInstallation()
    {
        var events = new List<string>();
        var ctx = new TestContext(9);
        var plan = new PhaseFeaturePlan<TestContext, IPhaseFeature<TestContext>>()
            .Add("feature", (in TestContext c) => new TestFeature("feature", c.Value, events));
        var binding = new PhaseStateFeatureBinding<TestContext, IPhaseFeature<TestContext>>(
            "test",
            feature => feature.OnAttach(in ctx),
            plan,
            beforeEnter: (in TestContext c) => events.Add($"before:{c.Value}"),
            afterEnter: (in TestContext c, int count) => events.Add($"after:{c.Value}:{count}"));

        var installed = binding.Enter(in ctx);

        Assert.Equal(1, installed);
        Assert.Equal(new[] { "before:9", "attach:feature:9", "after:9:1" }, events);
    }

    [Fact]
    public void Enter_WithClearBeforeEnterAndNoClearCallback_Throws()
    {
        var ctx = new TestContext(1);
        var binding = new PhaseStateFeatureBinding<TestContext, IPhaseFeature<TestContext>>(
            "test",
            _ => { },
            clearBeforeEnter: true);
 
        Assert.Throws<InvalidOperationException>(() => binding.Enter(in ctx));
    }
 
    [Fact]
    public void Exit_RunsExitHook()
    {
        var events = new List<string>();
        var ctx = new TestContext(6);
        var binding = new PhaseStateFeatureBinding<TestContext, IPhaseFeature<TestContext>>(
            "test",
            _ => { },
            onExit: (in TestContext c) => events.Add($"exit:{c.Value}"));
 
        binding.Exit(in ctx);
 
        Assert.Equal(new[] { "exit:6" }, events);
    }

    private readonly record struct TestContext(int Value);

    private sealed class TestFeature : IPhaseFeature<TestContext>
    {
        private readonly string _id;
        private readonly int _createdWith;
        private readonly List<string> _events;

        public TestFeature(string id, int createdWith, List<string> events)
        {
            _id = id;
            _createdWith = createdWith;
            _events = events;
        }

        public void OnAttach(in TestContext ctx)
        {
            _events.Add($"attach:{_id}:{_createdWith}");
        }

        public void OnDetach(in TestContext ctx)
        {
            _events.Add($"detach:{_id}:{ctx.Value}");
        }

        public void Tick(in TestContext ctx, float deltaTime)
        {
            _events.Add($"tick:{_id}:{deltaTime:0.00}");
        }
    }
}
