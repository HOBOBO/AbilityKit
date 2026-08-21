using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.BehaviorTree;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaBrainConfigurationValidatorTests
{
    [Fact]
    public void Validate_aggregates_profile_and_brain_reference_errors()
    {
        const string profileJson = """
            [
              {
                "id": "invalid-profile",
                "startState": "missing",
                "states": [
                  {
                    "id": "work",
                    "kind": "actionState",
                    "behaviorRoot": { "kind": "action", "type": "unregistered" }
                  }
                ]
              }
            ]
            """;

        var profiles = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(profileJson, profiles);
        var brains = new MobaActorBrainCatalog();
        var missingHfsm = new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "missing-profile");
        var missingBtree = new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "missing-tree");
        brains.Register(in missingHfsm);
        brains.Register(in missingBtree);

        var error = Assert.Throws<InvalidOperationException>(() => MobaBrainConfigurationValidator.Validate(
            brains,
            profiles,
            new MobaActorStateMachineRuntimeRegistry(),
            new MobaBrainDecisionDriverRegistry()));

        Assert.Contains("start state 'missing' does not exist", error.Message);
        Assert.Contains("action 'unregistered' is not registered", error.Message);
        Assert.Contains("missing HFSM profile 'missing-profile'", error.Message);
        Assert.Contains("unregistered driver 'behaviorTree'", error.Message);
    }

    [Fact]
    public void Blackboard_initialize_rejects_empty_declared_key()
    {
        var definition = new BtTreeDefinition();
        definition.Nodes.Add(new BtNodeDefinition { Id = "root", Type = "builtin.succeed" });
        definition.RootNodeId = "root";
        definition.Blackboard.Keys.Add(new BtBlackboardKeyDefinition { Name = "", Type = BtValueType.Int64 });

        var errors = BtTreeValidator.Validate(definition, new BtNodeRegistry());
        Assert.Contains(errors, e => e.Contains("must not be empty"));
    }

    [Fact]
    public void Blackboard_initialize_rejects_duplicate_declared_key()
    {
        var definition = new BtTreeDefinition();
        definition.Nodes.Add(new BtNodeDefinition { Id = "root", Type = "builtin.succeed" });
        definition.RootNodeId = "root";
        definition.Blackboard.Keys.Add(new BtBlackboardKeyDefinition { Name = "custom.value", Type = BtValueType.Int64 });
        definition.Blackboard.Keys.Add(new BtBlackboardKeyDefinition { Name = "custom.value", Type = BtValueType.Bool });

        var errors = BtTreeValidator.Validate(definition, new BtNodeRegistry());
        Assert.Contains(errors, e => e.Contains("duplicated") && e.Contains("custom.value"));
    }

    [Fact]
    public void Blackboard_initialize_rejects_reserved_key_with_wrong_type()
    {
        var definition = new BtTreeDefinition();
        definition.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
            { Name = MobaBTreeKeys.OwnerX, Type = BtValueType.Int64 });

        var error = Assert.Throws<InvalidOperationException>(
            () => MobaBTreeBlackboard.EnsureStandardSchema(definition));

        Assert.Contains(MobaBTreeKeys.OwnerX, error.Message);
        Assert.Contains(BtValueType.Fixed64.ToString(), error.Message);
    }
}
