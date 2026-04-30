using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 流式命中选择器接口
    /// </summary>
    public interface IStreamingHitSelector : ITargetSelector
    {
        bool CanStream(in SearchQuery query);
        void Begin(in SearchQuery query, SearchContext context);
        void Offer(in SearchHit hit);
        void End(in SearchQuery query, SearchContext context, List<IEntityId> results);
    }

    /// <summary>
    /// 目标映射器接口
    /// </summary>
    public interface ITargetMapper<T>
    {
        bool TryMap(SearchContext context, IEntityId id, out T result);
    }

    /// <summary>
    /// Actor ID 集合接口
    /// </summary>
    public interface IActorIdSet
    {
        bool Contains(int actorId);
        int Count { get; }
    }

    /// <summary>
    /// 实体 Key 提供者接口
    /// </summary>
    public interface IEntityKeyProvider
    {
        ulong GetKey(IEntityId id);
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
