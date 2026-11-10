using System;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PresentationLifecycleTests
{
    [Fact]
    public void ReplacementPreflightHonorsHistoryWithoutDestroyingExistingSlot()
    {
        var ledger = new PresentationEffectPolicyLedger<string>();
        var policy = new PresentationEffectPolicy(unique: true, minIntervalMs: 1000, maxActive: 1);
        Assert.True(ledger.TryAcquire("owner", policy, 0));
        Assert.False(ledger.CanAcquire("owner", policy, 100, replacingActive: 1));
        Assert.Equal(1, ledger.GetActive("owner"));
        Assert.True(ledger.CanAcquire("owner", policy, 1000, replacingActive: 1));
        Assert.False(ledger.CanAcquire("owner", policy, 1000));
        ledger.Release("owner");
        Assert.True(ledger.TryAcquire("owner", policy, 1000));
    }

    [Fact]
    public void DefaultStopIsImmediateAndTerminal()
    {
        var state = new PresentationLifetimeState();
        Assert.True(state.RequestStop(default, canDrain: true));
        Assert.True(state.Tick(default, 0, false, false));
    }

    [Fact]
    public void DrainWaitsForResourceDespiteElapsedDuration()
    {
        var policy = new PresentationLifecyclePolicy(stop: PresentationStopPolicy.Drain);
        var state = new PresentationLifetimeState();
        Assert.False(state.RequestStop(policy, true));
        Assert.False(state.Tick(policy, 0.2f, true, false));
        Assert.True(state.Tick(policy, 0.1f, false, true));
    }

    [Fact]
    public void RepeatedStopDoesNotExtendGraceOrChangeDrainCapability()
    {
        var policy = new PresentationLifecyclePolicy(stop: PresentationStopPolicy.Drain, stopGraceMs: 1000);
        var state = new PresentationLifetimeState();
        Assert.False(state.RequestStop(policy, true));
        Assert.False(state.Tick(policy, 0.75f, false, false));
        Assert.False(state.RequestStop(policy, false));
        Assert.True(state.Tick(policy, 0.25f, false, false));
    }

    [Fact]
    public void DefaultGraceIsBoundedAndForceBypassesDrain()
    {
        var policy = new PresentationLifecyclePolicy(stop: PresentationStopPolicy.Drain);
        var state = new PresentationLifetimeState();
        state.RequestStop(policy, true);
        Assert.False(state.Tick(policy, 2.9f, false, false));
        Assert.True(state.Tick(policy, 0.2f, false, false));
        var forced = new PresentationLifetimeState();
        forced.RequestStop(policy, true);
        Assert.True(forced.RequestStop(policy, true, force: true));
    }

    [Fact]
    public void UnsupportedDrainCompletesImmediately()
    {
        var policy = new PresentationLifecyclePolicy(stop: PresentationStopPolicy.Drain);
        Assert.True(new PresentationLifetimeState().RequestStop(policy, false));
    }

    [Theory]
    [InlineData(PresentationCompletionPolicy.Duration, false, true, false)]
    [InlineData(PresentationCompletionPolicy.Duration, true, false, true)]
    [InlineData(PresentationCompletionPolicy.ResourceOrDuration, false, true, true)]
    [InlineData(PresentationCompletionPolicy.ResourceOrDuration, true, false, true)]
    [InlineData(PresentationCompletionPolicy.ResourceOrDuration, false, false, false)]
    [InlineData(PresentationCompletionPolicy.Manual, true, true, false)]
    public void CompletionHonorsPolicy(PresentationCompletionPolicy completion, bool duration, bool resource, bool expected)
    {
        var policy = new PresentationLifecyclePolicy(completion: completion);
        Assert.Equal(expected, new PresentationLifetimeState().Tick(policy, 0.1f, duration, resource));
    }

    [Theory]
    [InlineData(PresentationTargetLossPolicy.BackendDefault, false, false)]
    [InlineData(PresentationTargetLossPolicy.Stop, true, false)]
    [InlineData(PresentationTargetLossPolicy.Detach, false, true)]
    [InlineData(PresentationTargetLossPolicy.Rebind, false, false)]
    public void TargetLossSeparatesStopDetachAndRebind(PresentationTargetLossPolicy loss, bool stop, bool detach)
    {
        var policy = new PresentationLifecyclePolicy(targetLoss: loss);
        var state = new PresentationLifetimeState();
        Assert.Equal(stop, state.OnTargetMissing(policy));
        Assert.Equal(detach, state.IsDetached);
        Assert.False(state.IsFinished);
    }

    [Fact]
    public void ManualCompletionStillAllowsExplicitStop()
    {
        var policy = new PresentationLifecyclePolicy(completion: PresentationCompletionPolicy.Manual);
        var state = new PresentationLifetimeState();
        Assert.False(state.Tick(policy, 1000f, true, true));
        Assert.True(state.RequestStop(policy, true));
    }

    [Fact]
    public void InvalidPoliciesFallBackToLegacyDefaults()
    {
        var policy = new PresentationLifecyclePolicy((PresentationStackPolicy)99, (PresentationStopPolicy)99,
            (PresentationTargetLossPolicy)99, (PresentationCompletionPolicy)99, -1);
        Assert.Equal(PresentationStackPolicy.Independent, policy.Stack);
        Assert.Equal(PresentationStopPolicy.Immediate, policy.Stop);
        Assert.Equal(PresentationTargetLossPolicy.BackendDefault, policy.TargetLoss);
        Assert.Equal(PresentationCompletionPolicy.Duration, policy.Completion);
        Assert.Equal(3000, policy.EffectiveStopGraceMs);
    }

    [Fact]
    public void InvalidClockDeltaIsRejected()
    {
        var state = new PresentationLifetimeState();
        foreach (var delta in new[] { -1f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(default, delta, false, false));
    }
}
