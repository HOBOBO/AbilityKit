using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation.Editor;
using AbilityKit.BehaviorTree.Serialization;
using AbilityKit.Deterministic;
using Xunit;

namespace AbilityKit.BehaviorTree.Tests;

public sealed class BehaviorTreeShowcaseTests
{
    private static AuthoringSourceDocument Source() => AuthoringJson.Load(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "complete_runtime_observation.json")));

    private static NodeRegistry Registry()
    {
        var registry = new NodeRegistry();
        BuiltInNodes.RegisterAll(registry);
        return registry;
    }

    [Fact]
    public void AllShowcaseTrees_ExportAndLoadIntoRuntime()
    {
        var source = Source();
        var patrol = BehaviorTreeShowcaseDocuments.BuildPatrol(source);
        var chase = BehaviorTreeShowcaseDocuments.BuildChase(source);
        Assert.Equal(26, source.Tree.Nodes.Count);
        Assert.Equal(8, patrol.Tree.Nodes.Count);
        Assert.Contains(chase.Tree.Nodes, node => node.Id == "chase");
        Assert.Contains(chase.Tree.Nodes, node => node.Id == "idle");
        Assert.Equal(26, source.Tree.Nodes.Count);

        foreach (var document in new[] { source, patrol, chase })
        {
            var registry = Registry();
            var json = TreeExporter.Export(document, registry, out var errors);
            Assert.Empty(errors);
            Assert.NotNull(json);
            using var runtime = TreeRuntime.Create(TreeJson.Load(json!), registry,
                options: new TreeRunOptions { RestartWhenComplete = true });
            runtime.Enable();
            runtime.Update(1, Fixed64.FromRatio(1, 30));
            Assert.False(runtime.IsFaulted);
            Assert.Equal(document.Tree.TreeId, runtime.Definition.TreeId);
        }
    }

    [Theory]
    [InlineData("patrol", false, 8, 100, "Patrol")]
    [InlineData("chase", false, 8, 100, "Idle")]
    [InlineData("chase", true, 8, 100, "Chase")]
    [InlineData("chase", true, 3, 100, "Attack")]
    [InlineData("decision", true, 3, 20, "Retreat")]
    public void ObservationInputs_SelectVisibleOutput(string example, bool hasTarget,
        int distance, int health, string expectedMode)
    {
        var source = Source();
        var document = example switch
        {
            "patrol" => BehaviorTreeShowcaseDocuments.BuildPatrol(source),
            "chase" => BehaviorTreeShowcaseDocuments.BuildChase(source),
            _ => source,
        };
        var registry = Registry();
        var json = TreeExporter.Export(document, registry, out var errors);
        Assert.Empty(errors);
        using var runtime = TreeRuntime.Create(TreeJson.Load(json!), registry,
            options: new TreeRunOptions { RestartWhenComplete = true });
        runtime.Blackboard.SetInt64("self.health", health);
        runtime.Blackboard.SetBool("self.hasTarget", hasTarget);
        runtime.Blackboard.SetBool("self.canAct", true);
        runtime.Blackboard.SetFixed64("self.targetDistance", Fixed64.FromInt32(distance));
        runtime.Enable();
        runtime.Update(1, Fixed64.FromRatio(1, 30));
        Assert.Equal(expectedMode, runtime.Blackboard.GetString("out.mode"));
    }

    [Fact]
    public void Chase_AbortsAttackAndClearsBusyWhenTargetMovesAway()
    {
        var document = BehaviorTreeShowcaseDocuments.BuildChase(Source());
        var registry = Registry();
        var json = TreeExporter.Export(document, registry, out var errors);
        Assert.Empty(errors);
        using var runtime = TreeRuntime.Create(TreeJson.Load(json!), registry);
        runtime.Blackboard.SetBool("self.hasTarget", true);
        runtime.Blackboard.SetBool("self.canAct", true);
        runtime.Blackboard.SetFixed64("self.targetDistance", Fixed64.FromInt32(3));
        runtime.Enable();
        runtime.Update(1, Fixed64.FromRatio(1, 30));
        Assert.Equal("Attack", runtime.Blackboard.GetString("out.mode"));
        Assert.True(runtime.Blackboard.GetBool("out.busy"));

        runtime.Blackboard.SetFixed64("self.targetDistance", Fixed64.FromInt32(8));
        runtime.Update(2, Fixed64.FromRatio(2, 30));
        Assert.Equal("Chase", runtime.Blackboard.GetString("out.mode"));
        Assert.False(runtime.Blackboard.GetBool("out.busy"));
    }
}
