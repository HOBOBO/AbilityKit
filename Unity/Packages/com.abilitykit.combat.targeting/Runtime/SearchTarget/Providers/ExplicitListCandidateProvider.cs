using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Providers
{
    public sealed class ExplicitListCandidateProvider : ICandidateProvider
    {
        private readonly IReadOnlyList<EcsEntityId> _ids;

        public ExplicitListCandidateProvider(IReadOnlyList<EcsEntityId> ids)
        {
            _ids = ids;
        }

        public bool RequiresPosition => false;

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_ids == null || _ids.Count == 0) return;

            for (int i = 0; i < _ids.Count; i++)
            {
                consumer.Consume(_ids[i]);
            }
        }
    }
}
