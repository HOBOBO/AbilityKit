namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 描述会话或玩法示例使用的网络同步策略。
    /// 该枚举保持玩法无关，便于不同示例共享同一套同步术语。
    ///
    /// <b>已实现：</b> <see cref="PredictRollback"/>、<see cref="AuthoritativeInterpolation"/>、
    /// <see cref="ServerRewindLagCompensation"/>。
    ///
    /// <b>预留（尚未实现）：</b> <see cref="Lockstep"/>、<see cref="BatchStateSync"/>、
    /// <see cref="MassBattleLodSync"/>、<see cref="HybridHeroPrediction"/>、
    /// <see cref="FastReconnect"/>。
    ///
    /// 预留值的存在是为了让 Shooter demo 的 sync template catalog
    /// 可以提前使用这些标签，无需等待完整实现。
    /// </summary>
    public enum NetworkSyncModel
    {
        /// <summary>
        /// 尚未选择明确的网络同步模型。
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// 确定性输入同步，不包含客户端回滚恢复。
        /// 尚未实现，预留给未来的 Lockstep 同步策略。
        /// </summary>
        Lockstep = 1,

        /// <summary>
        /// 本地预测、权威快照、回滚、重放和校正。
        /// 已实现，见预测回滚客户端同步策略。
        /// </summary>
        PredictRollback = 2,

        /// <summary>
        /// 通过插值或外推消费的服务器权威实体状态快照。
        /// 已实现，见权威插值客户端同步策略。
        /// </summary>
        AuthoritativeInterpolation = 3,

        /// <summary>
        /// 以低频批次发布的服务器权威状态更新。
        /// 尚未实现，预留给未来的批量状态同步策略。
        /// </summary>
        BatchStateSync = 4,

        /// <summary>
        /// 带兴趣管理和 LOD 策略的大规模服务器权威同步。
        /// 尚未实现，预留给未来的大规模战斗 LOD 同步策略。
        /// </summary>
        MassBattleLodSync = 5,

        /// <summary>
        /// 混合同步策略，通常用于本地主角预测、远端插值以及普通单位批量同步。
        /// 尚未实现，预留给未来的主角预测混合同步策略。
        /// </summary>
        HybridHeroPrediction = 6,

        /// <summary>
        /// 基于定向快照的快速重连和状态恢复流程。
        /// 尚未实现，预留给未来的快速重连策略。
        /// </summary>
        FastReconnect = 7,

        /// <summary>
        /// 对服务器权威历史进行回溯，用于带延迟补偿的命中验证。
        /// 已由服务器回溯延迟补偿辅助组件实现。
        /// </summary>
        ServerRewindLagCompensation = 8
    }
}
