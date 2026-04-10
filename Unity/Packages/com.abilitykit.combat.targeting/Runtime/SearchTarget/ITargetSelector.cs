using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public interface ITargetSelector
    {
        void Select(in SearchQuery query, SearchContext context, List<SearchHit> hits, List<EcsEntityId> results);

        bool RequiresPosition { get; }
    }
}
