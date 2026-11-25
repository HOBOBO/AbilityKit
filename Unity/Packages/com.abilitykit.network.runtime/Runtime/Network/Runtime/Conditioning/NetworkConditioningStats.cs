#nullable enable

namespace AbilityKit.Network.Runtime.Conditioning
{
    /// <summary>
    /// <see cref="NetworkConditioningMiddleware"/> 的运行计数器，供示例叠加显示及测试断言使用。
    /// </summary>
    public readonly struct NetworkConditioningStats
    {
        public NetworkConditioningStats(
            long inboundReceived,
            long inboundDelivered,
            long inboundDropped,
            long inboundReordered,
            long outboundReceived,
            long outboundDelivered,
            long outboundDropped,
            long outboundReordered,
            int pendingCount)
        {
            InboundReceived = inboundReceived;
            InboundDelivered = inboundDelivered;
            InboundDropped = inboundDropped;
            InboundReordered = inboundReordered;
            OutboundReceived = outboundReceived;
            OutboundDelivered = outboundDelivered;
            OutboundDropped = outboundDropped;
            OutboundReordered = outboundReordered;
            PendingCount = pendingCount;
        }

        public long InboundReceived { get; }
        public long InboundDelivered { get; }
        public long InboundDropped { get; }
        public long InboundReordered { get; }
        public long OutboundReceived { get; }
        public long OutboundDelivered { get; }
        public long OutboundDropped { get; }
        public long OutboundReordered { get; }

        /// <summary>
        /// 当前已缓冲、等待计划投递时间的数据包数量。
        /// </summary>
        public int PendingCount { get; }
    }
}
