namespace AbilityKit.Orleans.Grains.Battle;

internal readonly record struct BattleTickDeadlineSchedule(
    long NextDeadlineTimestamp,
    long DelayTimestampTicks,
    long SkippedDeadlineCount);

/// <summary>
/// 推进战斗时钟绝对截止点，避免无界突发追赶。
/// </summary>
internal static class BattleTickDeadlineScheduler
{
    public static BattleTickDeadlineSchedule ScheduleAfterTick(
        long currentDeadlineTimestamp,
        long completedAtTimestamp,
        long intervalTimestampTicks)
    {
        if (intervalTimestampTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalTimestampTicks));
        }

        var nextDeadline = currentDeadlineTimestamp + intervalTimestampTicks;
        long skippedDeadlines = 0;
        if (nextDeadline < completedAtTimestamp)
        {
            skippedDeadlines = ((completedAtTimestamp - nextDeadline - 1) / intervalTimestampTicks) + 1;
            nextDeadline += skippedDeadlines * intervalTimestampTicks;
        }

        return new BattleTickDeadlineSchedule(
            nextDeadline,
            Math.Max(0, nextDeadline - completedAtTimestamp),
            skippedDeadlines);
    }
}
