using System;
using System.Linq;
using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaBootstrapStagePlanTests
{
    [Fact]
    public void Create_PreservesSourceOrderForIndependentStages()
    {
        var first = new TestStage("first");
        var second = new TestStage("second");
        var third = new TestStage("third");

        var plan = MobaBootstrapStagePlan.Create(new[] { first, second, third });

        Assert.Equal(new[] { first, second, third }, plan.OrderedStages);
    }

    [Fact]
    public void Create_OrdersDependenciesAndRetainsOriginalStageInstances()
    {
        var start = new TestStage("start", "world");
        var core = new TestStage("core");
        var world = new TestStage("world", "core");

        var plan = MobaBootstrapStagePlan.Create(new[] { start, core, world });

        Assert.Equal(new[] { core, world, start }, plan.OrderedStages);
        Assert.Same(core, plan.OrderedStages[0]);
        Assert.Same(world, plan.OrderedStages[1]);
        Assert.Same(start, plan.OrderedStages[2]);
    }

    [Fact]
    public void Create_RejectsMissingDependency()
    {
        var stage = new TestStage("start", "missing");

        var exception = Assert.Throws<InvalidOperationException>(
            () => MobaBootstrapStagePlan.Create(new[] { stage }));

        Assert.Contains("missing prerequisite 'missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_RejectsDependencyCycle()
    {
        var first = new TestStage("first", "second");
        var second = new TestStage("second", "first");

        var exception = Assert.Throws<InvalidOperationException>(
            () => MobaBootstrapStagePlan.Create(new[] { first, second }));

        Assert.Contains("dependency cycle", exception.Message, StringComparison.Ordinal);
        Assert.Contains("first", exception.Message, StringComparison.Ordinal);
        Assert.Contains("second", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_RejectsDuplicateStageNames()
    {
        var stages = Enumerable.Range(0, 2)
            .Select(_ => new TestStage("duplicate"))
            .ToArray();

        var exception = Assert.Throws<InvalidOperationException>(
            () => MobaBootstrapStagePlan.Create(stages));

        Assert.Contains("Duplicate MOBA bootstrap stage name 'duplicate'", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestStage : MobaBootstrapStageBase
    {
        private readonly string _name;
        private readonly string[] _dependencies;

        public TestStage(string name, params string[] dependencies)
        {
            _name = name;
            _dependencies = dependencies;
        }

        public override string Name => _name;

        public override string[] Dependencies => _dependencies;
    }
}
