using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 目标选择器接口
    /// </summary>
    public interface ITargetSelector
    {
        void Select(in SearchQuery query, SearchContext context, List<SearchHit> hits, List<IEntityId> results);
        bool RequiresPosition { get; }
    }
}
