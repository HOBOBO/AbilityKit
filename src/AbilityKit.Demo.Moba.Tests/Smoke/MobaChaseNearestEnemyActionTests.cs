using System.Collections.Generic;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// MOBA 领域节点的黑板协议单测：通过 <see cref="TreeRuntime"/> 驱动节点（建整树执行，等价于逐节点驱动）。
/// </summary>
public sealed class MobaBTreeCombatNodeTests
{
    [Fact]
    public void Cast_plan_transfers_skill_id_target_position_and_direction_through_blackboard()
    {
        var runtime = RunSequence(bb =>
        {
            bb.SetFixed64(MobaBTreeKeys.OwnerX, Fixed64.FromInt32(1));
            bb.SetFixed64(MobaBTreeKeys.OwnerY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.OwnerZ, Fixed64.FromInt32(1));
            bb.SetBool(MobaBTreeKeys.TargetValid, true);
            bb.SetInt64(MobaBTreeKeys.TargetId, 42);
            bb.SetFixed64(MobaBTreeKeys.TargetX, Fixed64.FromInt32(4));
            bb.SetFixed64(MobaBTreeKeys.TargetY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.TargetZ, Fixed64.FromInt32(5));
            bb.SetBool(MobaBTreeKeys.SkillValid, true);
            bb.SetInt64(MobaBTreeKeys.SkillId, 10020101);
            bb.SetInt64(MobaBTreeKeys.SkillSlot, 2);
        }, MobaBTreeNodeTypes.ResolveTargetAim, MobaBTreeNodeTypes.CastSelectedSkill, MobaBTreeNodeTypes.ArbitrateCombatIntent);

        var bb = runtime.Blackboard;
        Assert.Equal(NodeState.Success, runtime.RootNodeState);
        Assert.Equal((long)MobaBTreeIntentKind.Cast, bb.GetInt64(MobaBTreeKeys.OutputKind));
        Assert.True(bb.GetBool(MobaBTreeKeys.HasCast));
        Assert.Equal(10020101, bb.GetInt64(MobaBTreeKeys.CastSkillId));
        Assert.Equal(2, bb.GetInt64(MobaBTreeKeys.CastSkillSlot));
        Assert.Equal(42, bb.GetInt64(MobaBTreeKeys.CastTargetActorId));
        Assert.Equal(4f, bb.GetFixed64(MobaBTreeKeys.CastAimX).ToSingle());
        Assert.Equal(5f, bb.GetFixed64(MobaBTreeKeys.CastAimZ).ToSingle());
        Assert.Equal(0.6f, bb.GetFixed64(MobaBTreeKeys.CastDirectionX).ToSingle(), 3);
        Assert.Equal(0.8f, bb.GetFixed64(MobaBTreeKeys.CastDirectionZ).ToSingle(), 3);
    }

    [Fact]
    public void Approach_condition_and_move_request_are_independently_composable()
    {
        // 条件 + 移动请求：移动请求已建立，但 out.* 尚未由仲裁节点发布。
        var request = RunSequence(SetupApproach, MobaBTreeNodeTypes.ShouldApproachEnemy, MobaBTreeNodeTypes.MoveToEnemy);
        Assert.Equal(NodeState.Success, request.RootNodeState);
        Assert.True(request.Blackboard.GetBool(MobaBTreeKeys.MoveRequestValid));
        Assert.False(request.Blackboard.GetBool(MobaBTreeKeys.HasMove));

        // 追加仲裁节点后，移动意图被发布。
        var published = RunSequence(SetupApproach, MobaBTreeNodeTypes.ShouldApproachEnemy, MobaBTreeNodeTypes.MoveToEnemy, MobaBTreeNodeTypes.ArbitrateCombatIntent);
        var bb = published.Blackboard;
        Assert.True(bb.GetBool(MobaBTreeKeys.HasMove));
        Assert.Equal(8f, bb.GetFixed64(MobaBTreeKeys.MoveX).ToSingle());
        Assert.Equal(2f, bb.GetFixed64(MobaBTreeKeys.MoveY).ToSingle());
        Assert.Equal(9f, bb.GetFixed64(MobaBTreeKeys.MoveZ).ToSingle());
        Assert.False(bb.GetBool(MobaBTreeKeys.HasCast));
    }

