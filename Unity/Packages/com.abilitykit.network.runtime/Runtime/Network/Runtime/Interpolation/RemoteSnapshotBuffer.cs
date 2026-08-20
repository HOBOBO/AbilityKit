#nullable enable

using System;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 玩法无关的远端权威快照有序缓冲区，以单调递增的时间线值（通常是服务器 tick）作为键。
    /// <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 风格的控制器会使用它保留一小段远端状态历史，
    /// 让表现层可以延迟播放并平滑插值实体状态，而不是直接跳到最新推送。
    ///
    /// 缓冲区会按 <see cref="IRemoteSnapshotSample.TimelineTicks"/> 升序保存样本，丢弃乱序或重复样本，
    /// 并按有界容量裁剪旧样本。
    /// </summary>
    /// <typeparam name="TSnapshot">远端快照载荷类型。</typeparam>
    public sealed class RemoteSnapshotBuffer<TSnapshot>
        : IBufferCapacityControl
        where TSnapshot : IRemoteSnapshotSample
    {
        public const int DefaultCapacity = 32;

        private readonly ISequentialBuffer<TSnapshot> _samples;

        public RemoteSnapshotBuffer()
            : this(DefaultCapacity)
        {
        }

        public RemoteSnapshotBuffer(int capacity)
            : this(CreateDefaultStorage(capacity))
        {
        }

        public RemoteSnapshotBuffer(ISequentialBuffer<TSnapshot> storage)
        {
            _samples = storage ?? throw new ArgumentNullException(nameof(storage));
            if (_samples.Capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(storage), _samples.Capacity, "Storage capacity must be at least 2 to interpolate between two samples.");
            }

            if (_samples.Count != 0)
            {
                throw new ArgumentException("Injected storage must be empty.", nameof(storage));
            }
        }

        public int Count => _samples.Count;

        public int Capacity => _samples.Capacity;

        public bool IsEmpty => _samples.Count == 0;

        /// <summary>
        /// 已缓冲的最新时间线值；缓冲区为空时为 <c>null</c>。
        /// </summary>
        public long? NewestTimelineTicks => _samples.Count == 0 ? (long?)null : _samples[_samples.Count - 1].TimelineTicks;

        /// <summary>
        /// 已缓冲的最旧时间线值；缓冲区为空时为 <c>null</c>。
        /// </summary>
        public long? OldestTimelineTicks => _samples.Count == 0 ? (long?)null : _samples[0].TimelineTicks;

        /// <summary>
        /// 将快照加入缓冲区。时间线值小于或等于最新已缓冲样本的快照会被视为过期并拒绝，
        /// 从而保持缓冲区严格递增。
        /// </summary>
        /// <returns>快照被接受时返回 <c>true</c>；快照过期或重复时返回 <c>false</c>。</returns>
        public bool TryAdd(TSnapshot snapshot)
        {
            if (_samples.Count > 0 && snapshot.TimelineTicks <= _samples[_samples.Count - 1].TimelineTicks)
            {
                return false;
            }

            _samples.AddLast(snapshot);
            return true;
        }

        /// <summary>
        /// 选择包围目标时间线值的两个样本，并计算二者之间的插值系数。
        /// 当目标早于最旧样本时，结果会钳制到最旧样本；当目标晚于最新样本时，
        /// 结果会基于最新样本报告外推距离。
        /// </summary>
        public bool TrySample(long targetTimelineTicks, out RemoteSnapshotInterpolation<TSnapshot> interpolation)
        {
            interpolation = default;
            if (_samples.Count == 0)
            {
                return false;
            }

            if (_samples.Count == 1)
            {
                var only = _samples[0];
                long aheadTicks = targetTimelineTicks - only.TimelineTicks;
                interpolation = new RemoteSnapshotInterpolation<TSnapshot>(only, only, 0f, aheadTicks > 0L ? aheadTicks : 0L);
                return true;
            }

            var oldest = _samples[0];
            if (targetTimelineTicks <= oldest.TimelineTicks)
            {
                interpolation = new RemoteSnapshotInterpolation<TSnapshot>(oldest, oldest, 0f, 0L);
                return true;
            }

            var newest = _samples[_samples.Count - 1];
            if (targetTimelineTicks >= newest.TimelineTicks)
            {
                long extrapolationTicks = targetTimelineTicks - newest.TimelineTicks;
                interpolation = new RemoteSnapshotInterpolation<TSnapshot>(newest, newest, 0f, extrapolationTicks);
                return true;
            }

            for (int i = _samples.Count - 1; i > 0; i--)
            {
                var to = _samples[i];
                var from = _samples[i - 1];
                if (targetTimelineTicks >= from.TimelineTicks && targetTimelineTicks <= to.TimelineTicks)
                {
                    long span = to.TimelineTicks - from.TimelineTicks;
                    float alpha = span <= 0L
                        ? 0f
                        : (float)((targetTimelineTicks - from.TimelineTicks) / (double)span);
                    interpolation = new RemoteSnapshotInterpolation<TSnapshot>(from, to, Clamp01(alpha), 0L);
                    return true;
                }
            }

            // 根据上面的包围检查理论上不可达，这里保留兜底逻辑。
            interpolation = new RemoteSnapshotInterpolation<TSnapshot>(newest, newest, 0f, 0L);
            return true;
        }

        public void Clear()
        {
            _samples.Clear();
        }

        public bool TrySetCapacity(int capacity)
        {
            if (capacity < 2) return false;
            return _samples.TrySetCapacity(capacity);
        }

        private static ISequentialBuffer<TSnapshot> CreateDefaultStorage(int capacity)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 2 to interpolate between two samples.");
            }

            return new RingSequentialBuffer<TSnapshot>(capacity);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }

    /// <summary>
    /// 存储在 <see cref="RemoteSnapshotBuffer{TSnapshot}"/> 中的远端快照契约。
    /// 快照之间的时间线值必须单调递增；Shooter 示例中以服务器 tick 作为标准来源。
    /// </summary>
    public interface IRemoteSnapshotSample
    {
        long TimelineTicks { get; }
    }

    /// <summary>
    /// 在目标时间线值上采样 <see cref="RemoteSnapshotBuffer{TSnapshot}"/> 的结果：
    /// 包围目标的快照、二者之间的插值系数，以及当目标晚于最新样本时播放需要外推的距离。
    /// </summary>
    public readonly struct RemoteSnapshotInterpolation<TSnapshot>
        where TSnapshot : IRemoteSnapshotSample
    {
        public RemoteSnapshotInterpolation(TSnapshot from, TSnapshot to, float alpha, long extrapolationTicks)
        {
            From = from;
            To = to;
            Alpha = alpha;
            ExtrapolationTicks = extrapolationTicks;
        }

        /// <summary>包围目标的较早快照。</summary>
        public TSnapshot From { get; }

        /// <summary>包围目标的较晚快照。钳制或外推时等于 <see cref="From"/>。</summary>
        public TSnapshot To { get; }

        /// <summary><see cref="From"/> 与 <see cref="To"/> 之间的 [0,1] 插值系数。</summary>
        public float Alpha { get; }

        /// <summary>
        /// 目标时间超过最新样本的距离，单位为时间线 tick。目标位于缓冲范围内时为零。
        /// 正数表示播放已缺样并停留在最新样本上（外推策略可以据此采取动作）。
        /// </summary>
        public long ExtrapolationTicks { get; }

        /// <summary>该采样是否是在两个不同快照之间进行的真实插值。</summary>
        public bool IsInterpolating => !ReferenceEquals(From, To) && ExtrapolationTicks == 0L && Alpha > 0f && Alpha < 1f;

        /// <summary>目标时间是否晚于最新已缓冲快照。</summary>
        public bool IsExtrapolating => ExtrapolationTicks > 0L;
    }
}
