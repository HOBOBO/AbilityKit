using System.Threading;
using AbilityKit.Timer;
using Xunit;

namespace AbilityKit.Timer.Tests;

public sealed class SystemTimerTests
{
    [Fact]
    public void Default_timer_reports_zero_elapsed()
    {
        var timer = default(SystemTimer);

        Assert.Equal(0f, timer.Elapsed, 6);
    }

    [Fact]
    public void Created_started_timer_elapses_monotonically()
    {
        var timer = TimerUtility.CreateStarted();

        float first = timer.Elapsed;
        Thread.SpinWait(2_000_000);
        float second = timer.Elapsed;

        Assert.True(first >= 0f);
        Assert.True(second >= first, $"elapsed regressed: {first} -> {second}");
    }

    [Fact]
    public void Reset_restarts_elapsed_time()
    {
        var timer = TimerUtility.CreateStarted();
        Thread.SpinWait(2_000_000);
        float before = timer.Elapsed;
        Assert.True(before > 0f);

        timer.Reset();
        float after = timer.Elapsed;

        Assert.True(after < before, $"reset did not restart: {before} -> {after}");
    }

    [Fact]
    public void Comparison_operators_use_elapsed_time()
    {
        var timer = TimerUtility.CreateStarted();
        Thread.SpinWait(2_000_000);
        var later = TimerUtility.CreateStarted();

        Assert.True(timer >= 0f);
        Assert.True(timer > -1f);
        Assert.True(timer <= 60f);
        // 比较的是已流逝时长：先启动并空转过的 timer 走得更久。
        Assert.True(timer > later);
        Assert.True(later < timer);
    }
}
