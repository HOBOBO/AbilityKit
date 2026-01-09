using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface IStreamingHitSelector
    {
        bool CanStream(in SearchQuery query);

        void Begin(in SearchQuery query, SearchContext context);

        void Offer(in SearchHit hit);

        void End(in SearchQuery query, SearchContext context, List<EcsEntityId> results);
    }
}
