using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class ReconnectAttemptSchedulerTests
{
    [Fact]
    public void Request_AndTick_UseSharedBackoffWithoutPostponingPendingAttempt()
    {
        var scheduler = new ReconnectAttemptScheduler(maxAttempts: 3);

        Assert.True(scheduler.Request());
        Assert.False(scheduler.TryTakeAttempt(0.75f, out _));
        Assert.True(scheduler.Request());
        Assert.Equal(0.25f, scheduler.RemainingDelaySeconds, 3);

        Assert.True(scheduler.TryTakeAttempt(0.25f, out var first));
        Assert.Equal(1, first);
        Assert.Equal(2f, scheduler.NextDelaySeconds);
    }

    [Fact]
    public void TryTakeAttempt_StopsAtConfiguredLimit()
    {
        var scheduler = new ReconnectAttemptScheduler(
            maxAttempts: 2,
            resolveDelay: _ => 0f);
        scheduler.Request();

        Assert.True(scheduler.TryTakeAttempt(0f, out var first));
        Assert.Equal(1, first);
        Assert.True(scheduler.IsPending);

        Assert.True(scheduler.TryTakeAttempt(0f, out var second));
        Assert.Equal(2, second);
        Assert.False(scheduler.IsPending);
        Assert.True(scheduler.IsExhausted);
        Assert.False(scheduler.TryTakeAttempt(100f, out _));
        Assert.False(scheduler.Request());
    }

    [Fact]
    public void Reset_AllowsACompletedScheduleToBeReused()
    {
        var scheduler = new ReconnectAttemptScheduler(
            maxAttempts: 1,
            resolveDelay: _ => 0f);
        scheduler.Request();
        scheduler.TryTakeAttempt(0f, out _);

        scheduler.Reset();

        Assert.False(scheduler.IsPending);
        Assert.False(scheduler.IsExhausted);
        Assert.Equal(0, scheduler.AttemptsStarted);
        Assert.True(scheduler.Request());
    }

    [Fact]
    public void NegativeDelta_DoesNotAdvanceTheSchedule()
    {
        var scheduler = new ReconnectAttemptScheduler();
        scheduler.Request();

        Assert.False(scheduler.TryTakeAttempt(-10f, out _));
        Assert.Equal(ReconnectBackoffPolicy.BaseDelaySeconds, scheduler.RemainingDelaySeconds);
    }
}
