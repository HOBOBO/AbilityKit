using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Providers
{
    public sealed class IndexedListCandidateProvider : ICandidateProvider
    {
        private readonly int _key;

        public IndexedListCandidateProvider(int key)
        {
            _key = key;
        }

        public bool RequiresPosition => false;

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (!context.TryGetService<IEntityIdIndex>(out var index) || index == null) return;
            if (!index.TryGetList(_key, out var ids) || ids == null || ids.Count == 0) return;

            for (int i = 0; i < ids.Count; i++)
            {
                consumer.Consume(ids[i]);
            }
        }
    }
}
