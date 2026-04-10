using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public interface IStreamingHitSelector
    {
        bool CanStream(in SearchQuery query);

        void Begin(in SearchQuery query, SearchContext context);

        void Offer(in SearchHit hit);

        void End(in SearchQuery query, SearchContext context, List<EcsEntityId> results);
    }
}
