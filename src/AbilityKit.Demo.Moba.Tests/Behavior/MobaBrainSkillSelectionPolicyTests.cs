using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaBrainSkillSelectionPolicyTests
{
    [Fact]
    public void First_ready_policy_selects_lowest_slot()
    {
        var candidates = new[]
        {
            new MobaSkillSelectionCandidate(1002, 2, 12f),
            new MobaSkillSelectionCandidate(1001, 1, 5f),
        };

        var selected = MobaBrainSkillSelectionPolicies.TrySelect(
            MobaBrainSkillSelectionPolicy.FirstReady,
            candidates,
            out var candidate);

        Assert.True(selected);
        Assert.Equal(1001, candidate.SkillId);
        Assert.Equal(1, candidate.Slot);
    }

    [Fact]
    public void Highest_range_policy_selects_longest_range_with_slot_tiebreaker()
    {
        var candidates = new[]
        {
            new MobaSkillSelectionCandidate(1003, 3, 12f),
            new MobaSkillSelectionCandidate(1002, 2, 18f),
            new MobaSkillSelectionCandidate(1001, 1, 18f),
        };

        var selected = MobaBrainSkillSelectionPolicies.TrySelect(
            MobaBrainSkillSelectionPolicy.HighestRange,
            candidates,
            out var candidate);

        Assert.True(selected);
        Assert.Equal(1001, candidate.SkillId);
        Assert.Equal(1, candidate.Slot);
    }

    [Fact]
    public void Highest_range_policy_flows_to_btree_skill_selection_blackboard()
    {
        var actorContext = new ActorContext();
        var actor = actorContext.CreateEntity();
        actor.AddActorId(1);
        actor.AddSkillLoadout(
            new[]
            {
                new ActiveSkillRuntime { SkillId = 1001 },
                new ActiveSkillRuntime { SkillId = 1002 },
            },
            Array.Empty<PassiveSkillRuntime>());

        var registry = new MobaActorRegistry();
        registry.Register(1, actor);
        var decision = MobaBTreeDecision.Create(
            CreateSelectReadySkillTreeJson(),
            registry,
            CreateConfigDatabase(),
            skillSelectionPolicy: MobaBrainSkillSelectionPolicy.HighestRange);
        Assert.NotNull(decision);

        var manager = new BehaviorManager();
        manager.CreateBehavior(new BehaviorCreateConfig
        {
            BehaviorKind = "test",
            OwnerId = new BehaviorEntityId(1),
            Decision = decision,
            World = new DefaultWorldQuery(),
        });

        manager.Tick(0.016f, frame: 1);

        Assert.True(decision.Blackboard.GetBool(MobaBTreeKeys.SkillValid));
        Assert.Equal(1002, decision.Blackboard.GetInt64(MobaBTreeKeys.SkillId));
        Assert.Equal(2, decision.Blackboard.GetInt64(MobaBTreeKeys.SkillSlot));
        Assert.Equal(12f, decision.Blackboard.GetFixed64(MobaBTreeKeys.SkillRange).ToSingle());
    }

    [Fact]
    public void Brain_catalog_parses_optional_skill_selection_policy()
    {
        const string json = """
            [{
              "BrainId": 17,
              "DriverKind": "behaviorTree",
              "DecisionName": "test_tree",
              "SkillSelectionPolicy": "highestRange"
            }]
            """;
        var catalog = new MobaActorBrainCatalog();

        MobaActorBrainCatalogJsonLoader.LoadJson(json, catalog);

        Assert.True(catalog.TryGet(17, out var definition));
        Assert.Equal(MobaBrainSkillSelectionPolicy.HighestRange, definition.SkillSelectionPolicy);
    }

    private static MobaConfigDatabase CreateConfigDatabase()
    {
        var configs = new MobaConfigDatabase();
        var result = configs.ReloadFromDtoArrays(
            new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[]
                {
                    new SkillDTO { Id = 1001, Name = "short_range", Range = 5, Tags = Array.Empty<int>() },
                    new SkillDTO { Id = 1002, Name = "long_range", Range = 12, Tags = Array.Empty<int>() },
                },
            },
            strict: false);

        Assert.True(result.Succeeded, result.Error);
        return configs;
    }

    private static string CreateSelectReadySkillTreeJson()
    {
        return """
            {
              "formatVersion": 1,
              "treeId": "test_select_ready_skill",
              "rootNodeId": "node",
              "nodes": [
                {
                  "id": "node",
                  "type": "moba.selectReadySkill",
                  "name": "Select",
                  "comment": "",
                  "properties": {},
                  "childIds": []
                }
              ],
              "blackboard": { "keys": [] }
            }
            """;
    }
}