    [Fact]
    public void Clearing_invalid_facts_removes_stale_payload_values()
    {
        var definition = new TreeDefinition();
        MobaBTreeBlackboard.EnsureStandardSchema(definition);
        var blackboard = Blackboard.Create(definition.Blackboard);

        blackboard.SetBool(MobaBTreeKeys.TargetValid, true);
        blackboard.SetInt64(MobaBTreeKeys.TargetId, 99);
        blackboard.SetFixed64(MobaBTreeKeys.TargetX, Fixed64.FromInt32(12));
        blackboard.SetBool(MobaBTreeKeys.SkillValid, true);
        blackboard.SetInt64(MobaBTreeKeys.SkillId, 1234);
        blackboard.SetInt64(MobaBTreeKeys.SkillSlot, 3);
        blackboard.SetBool(MobaBTreeKeys.CastRequestValid, true);
        blackboard.SetBool(MobaBTreeKeys.MoveRequestValid, true);
        blackboard.SetBool(MobaBTreeKeys.HasCast, true);
        blackboard.SetBool(MobaBTreeKeys.HasMove, true);

        MobaBTreeBlackboard.ClearTarget(blackboard);
        MobaBTreeBlackboard.ClearSkill(blackboard, Fixed64.FromSingle(1.5f));
        MobaBTreeBlackboard.ClearTransientIntents(blackboard);

        Assert.False(blackboard.GetBool(MobaBTreeKeys.TargetValid));
        Assert.Equal(0, blackboard.GetInt64(MobaBTreeKeys.TargetId));
        Assert.Equal(0f, blackboard.GetFixed64(MobaBTreeKeys.TargetX).ToSingle());
        Assert.False(blackboard.GetBool(MobaBTreeKeys.SkillValid));
        Assert.Equal(0, blackboard.GetInt64(MobaBTreeKeys.SkillId));
        Assert.Equal(0, blackboard.GetInt64(MobaBTreeKeys.SkillSlot));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.CastRequestValid));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.MoveRequestValid));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.HasCast));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.HasMove));
        Assert.Equal(0, blackboard.GetInt64(MobaBTreeKeys.CastRequestSkillId));
        Assert.Equal(0, blackboard.GetInt64(MobaBTreeKeys.CastSkillId));
        Assert.Equal(0f, blackboard.GetFixed64(MobaBTreeKeys.MoveRequestX).ToSingle());
        Assert.Equal(0f, blackboard.GetFixed64(MobaBTreeKeys.MoveX).ToSingle());
    }

    [Fact]
    public void Arbiter_chooses_highest_priority_intent_and_cast_wins_ties()
    {
        var runtime = RunSequence(bb =>
        {
            bb.SetBool(MobaBTreeKeys.CastRequestValid, true);
            bb.SetInt64(MobaBTreeKeys.CastRequestPriority, 50);
            bb.SetInt64(MobaBTreeKeys.CastRequestSkillId, 777);
            bb.SetInt64(MobaBTreeKeys.CastRequestSkillSlot, 1);
            bb.SetBool(MobaBTreeKeys.MoveRequestValid, true);
            bb.SetInt64(MobaBTreeKeys.MoveRequestPriority, 50);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestX, Fixed64.FromInt32(10));
        }, MobaBTreeNodeTypes.ArbitrateCombatIntent);

        var bb = runtime.Blackboard;
        Assert.True(bb.GetBool(MobaBTreeKeys.HasCast));
        Assert.False(bb.GetBool(MobaBTreeKeys.HasMove));
        Assert.Equal(777, bb.GetInt64(MobaBTreeKeys.CastSkillId));

        bb.SetInt64(MobaBTreeKeys.MoveRequestPriority, 51);
        runtime.Restart();
        runtime.Update(2, Fixed64.FromInt32(2));

        Assert.False(bb.GetBool(MobaBTreeKeys.HasCast));
        Assert.True(bb.GetBool(MobaBTreeKeys.HasMove));
    }

    [Fact]
    public void Hold_request_does_not_erase_other_intents_and_can_win_by_priority()
    {
        var definition = NewDefinition();
        definition.Nodes.Add(Leaf("root", BuiltInNodeTypes.Sequence));
        var hold = Leaf("n0", MobaBTreeNodeTypes.HoldPosition);
        hold.Properties.Set(MobaHoldPositionNode.PriorityProperty, PropertyValue.Of(60L));
        var arbitrate = Leaf("n1", MobaBTreeNodeTypes.ArbitrateCombatIntent);
        definition.Nodes.Add(hold);
        definition.Nodes.Add(arbitrate);
        definition.Nodes[0].ChildIds = new List<string> { "n0", "n1" };

        var runtime = Run(definition, bb =>
        {
            bb.SetBool(MobaBTreeKeys.MoveRequestValid, true);
            bb.SetInt64(MobaBTreeKeys.MoveRequestPriority, 50);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestX, Fixed64.FromInt32(10));
        });

        var bb = runtime.Blackboard;
        Assert.True(bb.GetBool(MobaBTreeKeys.MoveRequestValid));
        Assert.True(bb.GetBool(MobaBTreeKeys.HoldRequestValid));
        Assert.Equal((long)MobaBTreeIntentKind.Hold, bb.GetInt64(MobaBTreeKeys.OutputKind));
        Assert.False(bb.GetBool(MobaBTreeKeys.HasMove));
        Assert.False(bb.GetBool(MobaBTreeKeys.HasCast));
    }

    private static void SetupApproach(Blackboard bb)
    {
        bb.SetBool(MobaBTreeKeys.TargetValid, true);
        bb.SetFixed64(MobaBTreeKeys.TargetDistance, Fixed64.FromInt32(12));
        bb.SetFixed64(MobaBTreeKeys.TargetX, Fixed64.FromInt32(8));
        bb.SetFixed64(MobaBTreeKeys.TargetY, Fixed64.FromInt32(2));
        bb.SetFixed64(MobaBTreeKeys.TargetZ, Fixed64.FromInt32(9));
        bb.SetFixed64(MobaBTreeKeys.SkillApproachRange, Fixed64.FromInt32(8));
    }

    private static TreeRuntime RunSequence(System.Action<Blackboard>? setup, params string[] nodeTypes)
    {
        var definition = NewDefinition();
        if (nodeTypes.Length == 1)
        {
            definition.Nodes.Add(Leaf("root", nodeTypes[0]));
        }
        else
        {
            definition.Nodes.Add(Leaf("root", BuiltInNodeTypes.Sequence));
            var childIds = new List<string>(nodeTypes.Length);
            for (var i = 0; i < nodeTypes.Length; i++)
            {
                var id = "n" + i;
                definition.Nodes.Add(Leaf(id, nodeTypes[i]));
                childIds.Add(id);
            }
            definition.Nodes[0].ChildIds = childIds;
        }
        return Run(definition, setup);
    }

    private static TreeDefinition NewDefinition() => new()
    {
        TreeId = "test",
        RootNodeId = "root",
        FormatVersion = TreeDefinition.CurrentFormatVersion,
    };

    private static NodeDefinition Leaf(string id, string typeId) => new() { Id = id, Type = typeId };

    private static TreeRuntime Run(TreeDefinition definition, System.Action<Blackboard>? setup)
    {
        MobaBTreeBlackboard.EnsureStandardSchema(definition);
        var runtime = TreeRuntime.Create(definition, MobaBTreeCatalog.Registry);
        setup?.Invoke(runtime.Blackboard);
        runtime.Enable(0, Fixed64.Zero);
        runtime.Update(1, Fixed64.One);
        return runtime;
    }
}
