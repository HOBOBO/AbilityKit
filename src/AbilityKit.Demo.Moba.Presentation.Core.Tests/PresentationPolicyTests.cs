using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PresentationPolicyTests
{
    [Fact]
    public void EventSourceModeDefaultsAndHybridSelectionArePlatformNeutral()
    {
        var policy = new ViewEventSourceModePolicy();
        Assert.Equal(BattleViewEventSourceMode.SnapshotOnly, policy.Resolve(null));
        Assert.False(policy.ShouldUseTriggerAdapter(BattleViewEventSourceMode.SnapshotOnly));
        Assert.True(policy.ShouldUseTriggerAdapter(BattleViewEventSourceMode.Hybrid));
        Assert.True(policy.ShouldUseSnapshotAdapter(BattleViewEventSourceMode.Hybrid));
    }

    [Fact]
    public void LifetimeUsesMillisecondsAndRejectsUnsetExpiry()
    {
        var expiry = PresentationLifetimePolicy.ExpireAt(3f, 1500);
        Assert.Equal(4.5f, expiry);
        Assert.False(PresentationLifetimePolicy.IsExpired(4.49f, expiry));
        Assert.True(PresentationLifetimePolicy.IsExpired(4.5f, expiry));
        Assert.Equal(0f, PresentationLifetimePolicy.ExpireAt(3f, 0));
        Assert.False(PresentationLifetimePolicy.IsExpired(10f, 0f));
    }
}
