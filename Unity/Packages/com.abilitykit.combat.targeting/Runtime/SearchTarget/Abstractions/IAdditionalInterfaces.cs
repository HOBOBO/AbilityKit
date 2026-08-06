namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 目标映射器接口
    /// </summary>
    public interface ITargetMapper<T>
    {
        bool TryMap(SearchContext context, EntityId id, out T result);
    }

    /// <summary>
    /// 实体句柄集合接口。
    /// </summary>
    public interface IEntityIdSet
    {
        bool Contains(EntityId id);
        int Count { get; }
    }

    /// <summary>
    /// 可选的实体等价键提供者。未提供时使用 <see cref="EntityId.Value"/>。
    /// </summary>
    public interface IEntityKeyProvider
    {
        ulong GetKey(EntityId id);
    }

    /// <summary>
    /// 搜索统计接口
    /// </summary>
    public interface ISearchStats
    {
        void Reset();
        void OnCandidate();
        void OnHit();
        void OnResult(int count);
    }
}
