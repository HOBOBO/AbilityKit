using AbilityKit.BehaviorTree;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// MOBA 领域节点的黑板协议单测：直接以 BtExecutionContext 驱动节点（不建整树）。
/// </summary>
public sealed class MobaBTreeCombatNodeTests
{
    [Fact]
    public void Cast_plan_transfers_skill_id_target_position_and_direction_through_blackboard()
    {
        var blackboard = CreateBlackboard();
        var ctx = CreateContext(blackboard);
        blackboard.SetFixed64(MobaBTreeKeys.OwnerX, Fixed64.FromInt32(1));
        blackboard.SetFixed64(MobaBTreeKeys.OwnerY, Fixed64.Zero);
        blackboard.SetFixed64(MobaBTreeKeys.OwnerZ, Fixed64.FromInt32(1));
        blackboard.SetBool(MobaBTreeKeys.TargetValid, true);
        blackboard.SetInt64(MobaBTreeKeys.TargetId, 42);
        blackboard.SetFixed64(MobaBTreeKeys.TargetX, Fixed64.FromInt32(4));
        blackboard.SetFixed64(MobaBTreeKeys.TargetY, Fixed64.Zero);
        blackboard.SetFixed64(MobaBTreeKeys.TargetZ, Fixed64.FromInt32(5));
        blackboard.SetBool(MobaBTreeKeys.SkillValid, true);
        blackboard.SetInt64(MobaBTreeKeys.SkillId, 10020101);
        blackboard.SetInt64(MobaBTreeKeys.SkillSlot, 2);

        var resolveAim = Init(new MobaResolveTargetAimNode(), ctx);
        var requestCast = Init(new MobaCastSelectedSkillNode(), ctx);
        var arbitrate = Init(new MobaArbitrateCombatIntentNode(), ctx);

        Assert.Equal(BtNodeState.Success, resolveAim.OnTick(ctx));
        Assert.Equal(BtNodeState.Success, requestCast.OnTick(ctx));
        Assert.Equal(BtNodeState.Success, arbitrate.OnTick(ctx));
        Assert.Equal((long)MobaBTreeIntentKind.Cast, blackboard.GetInt64(MobaBTreeKeys.OutputKind));
        Assert.True(blackboard.GetBool(MobaBTreeKeys.HasCast));
        Assert.Equal(10020101, blackboard.GetInt64(MobaBTreeKeys.CastSkillId));
        Assert.Equal(2, blackboard.GetInt64(MobaBTreeKeys.CastSkillSlot));
        Assert.Equal(42, blackboard.GetInt64(MobaBTreeKeys.CastTargetActorId));
        Assert.Equal(4f, blackboard.GetFixed64(MobaBTreeKeys.CastAimX).ToSingle());
        Assert.Equal(5f, blackboard.GetFixed64(MobaBTreeKeys.CastAimZ).ToSingle());
        Assert.Equal(0.6f, blackboard.GetFixed64(MobaBTreeKeys.CastDirectionX).ToSingle(), 3);
        Assert.Equal(0.8f, blackboard.GetFixed64(MobaBTreeKeys.CastDirectionZ).ToSingle(), 3);
    }

