using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AbilityKit.Core.Timing
{
    /// <summary>
    /// Supplies opaque timestamps from a clock that cannot move backwards during normal operation.
    /// Timestamp values are meaningful only with the frequency reported by the same clock.
    /// </summary>
    public interface IMonotonicClock
    {
        /// <summary>Gets the current opaque timestamp.</summary>
        long Timestamp { get; }

        /// <summary>Gets the number of timestamp ticks per second.</summary>
        long Frequency { get; }
    }

    /// <summary>Provides the process-wide high-resolution <see cref="Stopwatch"/> clock.</summary>
    public sealed class StopwatchMonotonicClock : IMonotonicClock
    {
        private StopwatchMonotonicClock()
        {
        }

        /// <summary>Gets the shared stateless clock instance.</summary>
        public static StopwatchMonotonicClock Instance { get; } = new StopwatchMonotonicClock();

        /// <inheritdoc />
        public long Timestamp => Stopwatch.GetTimestamp();

        /// <inheritdoc />
        public long Frequency => Stopwatch.Frequency;
    }

    /// <summary>Reads and converts portable monotonic timestamps without wall-clock semantics.</summary>
    public static class MonotonicTime
    {
        /// <summary>Gets the number of ticks per second used by <see cref="GetTimestamp"/>.</summary>
        public static long TimestampFrequency => Stopwatch.Frequency;

        /// <summary>Gets the current high-resolution monotonic timestamp.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimestamp() => Stopwatch.GetTimestamp();

        /// <summary>Gets the current monotonic timestamp converted to whole milliseconds.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetMilliseconds() => ToMilliseconds(Stopwatch.GetTimestamp(), Stopwatch.Frequency);

        /// <summary>Converts an opaque timestamp to whole milliseconds, truncating fractional milliseconds.</summary>
        /// <param name="timestamp">Timestamp expressed in <paramref name="frequency"/> ticks per second.</param>
        /// <param name="frequency">The positive number of timestamp ticks per second.</param>
        public static long ToMilliseconds(long timestamp, long frequency)
        {
            ValidateFrequency(frequency);

            var wholeSeconds = timestamp / frequency;
            var remainder = timestamp % frequency;
            var wholeMilliseconds = checked(wholeSeconds * 1000L);
            var remainderMilliseconds = remainder <= long.MaxValue / 1000L && remainder >= long.MinValue / 1000L
                ? remainder * 1000L / frequency
                : (long)((decimal)remainder * 1000m / frequency);
            return checked(wholeMilliseconds + remainderMilliseconds);
        }

        /// <summary>
        /// Converts a non-negative duration to clock ticks, rounding a positive fractional tick upward
        /// so deadline checks cannot expire earlier than requested.
        /// </summary>
        /// <param name="duration">The non-negative duration to convert.</param>
        /// <param name="frequency">The positive number of timestamp ticks per second.</param>
        public static long DurationToTimestampTicks(TimeSpan duration, long frequency)
        {
            ValidateFrequency(frequency);
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            if (duration == TimeSpan.Zero) return 0L;

            var durationTicks = duration.Ticks;
            var wholeSeconds = durationTicks / TimeSpan.TicksPerSecond;
            if (wholeSeconds > long.MaxValue / frequency) return long.MaxValue;

            var result = wholeSeconds * frequency;
            var remainder = durationTicks % TimeSpan.TicksPerSecond;
            if (remainder == 0) return result;

            var frequencySeconds = frequency / TimeSpan.TicksPerSecond;
            var frequencyRemainder = frequency % TimeSpan.TicksPerSecond;
            var fractionalTicks = remainder * frequencySeconds;
            var fractionalNumerator = remainder * frequencyRemainder;
            fractionalTicks += fractionalNumerator / TimeSpan.TicksPerSecond;
            if (fractionalNumerator % TimeSpan.TicksPerSecond != 0) fractionalTicks++;

            return fractionalTicks > long.MaxValue - result
                ? long.MaxValue
                : result + fractionalTicks;
        }

        private static void ValidateFrequency(long frequency)
        {
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
        }
    }
}
