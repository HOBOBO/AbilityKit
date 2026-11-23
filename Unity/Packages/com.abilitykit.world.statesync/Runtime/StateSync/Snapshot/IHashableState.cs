namespace AbilityKit.Ability.StateSync.Snapshot
{
    /// <summary>
    /// 业务对象可实现的状态哈希接口；不会被框架自动发现或注册。
    /// </summary>
    public interface IHashableState
    {
        /// <summary>
        /// 返回业务状态的 64 位哈希；需要跨端比较时，由实现保证相同状态、顺序和配置产生相同值。
        /// </summary>
        ulong ComputeHash();
    }

    /// <summary>
    /// 为显式传入的业务状态集合提供聚合哈希，不负责快照捕获和恢复。
    /// </summary>
    public interface IBusinessHashProvider
    {
        /// <summary>
        /// 返回业务实体集合的组合哈希；遍历顺序、实体身份和字段覆盖范围由实现确定。
        /// </summary>
        ulong GetAllBusinessEntityHashes();
    }
}
