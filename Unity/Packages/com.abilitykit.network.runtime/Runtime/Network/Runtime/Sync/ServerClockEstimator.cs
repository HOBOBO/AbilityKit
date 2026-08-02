#nullable enable

using System;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// 协议无关的估算器，用于把 NTP 风格的时间同步往返样本转换为本地时钟与权威服务器时钟之间的
    /// 持续偏移估计，以及往返时间（RTT）。它刻意只接收原始 tick 值，从而与任何具体线协议类型
    /// （例如网关时间同步响应）解耦：调用方传入 <c>clientSendTicks</c>、<c>serverReceiveTicks</c> 和
    /// <c>clientReceiveTicks</c>，估算器会折叠每个样本。
    ///
    /// 与真实 NTP 客户端类似，RTT 最低（排队/抖动最少）的样本通常最准确，因此估算器保留目前观测到的
    /// 最低 RTT 样本对应偏移，而不是对噪声样本求平均。这样 <c>ServerTicksNow</c> 在抖动下保持稳定，
    /// 同时在更好（更低 RTT）样本到来时仍能快速收敛。
    /// </summary>
    public sealed class ServerClockEstimator
    {
        private long _bestRttTicks;
        private long _bestOffsetTicks;
        private int _sampleCount;

        /// <param name="serverTickFrequency">
        /// 一秒对应多少个服务器 tick。用于把 RTT/偏移转换为秒以便诊断。必须为正；非正值会按 1 处理，避免除零。
        /// </param>
        public ServerClockEstimator(long serverTickFrequency)
        {
            ServerTickFrequency = serverTickFrequency <= 0L ? 1L : serverTickFrequency;
        }

        /// <summary>一秒对应多少个服务器 tick。</summary>
        public long ServerTickFrequency { get; }

        /// <summary>是否已经观测到至少一个往返样本。</summary>
        public bool HasSample => _sampleCount > 0;

        /// <summary>目前已折叠的往返样本数量。</summary>
        public int SampleCount => _sampleCount;

        /// <summary>
        /// 目前观测到的最佳（最低）往返时间，单位为 tick。首个样本到来前为零。
        /// </summary>
        public long BestRoundTripTicks => _bestRttTicks;

        /// <summary>目前观测到的最佳往返时间，单位为秒。</summary>
        public double BestRoundTripSeconds => (double)_bestRttTicks / ServerTickFrequency;

        /// <summary>
        /// 需要加到本地时钟值上以获得服务器时间的估算偏移，单位为 tick。该值来自最低 RTT 样本对应偏移。
        /// 首个样本到来前为零。
        /// </summary>
        public long OffsetTicks => _bestOffsetTicks;

        /// <summary>估算时钟偏移，单位为秒。</summary>
        public double OffsetSeconds => (double)_bestOffsetTicks / ServerTickFrequency;

        /// <summary>
        /// 将一个往返时间同步样本折叠进估算。所有值都使用相同 tick 单位（<see cref="ServerTickFrequency"/>）。
        /// 本地发送/接收时间戳来自同一个本地时钟；服务器时间戳来自服务器时钟。
        ///
        /// RTT 为 <c>clientReceive - clientSend</c>。假设路径对称，服务器时间戳相当于在本地时间
        /// <c>clientSend + RTT/2</c> 处采集，因此偏移为 <c>serverReceive - (clientSend + RTT/2)</c>。
        /// 只有当该样本 RTT 是目前最低值（或它是首个样本）时才会采纳，模拟 NTP 的最佳样本选择。
        /// </summary>
        /// <param name="clientSendTicks">请求发出时的本地时钟。</param>
        /// <param name="serverReceiveTicks">服务器标记响应时的服务器时钟。</param>
        /// <param name="clientReceiveTicks">收到响应时的本地时钟。</param>
        /// <returns>如果该样本成为新的最佳估算则为 <c>true</c>；否则为 <c>false</c>。</returns>
        public bool ObserveRoundTrip(long clientSendTicks, long serverReceiveTicks, long clientReceiveTicks)
        {
            long rtt = clientReceiveTicks - clientSendTicks;
            if (rtt < 0L)
            {
                // 负 RTT 表示时间戳不一致（时钟回退或乱序投递）；拒绝该样本，避免污染估算。
                return false;
            }

            // 假设服务器时间戳采集点位于本地时间线上的往返中点。
            long localMidpoint = clientSendTicks + rtt / 2L;
            long offset = serverReceiveTicks - localMidpoint;

            if (_sampleCount == 0 || rtt < _bestRttTicks)
            {
                _bestRttTicks = rtt;
                _bestOffsetTicks = offset;
                _sampleCount++;
                return true;
            }

            _sampleCount++;
            return false;
        }

        /// <summary>
        /// 应用当前偏移，将本地时钟值转换为估算的权威服务器时钟值。没有样本前原样返回 <paramref name="localTicks"/>。
        /// </summary>
        public long ToServerTicks(long localTicks)
        {
            return HasSample ? localTicks + _bestOffsetTicks : localTicks;
        }

        /// <summary>
        /// 使用 <paramref name="localTicks"/> 对应的估算服务器时钟标记 <paramref name="anchor"/>。
        /// 尚未观测到样本时原样返回 anchor，确保调用方不会发布伪造的服务器时间。
        /// </summary>
        public SyncTimeAnchor StampServerTicks(SyncTimeAnchor anchor, long localTicks)
        {
            return HasSample ? anchor.WithServerTicks(ToServerTicks(localTicks)) : anchor;
        }

        /// <summary>清除所有已观测样本，将估算器恢复到初始状态。</summary>
        public void Reset()
        {
            _bestRttTicks = 0L;
            _bestOffsetTicks = 0L;
            _sampleCount = 0;
        }
    }
}
