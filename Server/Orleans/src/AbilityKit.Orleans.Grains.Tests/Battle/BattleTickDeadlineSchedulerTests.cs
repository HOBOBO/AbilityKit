using AbilityKit.Orleans.Grains.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class BattleTickDeadlineSchedulerTests
{
    [Fact]
    public void ScheduleAfterTick_SubtractsExecutionTimeFromNextDelay()
    {
        var schedule = BattleTickDeadlineScheduler.ScheduleAfterTick(
            currentDeadlineTimestamp: 100,
            completedAtTimestamp: 120,
            intervalTimestampTicks: 33);

        Assert.Equal(133, schedule.NextDeadlineTimestamp);
        Assert.Equal(13, schedule.DelayTimestampTicks);
        Assert.Equal(0, schedule.SkippedDeadlineCount);
    }

    [Fact]
    public void ScheduleAfterTick_UsesZeroDelayWhenCompletionMatchesDeadline()
    {
        var schedule = BattleTickDeadlineScheduler.ScheduleAfterTick(
            currentDeadlineTimestamp: 100,
            completedAtTimestamp: 133,
            intervalTimestampTicks: 33);

        Assert.Equal(133, schedule.NextDeadlineTimestamp);
        Assert.Equal(0, schedule.DelayTimestampTicks);
        Assert.Equal(0, schedule.SkippedDeadlineCount);
    }

    [Fact]
    public void ScheduleAfterTick_SkipsExpiredDeadlinesWithoutCatchUpBurst()
    {
        var schedule = BattleTickDeadlineScheduler.ScheduleAfterTick(
            currentDeadlineTimestamp: 100,
            completedAtTimestamp: 170,
            intervalTimestampTicks: 33);

        Assert.Equal(199, schedule.NextDeadlineTimestamp);
        Assert.Equal(29, schedule.DelayTimestampTicks);
        Assert.Equal(2, schedule.SkippedDeadlineCount);
    }

    [Fact]
    public void ScheduleAfterTick_RejectsNonPositiveInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleTickDeadlineScheduler.ScheduleAfterTick(100, 120, 0));
    }
}
