#nullable enable

using System;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 用于 <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 风格播放的玩法无关插值时间线。
    /// 它会根据帧增量推进本地服务器时间估计，吸收传入快照中观测到的权威服务器 tick，
    /// 并暴露用于采样 <see cref="RemoteSnapshotBuffer{TSnapshot}"/> 的延迟播放时间。
    ///
    /// 时间线会有意让远端播放固定落后最新权威样本 <see cref="InterpolationDelayTicks"/>，
    /// 使目标时间在正常抖动下落在两个已缓冲快照之间，让远端实体平滑移动而不是跳变。
    ///
    /// 支持两种校正行为。默认模式（<see cref="MaxCatchUpRate"/> == 0）会把本地估计直接吸附到权威目标，
    /// 简单且完全确定；正数追赶速率则会让估计值在若干帧内以有界速率收敛到权威目标，
    /// 从而平滑吸收本地帧时钟与服务器 tick 流之间的时钟漂移，避免可见跳变。
    /// </summary>
    public sealed class InterpolationTimeline : ITimelineDelayControl
    {
        // 权威目标时间：由帧增量推进，并通过观测到的服务器 tick 仅向前校正。
        // 软追赶模式下估计值会落后于该目标；吸附模式下二者相等。
        private double _targetTicks;
        private double _estimatedTicks;
        private bool _hasServerTime;
        private readonly double _maxCatchUpRate;
        private long _interpolationDelayTicks;
        private long _targetInterpolationDelayTicks;
        private double _delayConvergenceBudget;

        public InterpolationTimeline(long ticksPerSecond, long interpolationDelayTicks)
            : this(ticksPerSecond, interpolationDelayTicks, 0d)
        {
        }

        /// <param name="ticksPerSecond">时间线分辨率：一秒对应的 tick 数。</param>
        /// <param name="interpolationDelayTicks">播放时间落后最新权威时间的距离。</param>
        /// <param name="maxCatchUpRate">
        /// 每帧可用于缩小估计值与权威目标差距的最大额外推进比例。<c>0</c> 会将估计值吸附到目标；
        /// 例如 <c>0.1</c> 允许估计值最多以比真实时间快（或慢）10% 的速度追赶，从而平滑吸收漂移。
        /// 取值会被限制在 <c>[0, 1]</c>。
        /// </param>
        public InterpolationTimeline(long ticksPerSecond, long interpolationDelayTicks, double maxCatchUpRate)
        {
            if (ticksPerSecond <= 0L)
            {
                ticksPerSecond = 1L;
            }

            TicksPerSecond = ticksPerSecond;
            _interpolationDelayTicks = interpolationDelayTicks < 0L ? 0L : interpolationDelayTicks;
            _targetInterpolationDelayTicks = _interpolationDelayTicks;
            _maxCatchUpRate = maxCatchUpRate < 0d ? 0d : (maxCatchUpRate > 1d ? 1d : maxCatchUpRate);
        }

        /// <summary>时间线分辨率：一秒服务器时间对应的 tick 数。</summary>
        public long TicksPerSecond { get; }

        /// <summary>播放时间落后最新权威时间的距离，单位为 tick。</summary>
        public long InterpolationDelayTicks => _interpolationDelayTicks;

        public long TargetInterpolationDelayTicks => _targetInterpolationDelayTicks;

        long ITimelineDelayControl.DelayTicks => _interpolationDelayTicks;

        long ITimelineDelayControl.TargetDelayTicks => _targetInterpolationDelayTicks;

        public bool TrySetDelayTicks(long delayTicks)
        {
            if (delayTicks < 0L) return false;
            _targetInterpolationDelayTicks = delayTicks;
            return true;
        }

        /// <summary>
        /// 有界软追赶速率。零表示估计值吸附到权威目标；正数表示估计值会平滑收敛到目标。
        /// </summary>
        public double MaxCatchUpRate => _maxCatchUpRate;

        /// <summary>时间线是否已观测到至少一个权威服务器时间。</summary>
        public bool HasServerTime => _hasServerTime;

        /// <summary>当前本地服务器时间估计值，单位为 tick。</summary>
        public long EstimatedServerTicks => (long)_estimatedTicks;

        /// <summary>
        /// 估计值正在收敛的权威目标服务器时间，单位为 tick。吸附模式下等于 <see cref="EstimatedServerTicks"/>。
        /// </summary>
        public long TargetServerTicks => (long)_targetTicks;

        /// <summary>
        /// 用于采样远端快照缓冲区的延迟播放时间。它等于估计服务器时间减去插值延迟，且不会为负。
        /// </summary>
        public long PlaybackTicks
        {
            get
            {
                long playback = EstimatedServerTicks - InterpolationDelayTicks;
                return playback < 0L ? 0L : playback;
            }
        }

        /// <summary>
        /// 将权威服务器时间（来自收到的快照）合入时间线。首次观测会同时初始化目标值和估计值。
        /// 后续观测只会向前推进目标值（忽略过期时间）。吸附模式下估计值会随目标一起前移；
        /// 软追赶模式下估计值会在 <see cref="Advance"/> 中继续收敛。
        /// </summary>
        public void ObserveServerTicks(long serverTicks)
        {
            if (!_hasServerTime)
            {
                _targetTicks = serverTicks;
                _estimatedTicks = serverTicks;
                _hasServerTime = true;
                return;
            }

            if (serverTicks > _targetTicks)
            {
                _targetTicks = serverTicks;
            }

            if (_maxCatchUpRate <= 0d)
            {
                _estimatedTicks = _targetTicks;
            }
        }

        /// <summary>
        /// 按帧增量推进本地服务器时间估计。在首次观测到权威服务器时间前不执行任何操作。
        /// 权威目标按真实时间推进；软追赶模式下估计值会在真实时间推进量上叠加朝向目标的有界校正，
        /// 从而逐步吸收累计漂移。
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            if (!_hasServerTime || deltaSeconds <= 0f)
            {
                return;
            }

            var estimatedBefore = _estimatedTicks;
            double advance = deltaSeconds * TicksPerSecond;
            _targetTicks += advance;

            if (_maxCatchUpRate <= 0d)
            {
                _estimatedTicks = _targetTicks;
                ConvergeInterpolationDelay(_estimatedTicks - estimatedBefore);
                return;
            }

            double error = _targetTicks - _estimatedTicks;
            double maxCorrection = advance * _maxCatchUpRate;
            double correction = error * _maxCatchUpRate;
            if (correction > maxCorrection)
            {
                correction = maxCorrection;
            }
            else if (correction < -maxCorrection)
            {
                correction = -maxCorrection;
            }

            _estimatedTicks += advance + correction;
            ConvergeInterpolationDelay(_estimatedTicks - estimatedBefore);
        }

        private void ConvergeInterpolationDelay(double estimatedAdvance)
        {
            if (_interpolationDelayTicks == _targetInterpolationDelayTicks || estimatedAdvance <= 0d)
            {
                return;
            }

            _delayConvergenceBudget += estimatedAdvance;
            var maxStep = (long)Math.Floor(_delayConvergenceBudget);
            if (maxStep <= 0L) return;

            long appliedStep;
            if (_interpolationDelayTicks < _targetInterpolationDelayTicks)
            {
                appliedStep = Math.Min(
                    maxStep,
                    _targetInterpolationDelayTicks - _interpolationDelayTicks);
                _interpolationDelayTicks += appliedStep;
            }
            else
            {
                appliedStep = Math.Min(
                    maxStep,
                    _interpolationDelayTicks - _targetInterpolationDelayTicks);
                _interpolationDelayTicks -= appliedStep;
            }

            _delayConvergenceBudget -= appliedStep;
            if (_interpolationDelayTicks == _targetInterpolationDelayTicks)
                _delayConvergenceBudget = 0d;
        }

        public void Reset()
        {
            _targetTicks = 0d;
            _estimatedTicks = 0d;
            _hasServerTime = false;
            _interpolationDelayTicks = _targetInterpolationDelayTicks;
            _delayConvergenceBudget = 0d;
        }
    }
}
