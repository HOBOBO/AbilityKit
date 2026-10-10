using System.Collections.Generic;
using AbilityKit.Game.View.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class PhaseFeaturePlanTests
{
    [Fact]
    public void InstallByIdsOrAll_WithNoIds_InstallsAllInRegistrationOrder()
    {
        var events = new List<string>();
        var plan = new PhaseFeaturePlan<TestContext, IPhaseFeature<TestContext>>()
            .Add("first", (in TestContext ctx) => new PlanFeature("first", ctx.Value, events))
            .Add("second", (in TestContext ctx) => new PlanFeature("second", ctx.Value, events));
        var ctx = new TestContext(7);

        var installed = plan.InstallByIdsOrAll(null, in ctx, feature => feature.OnAttach(in ctx));

        Assert.Equal(2, installed);
        Assert.Equal(new[] { "attach:first:7", "attach:second:7" }, events);
    }

    [Fact]
    public void InstallByIdsOrAll_WithEmptyIds_InstallsNothing()
    {
        var events = new List<string>();
        var plan = new PhaseFeaturePlan<TestContext, IPhaseFeature<TestContext>>()
            .Add("first", (in TestContext ctx) => new PlanFeature("first", ctx.Value, events));
        var ctx = new TestContext(7);

        var installed = plan.InstallByIdsOrAll(System.Array.Empty<string>(), in ctx,
            feature => feature.OnAttach(in ctx));

        Assert.Equal(0, installed);
        Assert.Empty(events);
    }

    [Fact]
    public void InstallByIdsOrAll_WithIds_InstallsSelectedFeaturesInRequestedOrder()
    {
        var events = new List<string>();
        var failures = new List<string>();
        var plan = new PhaseFeaturePlan<TestContext, IPhaseFeature<TestContext>>()
            .Add("first", (in TestContext ctx) => new PlanFeature("first", ctx.Value, events))
            .Add("second", (in TestContext ctx) => new PlanFeature("second", ctx.Value, events))
            .Add("third", (in TestContext ctx) => new PlanFeature("third", ctx.Value, events));
        var ctx = new TestContext(3);

        var installed = plan.InstallByIdsOrAll(
            new[] { "third", "missing", "first" },
            in ctx,
            feature => feature.OnAttach(in ctx),
            failures.Add);

        Assert.Equal(2, installed);
        Assert.Equal(new[] { "attach:third:3", "attach:first:3" }, events);
        Assert.Equal(new[] { "Phase feature id not registered: missing" }, failures);
    }

    [Fact]
    public void TryCreate_ReturnsFeatureForRegisteredId()
    {
        var events = new List<string>();
        var plan = new PhaseFeaturePlan<TestContext, IPhaseFeature<TestContext>>()
            .Add("feature", (in TestContext ctx) => new PlanFeature("feature", ctx.Value, events));
        var ctx = new TestContext(5);

        var found = plan.TryCreate("feature", in ctx, out var feature);

        Assert.True(found);
        Assert.NotNull(feature);
        feature!.OnAttach(in ctx);
        Assert.Equal(new[] { "attach:feature:5" }, events);
    }

    private readonly record struct TestContext(int Value);

    private sealed class PlanFeature : IPhaseFeature<TestContext>
    {
        private readonly string _id;
        private readonly int _createdWith;
        private readonly List<string> _events;

        public PlanFeature(string id, int createdWith, List<string> events)
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
