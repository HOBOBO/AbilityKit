#nullable enable

namespace AbilityKit.Network.Runtime.Conditioning
{
    /// <summary>
    /// 可复现的模拟网络环境描述，与玩法无关：任意示例（Shooter、MOBA）均可在任意配置下运行任意同步模型。延迟以单向毫秒数表示，入站与出站组合模拟 RTT。
    /// </summary>
    public readonly struct NetworkConditionProfile
    {
        public NetworkConditionProfile(
            int baseLatencyMs,
            int jitterMs,
            double packetLossRate,
            double reorderRate,
            int bandwidthKbps)
        {
            BaseLatencyMs = baseLatencyMs < 0 ? 0 : baseLatencyMs;
            JitterMs = jitterMs < 0 ? 0 : jitterMs;
            PacketLossRate = Clamp01(packetLossRate);
            ReorderRate = Clamp01(reorderRate);
            BandwidthKbps = bandwidthKbps < 0 ? 0 : bandwidthKbps;
        }

        /// <summary>
        /// 应用于每个数据包的单向基础延迟，单位为毫秒。
        /// </summary>
        public int BaseLatencyMs { get; }

        /// <summary>
        /// 叠加在 <see cref="BaseLatencyMs"/> 上的最大正负抖动，单位为毫秒。
        /// </summary>
        public int JitterMs { get; }

        /// <summary>
        /// 数据包被丢弃的概率，范围为 [0,1]。
        /// </summary>
        public double PacketLossRate { get; }

        /// <summary>
        /// 数据包提前于计划时间投递、从而相对邻近包乱序的概率，范围为 [0,1]。
        /// </summary>
        public double ReorderRate { get; }

        /// <summary>
        /// 带宽上限，单位为千比特每秒。0 表示不限。
        /// </summary>
        public int BandwidthKbps { get; }

        /// <summary>
        /// 理想链路：零延迟、无丢包，用作对照基线。
        /// </summary>
        public static NetworkConditionProfile Ideal =>
            new NetworkConditionProfile(0, 0, 0d, 0d, 0);

        /// <summary>
        /// 局域网：数毫秒延迟、无丢包。
        /// </summary>
        public static NetworkConditionProfile Lan =>
            new NetworkConditionProfile(5, 1, 0d, 0d, 0);

        /// <summary>
        /// 典型 4G 移动链路：中等延迟及明显抖动。
        /// </summary>
        public static NetworkConditionProfile Mobile4G =>
            new NetworkConditionProfile(60, 20, 0.005d, 0.01d, 0);

        /// <summary>
        /// 跨区域链路：高基础延迟、轻度抖动。
        /// </summary>
        public static NetworkConditionProfile CrossRegion =>
            new NetworkConditionProfile(150, 25, 0.01d, 0.01d, 0);

        /// <summary>
        /// 较差 Wi-Fi：高抖动及明显丢包，用于同步模型压力测试。
        /// </summary>
        public static NetworkConditionProfile PoorWifi =>
            new NetworkConditionProfile(80, 60, 0.05d, 0.05d, 0);

        /// <summary>
        /// 低带宽链路（128 Kbps），其余条件理想。用于验证传输限制吞吐量时，同步模型在带宽预算下仍保持正确。
        /// </summary>
        public static NetworkConditionProfile LimitedBandwidth =>
            new NetworkConditionProfile(0, 0, 0d, 0d, 128);

        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            if (value > 1d) return 1d;
            return value;
        }
    }
}
