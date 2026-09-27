using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class SkillCooldownPresentationStateTests
{
    [Fact]
    public void ServerDeadlineExtendsStaleRemainingDuration()
    {
        var snapshot = new SkillStateData(
            actorId: 1,
            slot: 1,
            skillId: 7101,
            cooldownTotalMs: 8000,
            cooldownRemainingMs: 2500,
            cooldownEndTimeMs: 16000,
            serverTimeMs: 10000,
            availability: SkillAvailabilityState.CoolingDown);

        var state = SkillCooldownPresentationState.FromSnapshot(snapshot, localReceiveTimeSeconds: 20f);
        var display = state.GetDisplayState(nowSeconds: 22f);

        Assert.True(state.BlocksInput(22f));
        Assert.True(display.ShowOverlay);
        Assert.True(display.ShowCountdown);
        Assert.Equal(4f, display.RemainingSeconds, precision: 3);
        Assert.Equal(0.5f, display.FillAmount, precision: 3);
    }

    [Fact]
    public void CooldownExpiresAgainstMonotonicLocalTime()
    {
        var snapshot = new SkillStateData(
            actorId: 1,
            slot: 1,
            skillId: 7101,
            cooldownTotalMs: 3000,
            cooldownRemainingMs: 1000,
            availability: SkillAvailabilityState.CoolingDown);

        var state = SkillCooldownPresentationState.FromSnapshot(snapshot, localReceiveTimeSeconds: 5f);

        Assert.False(state.BlocksInput(6f));
        Assert.False(state.GetDisplayState(6f).ShowOverlay);
    }

    [Fact]
    public void DisabledStateBlocksInputWithoutCountdown()
    {
        var snapshot = new SkillStateData(
            actorId: 1,
            slot: 1,
            skillId: 7101,
            availability: SkillAvailabilityState.Disabled,
            disableReason: 9);

        var state = SkillCooldownPresentationState.FromSnapshot(snapshot, localReceiveTimeSeconds: 0f);
        var display = state.GetDisplayState(nowSeconds: 100f);

        Assert.True(state.BlocksInput(100f));
        Assert.True(display.IsDisabled);
        Assert.True(display.ShowOverlay);
        Assert.False(display.ShowCountdown);
        Assert.Equal(1f, display.FillAmount);
    }
}
