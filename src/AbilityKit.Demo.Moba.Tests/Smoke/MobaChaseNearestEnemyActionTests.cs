using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using BTCore.Runtime;
using BTCore.Runtime.Blackboards;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaBTreeCombatNodeTests
{
    [Fact]
    public void Cast_plan_transfers_skill_id_target_position_and_direction_through_blackboard()
    {
        var blackboard = CreateInitializedBlackboard();
        blackboard.SetValue(MobaBTreeKeys.OwnerX, 1f);
        blackboard.SetValue(MobaBTreeKeys.OwnerY, 0f);
        blackboard.SetValue(MobaBTreeKeys.OwnerZ, 1f);
        blackboard.SetValue(MobaBTreeKeys.TargetValid, true);
        blackboard.SetValue(MobaBTreeKeys.TargetId, 42);
        blackboard.SetValue(MobaBTreeKeys.TargetX, 4f);
        blackboard.SetValue(MobaBTreeKeys.TargetY, 0f);
        blackboard.SetValue(MobaBTreeKeys.TargetZ, 5f);
        blackboard.SetValue(MobaBTreeKeys.SkillValid, true);
        blackboard.SetValue(MobaBTreeKeys.SkillId, 10020101);
        blackboard.SetValue(MobaBTreeKeys.SkillSlot, 2);

        var resolveAim = Bind(new MobaResolveTargetAimAction(), blackboard);
        var requestCast = Bind(new MobaCastSelectedSkillAction(), blackboard);
        var arbitrate = Bind(new MobaArbitrateCombatIntentAction(), blackboard);

        Assert.Equal(NodeState.Success, resolveAim.Update());
        Assert.Equal(NodeState.Success, requestCast.Update());
        Assert.Equal(NodeState.Success, arbitrate.Update());
        Assert.Equal((int)MobaBTreeIntentKind.Cast,
            blackboard.GetValue<int>(MobaBTreeKeys.OutputKind));
        Assert.True(blackboard.GetValue<bool>(MobaBTreeKeys.HasCast));
        Assert.Equal(10020101, blackboard.GetValue<int>(MobaBTreeKeys.CastSkillId));
        Assert.Equal(2, blackboard.GetValue<int>(MobaBTreeKeys.CastSkillSlot));
        Assert.Equal(42, blackboard.GetValue<int>(MobaBTreeKeys.CastTargetActorId));
        Assert.Equal(4f, blackboard.GetValue<float>(MobaBTreeKeys.CastAimX));
        Assert.Equal(5f, blackboard.GetValue<float>(MobaBTreeKeys.CastAimZ));
        Assert.Equal(0.6f, blackboard.GetValue<float>(MobaBTreeKeys.CastDirectionX), 3);
        Assert.Equal(0.8f, blackboard.GetValue<float>(MobaBTreeKeys.CastDirectionZ), 3);
    }

    [Fact]
    public void Approach_condition_and_move_request_are_independently_composable()
    {
        var blackboard = CreateInitializedBlackboard();
        blackboard.SetValue(MobaBTreeKeys.TargetValid, true);
        blackboard.SetValue(MobaBTreeKeys.TargetDistance, 12f);
        blackboard.SetValue(MobaBTreeKeys.TargetX, 8f);
        blackboard.SetValue(MobaBTreeKeys.TargetY, 2f);
        blackboard.SetValue(MobaBTreeKeys.TargetZ, 9f);
        blackboard.SetValue(MobaBTreeKeys.SkillApproachRange, 8f);

        var condition = Bind(new MobaShouldApproachEnemyCondition(), blackboard);
        var requestMove = Bind(new MobaMoveToEnemyAction(), blackboard);
        var arbitrate = Bind(new MobaArbitrateCombatIntentAction(), blackboard);

        Assert.Equal(NodeState.Success, condition.Update());
        Assert.Equal(NodeState.Success, requestMove.Update());
        Assert.True(blackboard.GetValue<bool>(MobaBTreeKeys.MoveRequestValid));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.HasMove));
        Assert.Equal(NodeState.Success, arbitrate.Update());
        Assert.True(blackboard.GetValue<bool>(MobaBTreeKeys.HasMove));
        Assert.Equal(8f, blackboard.GetValue<float>(MobaBTreeKeys.MoveX));
        Assert.Equal(2f, blackboard.GetValue<float>(MobaBTreeKeys.MoveY));
        Assert.Equal(9f, blackboard.GetValue<float>(MobaBTreeKeys.MoveZ));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.HasCast));
    }

    [Fact]
    public void Clearing_invalid_facts_removes_stale_payload_values()
    {
        var blackboard = CreateInitializedBlackboard();
        blackboard.SetValue(MobaBTreeKeys.TargetValid, true);
        blackboard.SetValue(MobaBTreeKeys.TargetId, 99);
        blackboard.SetValue(MobaBTreeKeys.TargetX, 12f);
        blackboard.SetValue(MobaBTreeKeys.SkillValid, true);
        blackboard.SetValue(MobaBTreeKeys.SkillId, 1234);
        blackboard.SetValue(MobaBTreeKeys.SkillSlot, 3);
        blackboard.SetValue(MobaBTreeKeys.CastRequestValid, true);
        blackboard.SetValue(MobaBTreeKeys.MoveRequestValid, true);
        blackboard.SetValue(MobaBTreeKeys.HasCast, true);
        blackboard.SetValue(MobaBTreeKeys.HasMove, true);

        MobaBTreeBlackboard.ClearTarget(blackboard);
        MobaBTreeBlackboard.ClearSkill(blackboard, 1.5f);
        MobaBTreeBlackboard.ClearTransientIntents(blackboard);

        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.TargetValid));
        Assert.Equal(0, blackboard.GetValue<int>(MobaBTreeKeys.TargetId));
        Assert.Equal(0f, blackboard.GetValue<float>(MobaBTreeKeys.TargetX));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.SkillValid));
        Assert.Equal(0, blackboard.GetValue<int>(MobaBTreeKeys.SkillId));
        Assert.Equal(0, blackboard.GetValue<int>(MobaBTreeKeys.SkillSlot));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.CastRequestValid));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.MoveRequestValid));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.HasCast));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.HasMove));
        Assert.Equal(0, blackboard.GetValue<int>(MobaBTreeKeys.CastRequestSkillId));
        Assert.Equal(0, blackboard.GetValue<int>(MobaBTreeKeys.CastSkillId));
        Assert.Equal(0f, blackboard.GetValue<float>(MobaBTreeKeys.MoveRequestX));
        Assert.Equal(0f, blackboard.GetValue<float>(MobaBTreeKeys.MoveX));
    }

    [Fact]
    public void Arbiter_chooses_highest_priority_intent_and_cast_wins_ties()
    {
        var blackboard = CreateInitializedBlackboard();
        blackboard.SetValue(MobaBTreeKeys.CastRequestValid, true);
        blackboard.SetValue(MobaBTreeKeys.CastRequestPriority, 50);
        blackboard.SetValue(MobaBTreeKeys.CastRequestSkillId, 777);
        blackboard.SetValue(MobaBTreeKeys.CastRequestSkillSlot, 1);
        blackboard.SetValue(MobaBTreeKeys.MoveRequestValid, true);
        blackboard.SetValue(MobaBTreeKeys.MoveRequestPriority, 50);
        blackboard.SetValue(MobaBTreeKeys.MoveRequestX, 10f);

        var arbitrate = Bind(new MobaArbitrateCombatIntentAction(), blackboard);

        Assert.Equal(NodeState.Success, arbitrate.Update());
        Assert.True(blackboard.GetValue<bool>(MobaBTreeKeys.HasCast));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.HasMove));
        Assert.Equal(777, blackboard.GetValue<int>(MobaBTreeKeys.CastSkillId));

        blackboard.SetValue(MobaBTreeKeys.MoveRequestPriority, 51);
        Assert.Equal(NodeState.Success, arbitrate.Update());
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.HasCast));
        Assert.True(blackboard.GetValue<bool>(MobaBTreeKeys.HasMove));
    }

    [Fact]
    public void Hold_request_does_not_erase_other_intents_and_can_win_by_priority()
    {
        var blackboard = CreateInitializedBlackboard();
        blackboard.SetValue(MobaBTreeKeys.MoveRequestValid, true);
        blackboard.SetValue(MobaBTreeKeys.MoveRequestPriority, 50);
        blackboard.SetValue(MobaBTreeKeys.MoveRequestX, 10f);

        var hold = Bind(new MobaHoldPositionAction(), blackboard);
        hold.Properties["priority"] = "60";
        var arbitrate = Bind(new MobaArbitrateCombatIntentAction(), blackboard);

        Assert.Equal(NodeState.Success, hold.Update());
        Assert.True(blackboard.GetValue<bool>(MobaBTreeKeys.MoveRequestValid));
        Assert.True(blackboard.GetValue<bool>(MobaBTreeKeys.HoldRequestValid));
        Assert.Equal(NodeState.Success, arbitrate.Update());
        Assert.Equal((int)MobaBTreeIntentKind.Hold,
            blackboard.GetValue<int>(MobaBTreeKeys.OutputKind));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.HasMove));
        Assert.False(blackboard.GetValue<bool>(MobaBTreeKeys.HasCast));
    }

    private static Blackboard CreateInitializedBlackboard()
    {
        var blackboard = new Blackboard();
        MobaBTreeBlackboard.Initialize(blackboard);
        return blackboard;
    }

    private static T Bind<T>(T node, Blackboard blackboard) where T : BTNode
    {
        node.SetBlackboard(blackboard);
        return node;
    }
}
