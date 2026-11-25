using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class ExecutionNodeReferenceTests
{
    [Fact]
    public void Resolves_case_insensitive_references_in_field_order_without_sharing_runtime_nodes()
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""
            { "FormatVersion": 1,
              "Behaviors": { "shared": { "Kind": "Action", "Action": { "ActionId": 11 } }, "nodeOnly": null },
              "Nodes": {
                "shared": { "Kind": "Action", "Action": { "ActionId": 12 } },
                "nodeOnly": { "Kind": "Action", "Action": { "ActionId": 13 } },
                "alias": { "NodeId": "NODEONLY" }
              },
              "Triggers": [
                { "TriggerId": 1, "ExecutionRoot": { "Kind": "Sequence", "Children": [
                    { "BehaviorRef": "SHARED", "BehaviorId": "missing", "NodeRef": "missing" },
                    { "BehaviorId": "SHARED", "NodeRef": "missing" },
                    { "NodeRef": "SHARED", "NodeId": "missing", "Ref": "missing" },
                    { "NodeId": "SHARED", "Ref": "missing" },
                    { "Ref": "SHARED" }, { "Ref": "ALIAS" }, { "Ref": "ALIAS" }
                ] } },
                { "TriggerId": 2, "ExecutionRoot": { "Ref": "ALIAS" } }
              ] }
            """);

        var root = Assert.IsType<SequenceTriggerPlanExecutable>(database.Records[0].ExecutionRoot);
        Assert.Equal(new[] { 11, 11, 12, 12, 11, 13, 13 },
            root.Children.Select(child => Assert.IsType<ActionCallTriggerPlanExecutable>(child).Action.Id.Value));
        Assert.NotSame(root.Children[5], root.Children[6]);
        Assert.NotSame(root.Children[5], database.Records[1].ExecutionRoot);
        Assert.Equal(13, Assert.IsType<ActionCallTriggerPlanExecutable>(database.Records[1].ExecutionRoot).Action.Id.Value);
    }

    [Theory]
    [InlineData("BehaviorRef", "ONLY_NODE", "Behavior reference not found: ONLY_NODE")]
    [InlineData("NodeRef", "ONLY_BEHAVIOR", "Node reference not found: ONLY_BEHAVIOR")]
    [InlineData("Ref", "MISSING", "Execution node reference not found: MISSING")]
    public void Missing_references_keep_their_diagnostics_and_do_not_publish_partial_records(string field, string key, string message)
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""{ "FormatVersion": 1, "Triggers": [{ "TriggerId": 99 }] }""");
        var previous = database.Records;

        var error = Assert.Throws<InvalidOperationException>(() => database.LoadFromJson($$"""
            { "FormatVersion": 1,
              "Behaviors": { "only_behavior": { "Kind": "Succeed" }, "missing": null },
              "Nodes": { "only_node": { "Kind": "Succeed" } },
              "Triggers": [ { "TriggerId": 1 }, { "TriggerId": 2, "ExecutionRoot": { "{{field}}": "{{key}}" } } ] }
            """));

        Assert.Equal(message, error.Message);
        Assert.Same(previous, database.Records);
    }

    [Theory]
    [InlineData("""{ "BehaviorRef": "LOOP" }""")]
    [InlineData("""{ "Kind": "Sequence", "Children": [{ "BehaviorRef": "LOOP" }] }""")]
    public void Detects_case_insensitive_cycles_through_aliases_and_descendants(string node)
    {
        var database = new TriggerPlanJsonDatabase();
        var error = Assert.Throws<InvalidOperationException>(() => database.LoadFromJson($$"""
            { "FormatVersion": 1,
              "Behaviors": { "loop": { "NodeRef": "middle" } },
              "Nodes": { "middle": {{node}} },
              "Triggers": [{ "TriggerId": 1, "ExecutionRoot": { "BehaviorRef": "loop" } }] }
            """));

        Assert.Equal("Cyclic execution node reference detected: behavior:LOOP", error.Message);
        Assert.Empty(database.Records);
    }

    [Fact]
    public void Replacing_a_document_does_not_reuse_its_old_reference_index()
    {
        var database = new TriggerPlanJsonDatabase();
        const string first = """
            { "FormatVersion": 1, "Nodes": { "effect": { "Kind": "Action", "Action": { "ActionId": 4 } } },
              "Triggers": [{ "TriggerId": 1, "ExecutionRoot": { "NodeRef": "effect" } }] }
            """;
        database.LoadFromJson(first);
        var firstRoot = Assert.IsType<ActionCallTriggerPlanExecutable>(Assert.Single(database.Records).ExecutionRoot);
        database.LoadFromJson(first.Replace("\"ActionId\": 4", "\"ActionId\": 8"));
        var secondRoot = Assert.IsType<ActionCallTriggerPlanExecutable>(Assert.Single(database.Records).ExecutionRoot);

        Assert.Equal(4, firstRoot.Action.Id.Value);
        Assert.Equal(8, secondRoot.Action.Id.Value);
    }

    [Fact]
    public void Duplicate_catalog_keys_are_rejected_on_first_explicit_root_not_on_flat_only_load()
    {
        var database = new TriggerPlanJsonDatabase();
        const string flat = """
            { "FormatVersion": 1, "Nodes": { "duplicate": { "Kind": "Succeed" }, "DUPLICATE": { "Kind": "Fail" } },
              "Triggers": [{ "TriggerId": 1, "Actions": [{ "ActionId": 7 }] }] }
            """;
        database.LoadFromJson(flat);
        var previous = database.Records;

        Assert.Throws<ArgumentException>(() => database.LoadFromJson(flat.Replace(
            "\"Actions\": [{ \"ActionId\": 7 }]", "\"ExecutionRoot\": { \"Kind\": \"Succeed\" }")));
        Assert.Same(previous, database.Records);
    }
}
