using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class BattleProjectionSelectionPolicyTests
{
    [Fact]
    public void GrantsOnlyExplicitWorldRoles()
    {
        var policy = new BattleProjectionSelectionPolicy(
            BattleProjectionPermission.Prediction | BattleProjectionPermission.Replay);

        Assert.True(policy.Allows(BattleProjectionViewRole.Prediction));
        Assert.True(policy.Allows(BattleProjectionViewRole.Replay));
        Assert.False(policy.Allows(BattleProjectionViewRole.Confirmed));
        Assert.False(policy.Allows(BattleProjectionViewRole.Spectator));
        Assert.False(policy.Allows(BattleProjectionViewRole.Auxiliary));
    }
}
