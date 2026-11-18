using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AbilityKit.Core.Timing
{
    /// <summary>
    /// 提供正常运行期间不倒退的不透明时间戳。
    /// 时间戳只能结合生成它的同一时钟频率解释，不表示日期、UTC 或可持久化时刻。
    /// </summary>
    /// <remarks>
    /// 实现应提供稳定的正频率，调用方只能比较同一时钟来源的读数。
    /// 自定义时钟的驱动与并发读取保证由其实现负责，本接口不提供调度或模拟帧推进。
    /// </remarks>
    public interface IMonotonicClock
    {
        /// <summary>
        /// 获取当前不透明时间戳，其单位由同一时钟的频率定义。
        /// </summary>
        long Timestamp { get; }

        /// <summary>
        /// 获取每秒时间戳计数，用于解释当前时钟生成的时间戳。
        /// </summary>
        long Frequency { get; }
    }

    /// <summary>
    /// 提供进程内基于 Stopwatch 的高精度单调时钟。
    /// </summary>
    /// <remarks>
    /// 读取使用运行时的 Stopwatch 时间源；共享实例不维护额外状态。
    /// 不同进程、设备或自定义时钟的时间戳不能只因频率相同就直接比较。
    /// </remarks>
    public sealed class StopwatchMonotonicClock : IMonotonicClock
    {
        private StopwatchMonotonicClock()
        {
        }

        /// <summary>
        /// 获取共享的无状态时钟实例。
        /// </summary>
        public static StopwatchMonotonicClock Instance { get; } = new StopwatchMonotonicClock();

        /// <summary>
        /// 读取当前进程高精度时钟的时间戳。
        /// </summary>
        public long Timestamp => Stopwatch.GetTimestamp();

        /// <summary>
        /// 获取当前进程高精度时钟的每秒计数。
        /// </summary>
        public long Frequency => Stopwatch.Frequency;
    }

    /// <summary>
    /// 读取和转换可移植的单调时间戳，不表达墙上时钟日期或 UTC 时刻。
    /// </summary>
    /// <remarks>
    /// 本类型用于测量本地耗时与检查截止时间，不提供跨端同步或确定性的战斗模拟时钟。
    /// 换算方法支持显式提供其他时钟的正频率，但不会验证时间戳的实际来源。
    /// </remarks>
    public static class MonotonicTime
    {
        /// <summary>
        /// 获取当前时间戳读取入口使用的每秒计数。
        /// </summary>
        public static long TimestampFrequency => Stopwatch.Frequency;

        /// <summary>
        /// 读取当前高精度单调时间戳。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimestamp() => Stopwatch.GetTimestamp();

        /// <summary>
        /// 读取当前单调时间戳并转换为整数毫秒；该值不是 Unix 时间戳。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetMilliseconds() => ToMilliseconds(Stopwatch.GetTimestamp(), Stopwatch.Frequency);

        /// <summary>
        /// 按指定频率将时间戳转换为整数毫秒，向零截断不足一毫秒的部分。
        /// </summary>
        /// <param name="timestamp">以指定时钟频率表达的时间戳。</param>
        /// <param name="frequency">每秒时间戳计数，必须为正。</param>
        /// <returns>对应的整数毫秒数。</returns>
        /// <exception cref="ArgumentOutOfRangeException">时钟频率不是正数。</exception>
        /// <exception cref="OverflowException">转换后的整数毫秒数超出长整数范围。</exception>
        public static long ToMilliseconds(long timestamp, long frequency)
        {
            ValidateFrequency(frequency);

            var wholeSeconds = timestamp / frequency;
            /*
             * 分离整秒与余数，避免直接乘以千导致本可表示的换算溢出。
             * 余数乘法也可能溢出，此时通过十进制中间值保持向零截断；最终范围仍显式检查。
             */
            var remainder = timestamp % frequency;
            var wholeMilliseconds = checked(wholeSeconds * 1000L);
            var remainderMilliseconds = remainder <= long.MaxValue / 1000L && remainder >= long.MinValue / 1000L
                ? remainder * 1000L / frequency
                : (long)((decimal)remainder * 1000m / frequency);
            return checked(wholeMilliseconds + remainderMilliseconds);
        }

        /// <summary>
        /// 将非负持续时间转换为时钟计数，正的小数计数向上取整，
        /// 确保截止检查不早于请求时长到期；结果超出长整数范围时饱和为最大值。
        /// </summary>
        /// <remarks>
        /// 输入单位为 TimeSpan 计数，输出单位为指定频率下的时钟计数；两者不一定相同。
        /// 饱和仅作用于换算结果，将结果加到当前时间戳时的溢出处理仍由调用方负责。
        /// </remarks>
        /// <param name="duration">待转换的非负持续时间。</param>
        /// <param name="frequency">每秒时间戳计数，必须为正。</param>
        /// <returns>向上取整的计数；零时长返回零，超出表示范围返回长整数最大值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">时长为负，或时钟频率不是正数。</exception>
        public static long DurationToTimestampTicks(TimeSpan duration, long frequency)
        {
            ValidateFrequency(frequency);
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            if (duration == TimeSpan.Zero) return 0L;

            var durationTicks = duration.Ticks;
            /*
             * 先检查整秒贡献能否表示，再拆分频率计算不足一秒的部分，避免中间乘法溢出。
             * 最后的非整计数向上补一，并在与整秒贡献合并时执行饱和检查。
             */
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
