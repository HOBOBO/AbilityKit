#nullable enable

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// A point-in-time snapshot of authoritative remote interpolation playback health, surfaced for
    /// diagnostics / smoke output / connection-quality hints. This is intentionally kept off the
    /// mode-agnostic <see cref="INetworkSyncController"/> surface: only controllers that actually
    /// interpolate implement <see cref="IInterpolationDiagnosticsProvider"/>.
    /// </summary>
    public readonly struct InterpolationDiagnostics
    {
        public InterpolationDiagnostics(
            int bufferedRemoteSnapshotCount,
            long remotePlaybackTicks,
            long estimatedServerTicks,
            bool hasPublishedRemoteFrame,
            bool isRemotePlaybackStarved)
        {
            BufferedRemoteSnapshotCount = bufferedRemoteSnapshotCount;
            RemotePlaybackTicks = remotePlaybackTicks;
            EstimatedServerTicks = estimatedServerTicks;
            HasPublishedRemoteFrame = hasPublishedRemoteFrame;
            IsRemotePlaybackStarved = isRemotePlaybackStarved;
        }

        /// <summary>
        /// 当前为插值缓冲的远端权威快照数量。
        /// </summary>
        public int BufferedRemoteSnapshotCount { get; }

        /// <summary>
        /// 当前延迟后的远端播放时间，单位为时间轴刻度。
        /// </summary>
        public long RemotePlaybackTicks { get; }

        /// <summary>
        /// 当前本地估计的服务端权威时间，单位为时间轴刻度。
        /// </summary>
        public long EstimatedServerTicks { get; }

        /// <summary>
        /// 是否至少已有一个远端插值帧发布到表现层。
        /// </summary>
        public bool HasPublishedRemoteFrame { get; }

        /// <summary>
        /// Whether the delayed playback time is running past the newest buffered snapshot by more than
        /// the configured extrapolation tolerance (the remote buffer is starved and playback is holding
        /// the last authoritative pose rather than extrapolating further).
        /// </summary>
        public bool IsRemotePlaybackStarved { get; }

        /// <summary>
        /// 播放时钟落后估计服务端时间的距离，单位为时间轴刻度。
        /// </summary>
        public long PlaybackDelayTicks => EstimatedServerTicks - RemotePlaybackTicks;
    }

    /// <summary>
    /// Implemented by sync controllers that run authoritative remote interpolation and can therefore
    /// report <see cref="InterpolationDiagnostics"/>. Controllers for non-interpolating sync models do
    /// not implement this, so consumers should probe with a type check / <c>TryGet</c> pattern.
    /// </summary>
    public interface IInterpolationDiagnosticsProvider
    {
        InterpolationDiagnostics GetInterpolationDiagnostics();
    }
}