    [Fact]
    public void Approach_condition_and_move_request_are_independently_composable()
    {
        var blackboard = CreateBlackboard();
        var ctx = CreateContext(blackboard);
        blackboard.SetBool(MobaBTreeKeys.TargetValid, true);
        blackboard.SetFixed64(MobaBTreeKeys.TargetDistance, Fixed64.FromInt32(12));
        blackboard.SetFixed64(MobaBTreeKeys.TargetX, Fixed64.FromInt32(8));
        blackboard.SetFixed64(MobaBTreeKeys.TargetY, Fixed64.FromInt32(2));
        blackboard.SetFixed64(MobaBTreeKeys.TargetZ, Fixed64.FromInt32(9));
        blackboard.SetFixed64(MobaBTreeKeys.SkillApproachRange, Fixed64.FromInt32(8));

        var condition = Init(new MobaShouldApproachEnemyNode(), ctx);
        var requestMove = Init(new MobaMoveToEnemyNode(), ctx);
        var arbitrate = Init(new MobaArbitrateCombatIntentNode(), ctx);

        Assert.Equal(BtNodeState.Success, condition.OnTick(ctx));
        Assert.Equal(BtNodeState.Success, requestMove.OnTick(ctx));
        Assert.True(blackboard.GetBool(MobaBTreeKeys.MoveRequestValid));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.HasMove));
        Assert.Equal(BtNodeState.Success, arbitrate.OnTick(ctx));
        Assert.True(blackboard.GetBool(MobaBTreeKeys.HasMove));
        Assert.Equal(8f, blackboard.GetFixed64(MobaBTreeKeys.MoveX).ToSingle());
        Assert.Equal(2f, blackboard.GetFixed64(MobaBTreeKeys.MoveY).ToSingle());
        Assert.Equal(9f, blackboard.GetFixed64(MobaBTreeKeys.MoveZ).ToSingle());
        Assert.False(blackboard.GetBool(MobaBTreeKeys.HasCast));
    }

    [Fact]
    public void Clearing_invalid_facts_removes_stale_payload_values()
    {
        var blackboard = CreateBlackboard();
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
        var blackboard = CreateBlackboard();
        var ctx = CreateContext(blackboard);
        blackboard.SetBool(MobaBTreeKeys.CastRequestValid, true);
        blackboard.SetInt64(MobaBTreeKeys.CastRequestPriority, 50);
        blackboard.SetInt64(MobaBTreeKeys.CastRequestSkillId, 777);
        blackboard.SetInt64(MobaBTreeKeys.CastRequestSkillSlot, 1);
        blackboard.SetBool(MobaBTreeKeys.MoveRequestValid, true);
        blackboard.SetInt64(MobaBTreeKeys.MoveRequestPriority, 50);
        blackboard.SetFixed64(MobaBTreeKeys.MoveRequestX, Fixed64.FromInt32(10));

        var arbitrate = Init(new MobaArbitrateCombatIntentNode(), ctx);

        Assert.Equal(BtNodeState.Success, arbitrate.OnTick(ctx));
        Assert.True(blackboard.GetBool(MobaBTreeKeys.HasCast));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.HasMove));
        Assert.Equal(777, blackboard.GetInt64(MobaBTreeKeys.CastSkillId));

        blackboard.SetInt64(MobaBTreeKeys.MoveRequestPriority, 51);
        Assert.Equal(BtNodeState.Success, arbitrate.OnTick(ctx));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.HasCast));
        Assert.True(blackboard.GetBool(MobaBTreeKeys.HasMove));
    }

    [Fact]
    public void Hold_request_does_not_erase_other_intents_and_can_win_by_priority()
    {
        var blackboard = CreateBlackboard();
        var ctx = CreateContext(blackboard);
        blackboard.SetBool(MobaBTreeKeys.MoveRequestValid, true);
        blackboard.SetInt64(MobaBTreeKeys.MoveRequestPriority, 50);
        blackboard.SetFixed64(MobaBTreeKeys.MoveRequestX, Fixed64.FromInt32(10));

        var holdProps = new BtPropertyBag();
        holdProps.Set(MobaHoldPositionNode.PriorityProperty, BtPropertyValue.Of(60L));
        var hold = Init(new MobaHoldPositionNode(), ctx, holdProps);
        var arbitrate = Init(new MobaArbitrateCombatIntentNode(), ctx);

        Assert.Equal(BtNodeState.Success, hold.OnTick(ctx));
        Assert.True(blackboard.GetBool(MobaBTreeKeys.MoveRequestValid));
        Assert.True(blackboard.GetBool(MobaBTreeKeys.HoldRequestValid));
        Assert.Equal(BtNodeState.Success, arbitrate.OnTick(ctx));
        Assert.Equal((long)MobaBTreeIntentKind.Hold, blackboard.GetInt64(MobaBTreeKeys.OutputKind));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.HasMove));
        Assert.False(blackboard.GetBool(MobaBTreeKeys.HasCast));
    }

    private static BtBlackboard CreateBlackboard()
    {
        var definition = new BtTreeDefinition();
        MobaBTreeBlackboard.EnsureStandardSchema(definition);
        return BtBlackboard.Create(definition.Blackboard);
    }

    private static BtExecutionContext CreateContext(BtBlackboard blackboard)
        => new BtExecutionContext(blackboard, new BtServiceResolver());

    private static T Init<T>(T node, BtExecutionContext ctx, BtPropertyBag? properties = null) where T : BtNodeBase
    {
        properties ??= new BtPropertyBag();
        node.OnInit(new BtNodeInitContext
        {
            Tree = new BtTreeDefinition(),
            Definition = new BtNodeDefinition { Id = "test.node", Type = "test", Properties = properties },
            Properties = new BtPropertyReader(properties),
            ChildCount = 0,
            Registry = MobaBTreeCatalog.Registry,
            Random = new DeterministicRandom(1UL),
            Context = ctx,
        });
        return node;
    }
}
