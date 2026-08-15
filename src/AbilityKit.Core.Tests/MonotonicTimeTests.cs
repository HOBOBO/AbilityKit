using AbilityKit.Core.Timing;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class MonotonicTimeTests
{
    [Fact]
    public void Stopwatch_clock_has_positive_frequency_and_nondecreasing_timestamps()
    {
        IMonotonicClock clock = StopwatchMonotonicClock.Instance;

        var first = clock.Timestamp;
        var second = clock.Timestamp;

        Assert.True(clock.Frequency > 0);
        Assert.True(second >= first);
        Assert.Same(StopwatchMonotonicClock.Instance, StopwatchMonotonicClock.Instance);
    }

    [Theory]
    [InlineData(0, 10_000_000, 0)]
    [InlineData(15_000_000, 10_000_000, 1_500)]
    [InlineData(15_009_999, 10_000_000, 1_500)]
    [InlineData(-15_009_999, 10_000_000, -1_500)]
    public void Timestamp_conversion_truncates_to_whole_milliseconds(
        long timestamp,
        long frequency,
        long expected)
    {
        Assert.Equal(expected, MonotonicTime.ToMilliseconds(timestamp, frequency));
    }

    [Fact]
    public void Duration_conversion_rounds_positive_fractional_ticks_up()
    {
        Assert.Equal(0, MonotonicTime.DurationToTimestampTicks(TimeSpan.Zero, 3));
        Assert.Equal(1, MonotonicTime.DurationToTimestampTicks(TimeSpan.FromTicks(1), 3));
        Assert.Equal(3, MonotonicTime.DurationToTimestampTicks(TimeSpan.FromSeconds(1), 3));
        Assert.Equal(4, MonotonicTime.DurationToTimestampTicks(TimeSpan.FromMilliseconds(1_001), 3));
    }

    [Fact]
    public void Duration_conversion_is_exact_and_saturates_on_overflow()
    {
        Assert.Equal(
            TimeSpan.MaxValue.Ticks,
            MonotonicTime.DurationToTimestampTicks(TimeSpan.MaxValue, TimeSpan.TicksPerSecond));
        Assert.Equal(
            long.MaxValue,
            MonotonicTime.DurationToTimestampTicks(TimeSpan.MaxValue, long.MaxValue));
    }

    [Fact]
    public void Invalid_frequency_and_negative_duration_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MonotonicTime.ToMilliseconds(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MonotonicTime.DurationToTimestampTicks(TimeSpan.FromTicks(-1), 1));
    }

    [Fact]
    public void Reads_and_conversions_have_no_steady_state_allocation()
    {
        _ = MonotonicTime.GetMilliseconds();
        _ = MonotonicTime.DurationToTimestampTicks(TimeSpan.FromMilliseconds(10), 10_000_000);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = MonotonicTime.GetTimestamp();
            _ = MonotonicTime.GetMilliseconds();
            _ = MonotonicTime.DurationToTimestampTicks(TimeSpan.FromMilliseconds(10), 10_000_000);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
