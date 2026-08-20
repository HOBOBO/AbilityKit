#nullable enable

using System;
using AbilityKit.Core.Buffers;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 用于 <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 风格远端播放的玩法无关编排器。
    /// 它持有 <see cref="RemoteSnapshotBuffer{TSnapshot}"/> 与 <see cref="InterpolationTimeline"/>，
    /// 并管理外推/缺样策略，因此每个 Demo 只需要：
    /// (1) 将传入推送解码为 <typeparamref name="TSample"/> 并通过 <see cref="Observe"/> 输入；
    /// (2) 将采样得到的插值结果投影并应用到表现层。
    ///
    /// 典型逐帧用法：
    /// <code>
    /// playback.Advance(deltaSeconds);
    /// if (playback.TrySample(out var interpolation))
    /// {
    ///     var projected = project(in interpolation);
    ///     presentation.Apply(in projected);
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="TSample">已缓冲的远端快照样本类型。</typeparam>
    public sealed class RemoteInterpolationPlayback<TSample>
        where TSample : IRemoteSnapshotSample
    {
        private readonly RemoteSnapshotBuffer<TSample> _buffer;
        private readonly InterpolationTimeline _timeline;
        private readonly long _maxExtrapolationTicks;

        public RemoteInterpolationPlayback()
            : this(InterpolationConfig.Default)
        {
        }

        public RemoteInterpolationPlayback(InterpolationConfig config)
            : this(config, new RemoteSnapshotBuffer<TSample>(config.BufferCapacity))
        {
        }

        /// <summary>
        /// Creates playback with an explicitly assembled snapshot buffer.
        /// The injected buffer's capacity is authoritative over <see cref="InterpolationConfig.BufferCapacity"/>.
        /// </summary>
        public RemoteInterpolationPlayback(
            InterpolationConfig config,
            RemoteSnapshotBuffer<TSample> snapshotBuffer)
        {
            _buffer = snapshotBuffer ?? throw new ArgumentNullException(nameof(snapshotBuffer));
            _timeline = new InterpolationTimeline(config.TicksPerSecond, config.InterpolationDelayTicks, config.CatchUpRate);
            _maxExtrapolationTicks = config.MaxExtrapolationTicks;
        }

        /// <summary>当前为插值缓冲的远端权威快照数量。</summary>
        public int BufferedSampleCount => _buffer.Count;

        public IBufferCapacityControl SnapshotCapacityControl => _buffer;

        public ITimelineDelayControl TimelineDelayControl => _timeline;

        /// <summary>当前延迟后的远端播放时间，单位为时间线 tick。</summary>
        public long PlaybackTicks => _timeline.PlaybackTicks;

        /// <summary>当前本地权威服务器时间估计值，单位为时间线 tick。</summary>
        public long EstimatedServerTicks => _timeline.EstimatedServerTicks;

        /// <summary>是否已通过 <see cref="TrySample"/> 产出过至少一个远端插值样本。</summary>
        public bool HasPublished { get; private set; }

        /// <summary>
        /// 最近一次 <see cref="TrySample"/> 是否发现延迟播放时间已经超过最新缓冲快照，
        /// 且超出距离大于 <see cref="InterpolationConfig.MaxExtrapolationTicks"/>。
        /// 这表示缓冲区缺样（例如快照停止到达），播放会停留在最后一个权威姿态，而不是继续外推。
        /// </summary>
        public bool IsStarved { get; private set; }

        /// <summary>
        /// 缓冲已解码的远端权威样本，并将其服务器时间合入时间线。
        /// 过期或重复样本（时间线值未严格晚于最新已缓冲样本）会被拒绝，且不会推进时间线。
        /// </summary>
        /// <returns>样本被接受时返回 <c>true</c>；样本过期或重复时返回 <c>false</c>。</returns>
        public bool Observe(TSample sample)
        {
            if (!_buffer.TryAdd(sample))
            {
                return false;
            }

            _timeline.ObserveServerTicks(sample.TimelineTicks);
            return true;
        }

        /// <summary>按帧增量推进延迟播放时间线。</summary>
        public void Advance(float deltaSeconds)
        {
            _timeline.Advance(deltaSeconds);
        }

        /// <summary>
        /// 在当前延迟播放时间上采样缓冲区。至少观测到一个权威时间且缓冲区非空之前返回 <c>false</c>。
        /// 成功时会按外推策略更新 <see cref="IsStarved"/>，并标记 <see cref="HasPublished"/>。
        ///
        /// 外推策略：当延迟播放时间超过最新已缓冲快照时，缓冲区处于缺样状态。
        /// 远端姿态不会被刻意向前外推（那会制造非权威运动）；返回的插值结果会停留在最新样本上。
        /// 一旦间隔超过配置容忍值，播放还会被标记为缺样，供调用方响应（例如显示连接质量提示）。
        /// </summary>
        public bool TrySample(out RemoteSnapshotInterpolation<TSample> interpolation)
        {
            interpolation = default;
            if (!_timeline.HasServerTime || _buffer.IsEmpty)
            {
                return false;
            }

            if (!_buffer.TrySample(_timeline.PlaybackTicks, out interpolation))
            {
                return false;
            }

            IsStarved = interpolation.ExtrapolationTicks > _maxExtrapolationTicks;
            HasPublished = true;
            return true;
        }

        /// <summary>捕获当前插值播放健康状态，用于诊断或冒烟输出。</summary>
        public InterpolationDiagnostics GetDiagnostics()
        {
            return new InterpolationDiagnostics(
                bufferedRemoteSnapshotCount: BufferedSampleCount,
                remotePlaybackTicks: PlaybackTicks,
                estimatedServerTicks: EstimatedServerTicks,
                hasPublishedRemoteFrame: HasPublished,
                isRemotePlaybackStarved: IsStarved);
        }

        /// <summary>清空缓冲区，并重置时间线和播放标志。</summary>
        public void Reset()
        {
            _buffer.Clear();
            _timeline.Reset();
            HasPublished = false;
            IsStarved = false;
        }
    }
}
