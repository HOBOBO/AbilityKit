#nullable enable

using System;
using AbilityKit.Ability.Host;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// 用于 <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 的 Moba 客户端同步控制器，绑定到
    /// 与玩法无关的框架契约 <see cref="IClientSyncStrategy{TInput, TSample}"/>，使 demo 框架能通过与 Shooter
    /// 相同的入口驱动 Moba（Tick / SubmitInput / ObserveRemote / GetReconciliationReport）。这是第二个接入
    /// A-axis 契约的载体，用于证明抽象确实与 demo 无关：Moba 只提供自己的输入命令
    /// (<see cref="PlayerInputCommand"/>) 和远端样本 (<see cref="MobaRemoteSnapshotSample"/>)；buffer、延迟时间线、
    /// interpolation 和 starvation 策略都保留在共享框架中。
    ///
    /// 该控制器是 <see cref="MobaRemoteInterpolationPlayback"/> 的轻量包装。它是纯增量实现：现有 Moba
    /// playback/projection 代码保持不变，因此既有测试行为不变。
    /// </summary>
    public sealed class MobaClientAuthoritativeInterpolationSyncController
        : IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample>
    {
        private readonly MobaRemoteInterpolationPlayback _playback;
        private bool _started;
        private int _currentFrame;

        public MobaClientAuthoritativeInterpolationSyncController()
            : this(new MobaRemoteInterpolationPlayback())
        {
        }

        public MobaClientAuthoritativeInterpolationSyncController(InterpolationConfig config)
            : this(new MobaRemoteInterpolationPlayback(config))
        {
        }

        internal MobaClientAuthoritativeInterpolationSyncController(MobaRemoteInterpolationPlayback playback)
        {
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        }

        /// <inheritdoc />
        public NetworkSyncModel SyncModel => NetworkSyncModel.AuthoritativeInterpolation;

        /// <inheritdoc />
        public bool IsStarted => _started;

        /// <inheritdoc />
        public int CurrentFrame => _currentFrame;

        /// <summary>当前为 interpolation 缓存的远端权威快照数量。</summary>
        public int BufferedRemoteSnapshotCount => _playback.BufferedRemoteSnapshotCount;

        /// <summary>当前本地估计的权威服务器时间，单位为 timeline tick。</summary>
        public long EstimatedServerTicks => _playback.EstimatedServerTicks;

        /// <summary>是否已至少投影过一个远端 interpolation 帧。</summary>
        public bool HasPublishedRemoteFrame => _playback.HasPublishedRemoteFrame;

        /// <summary>远端 buffer 是否已饥饿，并由 playback 保持最后一个权威姿态。</summary>
        public bool IsRemotePlaybackStarved => _playback.IsRemotePlaybackStarved;

        /// <summary>
        /// 在当前延迟时间采样框架 playback，并将包围样本对投影为供 Moba 表现 pipeline 使用的
        /// interpolated <see cref="GatewayStateSyncSnapshot"/>。
        /// </summary>
        public bool TryProjectRemoteFrame(out GatewayStateSyncSnapshot snapshot)
            => _playback.TryProjectRemoteFrame(out snapshot);

        /// <summary>捕获当前 interpolation playback 健康状态，用于 diagnostics / smoke 输出。</summary>
        public InterpolationDiagnostics GetInterpolationDiagnostics()
            => _playback.GetInterpolationDiagnostics();

        /// <summary>清理 buffer，并重置 timeline、playback 标志和控制器状态。</summary>
        public void Reset()
        {
            _playback.Reset();
            _started = false;
            _currentFrame = 0;
        }

        /// <inheritdoc />
        public SyncTickResult Tick(float deltaSeconds)
        {
            _started = true;
            _playback.Advance(deltaSeconds);

            if (_playback.TryProjectRemoteFrame(out var snapshot))
            {
                _currentFrame = snapshot.Frame;
                return new SyncTickResult(ticks: 0, frame: snapshot.Frame, stateHash: 0u);
            }

            return new SyncTickResult(ticks: 0, frame: _currentFrame, stateHash: 0u);
        }

        /// <inheritdoc />
        public void SubmitInput(in PlayerInputCommand input)
        {
            // 对权威 interpolation 来说这里是 no-op：远端 actor 完全由观测到的权威样本驱动，
            // 不会由本地预测输入驱动。本地玩家命令走玩法命令 pipeline（gateway 提交），不走该表现策略。
        }

        /// <inheritdoc />
        public void ObserveRemote(in MobaRemoteSnapshotSample sample)
            => _playback.Observe(in sample);

        /// <inheritdoc />
        public SyncReconciliationReport GetReconciliationReport() => SyncReconciliationReport.None;
    }
}
