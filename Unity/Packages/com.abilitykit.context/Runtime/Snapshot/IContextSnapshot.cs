namespace AbilityKit.Context
{
    /// <summary>
    /// 快照接口
    /// 用于持久化存储实体的瞬时状态
    /// </summary>
    public interface IContextSnapshot
    {
        /// <summary>
        /// 对应的实体 ID
        /// </summary>
        long EntityId { get; }

        /// <summary>
        /// 快照创建时间
        /// </summary>
        long CreatedAtMs { get; }
    }
}
