#nullable enable

using System;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 附加在权威实体样本上的协议无关交付语义。
    /// 各项目编解码器在表现层边界将线协议标志映射为这些提示。
    /// </summary>
    [Flags]
    public enum SnapshotDeliveryHints : byte
    {
        /// <summary>没有特殊交付行为。</summary>
        None = 0,

        /// <summary>实体可能在中间快照中被省略。</summary>
        SparseUpdate = 1 << 0,

        /// <summary>本地控制者保留预测得到的表现层变换。</summary>
        PredictedOwner = 1 << 1,

        /// <summary>样本发生不连续变化，不得从上一姿态插值过渡。</summary>
        Teleport = 1 << 2,

        /// <summary>即使存在两个时间线样本，也应使用阶跃方式采样。</summary>
        NoInterpolation = 1 << 3
    }

    /// <summary>根据协议无关交付提示做出通用决策。</summary>
    public static class SnapshotDeliveryPolicy
    {
        /// <summary>
        /// 返回权威变换是否应覆盖当前表现姿态。
        /// </summary>
        public static bool ShouldApplyAuthoritativeTransform(
            SnapshotDeliveryHints hints,
            bool isLocallyControlled)
        {
            return !isLocallyControlled ||
                (hints & SnapshotDeliveryHints.PredictedOwner) == 0;
        }
    }
}
