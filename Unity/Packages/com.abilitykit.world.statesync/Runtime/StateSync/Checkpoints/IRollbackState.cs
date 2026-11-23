namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 本地逐实体检查点的状态适配契约，不声明网络格式。
    /// </summary>
    public interface IRollbackState
    {
        /// <summary>
        /// 业务定义的状态种类标识。
        /// </summary>
        int SnapshotKey { get; }

        /// <summary>
        /// 返回可长期保留的状态数据；允许转交独占数组，不要求每次重新分配。
        /// 提交后，生产者及持有引用的调用方不得再修改或复用其存储。
        /// </summary>
        byte[] Serialize();

        /// <summary>
        /// 从本地历史恢复状态对象；实现不得修改历史数据。
        /// </summary>
        void Deserialize(byte[] data);
    }
}
