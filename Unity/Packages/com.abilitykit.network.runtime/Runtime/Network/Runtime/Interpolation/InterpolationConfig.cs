#nullable enable

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 由 <see cref="RemoteSnapshotBuffer{TSample}"/> 与 <see cref="InterpolationTimeline"/> 驱动的
    /// 玩法无关权威远端回放参数。<see cref="TicksPerSecond"/> 将服务器权威 tick 时间线映射到现实秒数；
    /// <see cref="InterpolationDelayTicks"/> 指定远端回放落后最新权威样本的距离，用于吸收抖动并保留两个插值端点；
    /// <see cref="BufferCapacity"/> 限制保留的远端快照数量。
    /// </summary>
    public readonly struct InterpolationConfig
    {
        public InterpolationConfig(long ticksPerSecond, long interpolationDelayTicks, int bufferCapacity)
            : this(ticksPerSecond, interpolationDelayTicks, bufferCapacity, DefaultCatchUpRate, DefaultMaxExtrapolationTicks)
        {
        }

        public InterpolationConfig(long ticksPerSecond, long interpolationDelayTicks, int bufferCapacity, double catchUpRate)
            : this(ticksPerSecond, interpolationDelayTicks, bufferCapacity, catchUpRate, DefaultMaxExtrapolationTicks)
        {
        }

        public InterpolationConfig(long ticksPerSecond, long interpolationDelayTicks, int bufferCapacity, double catchUpRate, long maxExtrapolationTicks)
        {
            TicksPerSecond = ticksPerSecond <= 0L ? 1L : ticksPerSecond;
            InterpolationDelayTicks = interpolationDelayTicks < 0L ? 0L : interpolationDelayTicks;
            BufferCapacity = bufferCapacity < 2 ? 2 : bufferCapacity;
            CatchUpRate = catchUpRate < 0d ? 0d : (catchUpRate > 1d ? 1d : catchUpRate);
            MaxExtrapolationTicks = maxExtrapolationTicks < 0L ? 0L : maxExtrapolationTicks;
        }

        /// <summary>默认柔性追帧速率：每帧最多按现实时间的 10% 吸收时钟漂移。</summary>
        public const double DefaultCatchUpRate = 0.1d;

        /// <summary>
        /// 默认外推容忍度：当延迟回放时间超过最新缓冲快照 50ms 时，缓冲区被视为饥饿；
        /// 此时保持最后一个权威姿态并标记饥饿，不再继续漂移。
        /// </summary>
        public const long DefaultMaxExtrapolationTicks = 50L;

        public long TicksPerSecond { get; }

        public long InterpolationDelayTicks { get; }

        public int BufferCapacity { get; }

        /// <summary>
        /// 回放时钟向服务器权威时间收敛的强度。零表示直接跳转；正值会平滑吸收漂移，最大限制为 1。
        /// 参见 <see cref="InterpolationTimeline.MaxCatchUpRate"/>。
        /// </summary>
        public double CatchUpRate { get; }

        /// <summary>
        /// 延迟回放超过最新缓冲快照多远后被视为饥饿。在该容忍范围内保持最新权威姿态；
        /// 超出后控制器将缓冲区标记为饥饿，参见
        /// <see cref="InterpolationDiagnostics.IsRemotePlaybackStarved"/>。
        /// </summary>
        public long MaxExtrapolationTicks { get; }

        /// <summary>
        /// 默认参数：在毫秒时间线上使用 100ms 插值延迟，保留最近 32 个远端快照，
        /// 使用柔性时钟收敛与 50ms 外推容忍度。若样本中的服务器 tick 使用其他单位，
        /// 应提供与之匹配的 <see cref="TicksPerSecond"/>。
        /// </summary>
        public static InterpolationConfig Default => new InterpolationConfig(
            ticksPerSecond: 1000L,
            interpolationDelayTicks: 100L,
            bufferCapacity: 32,
            catchUpRate: DefaultCatchUpRate,
            maxExtrapolationTicks: DefaultMaxExtrapolationTicks);
    }
}
