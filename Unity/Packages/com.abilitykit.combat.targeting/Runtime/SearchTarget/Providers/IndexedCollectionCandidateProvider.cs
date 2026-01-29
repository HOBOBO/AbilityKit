using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Providers
{
    public sealed class IndexedCollectionCandidateProvider : ICandidateProvider
    {
        private readonly int _key;

        public IndexedCollectionCandidateProvider(int key)
        {
            _key = key;
        }

        public bool RequiresPosition => false;

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (!context.TryGetService<IEntityIdCollectionIndex>(out var index) || index == null) return;

            index.ForEach(_key, ref consumer);
        }
    }
}
