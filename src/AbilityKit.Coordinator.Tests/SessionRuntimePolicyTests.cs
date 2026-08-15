using AbilityKit.Coordinator.Core;
using Xunit;

namespace AbilityKit.Coordinator.Tests;

public sealed class SessionRuntimePolicyTests
{
    [Fact]
    public void LockstepHost_StillRequiresNetwork()
    {
        var config = SessionConfig.CreateHost(playerId: 1);

        var policy = config.ResolveRuntimePolicy();

        Assert.Equal(SyncMode.Lockstep, policy.EffectiveSyncMode);
        Assert.True(policy.RequiresNetwork);
    }

    [Fact]
    public void LocalSession_DoesNotRequireNetwork()
    {
        var config = SessionConfig.CreateLocal(playerId: 1);

        Assert.False(config.ResolveRuntimePolicy().RequiresNetwork);
    }
}
