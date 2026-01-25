using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface ICandidateProvider
    {
        void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer;

        bool RequiresPosition { get; }
    }
}
