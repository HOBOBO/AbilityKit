using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using BTCore.Runtime;
using BTCore.Runtime.Blackboards;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaChaseNearestEnemyActionTests
{
    [Fact]
    public void Update_without_valid_enemy_clears_previous_move_output()
    {
        var blackboard = new Blackboard();
        blackboard.SetValue("enemy.valid", false);
        blackboard.SetValue("out.hasMove", true);

        var action = new MobaChaseNearestEnemyAction();
        action.SetBlackboard(blackboard);

        var state = action.Update();

        Assert.Equal(NodeState.Failure, state);
        Assert.False(blackboard.GetValue<bool>("out.hasMove"));
    }
}
