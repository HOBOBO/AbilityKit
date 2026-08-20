using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using AbilityKit.Demo.Moba.Services.StateMachine;
using BTCore.Runtime.Blackboards;
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
        var blackboard = new Blackboard();
        blackboard.Values.Add(new BlackboardValue<int>(""));

        var error = Assert.Throws<InvalidOperationException>(() => MobaBTreeBlackboard.Initialize(blackboard));

        Assert.Contains("empty name", error.Message);
    }

    [Fact]
    public void Blackboard_initialize_rejects_duplicate_declared_key()
    {
        var blackboard = new Blackboard();
        blackboard.Values.Add(new BlackboardValue<int>("custom.value"));
        blackboard.Values.Add(new BlackboardValue<bool>("custom.value"));

        var error = Assert.Throws<InvalidOperationException>(() => MobaBTreeBlackboard.Initialize(blackboard));

        Assert.Contains("duplicated", error.Message);
        Assert.Contains("custom.value", error.Message);
    }

    [Fact]
    public void Blackboard_initialize_rejects_reserved_key_with_wrong_type()
    {
        var blackboard = new Blackboard();
        blackboard.Values.Add(new BlackboardValue<int>(MobaBTreeKeys.OwnerX));

        var error = Assert.Throws<InvalidOperationException>(() => MobaBTreeBlackboard.Initialize(blackboard));

        Assert.Contains(MobaBTreeKeys.OwnerX, error.Message);
        Assert.Contains(typeof(float).FullName, error.Message);
    }
}
