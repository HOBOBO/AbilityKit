namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 由业务实体适配器实现的本地捕获与恢复入口；不负责实体重建。
    /// </summary>
    public interface IRollbackable
    {
        /// <summary>
        /// 历史有效期间稳定的业务标识；宿主不得在同一历史窗口内复用为其他实体。
        /// </summary>
        long EntityId { get; }

        /// <summary>
        /// 业务定义的状态种类标识；本地管理器不以此验证实体代际。
        /// </summary>
        int SnapshotKey { get; }

        /// <summary>
        /// 创建可序列化当前状态并可接收历史值的状态对象；空值表示跳过该实体。
        /// </summary>
        IRollbackState CreateRollbackState();

        /// <summary>
        /// 把已还原的历史值应用到当前存活实体。
        /// </summary>
        void RestoreFromRollbackState(IRollbackState state);
    }
}
