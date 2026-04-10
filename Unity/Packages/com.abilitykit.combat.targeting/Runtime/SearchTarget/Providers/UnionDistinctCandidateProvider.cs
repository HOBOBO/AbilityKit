using System.Collections.Generic;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Providers
{
    public sealed class UnionDistinctCandidateProvider : ICandidateProvider
    {
        private readonly IReadOnlyList<ICandidateProvider> _providers;

        public UnionDistinctCandidateProvider(IReadOnlyList<ICandidateProvider> providers)
        {
            _providers = providers;
        }

        public bool RequiresPosition
        {
            get
            {
                if (_providers == null) return false;
                for (int i = 0; i < _providers.Count; i++)
                {
                    var p = _providers[i];
                    if (p != null && p.RequiresPosition) return true;
                }
                return false;
            }
        }

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_providers == null || _providers.Count == 0) return;

            if (!context.TryGetService<IVisitedSet>(out var visited) || visited == null)
            {
                for (int i = 0; i < _providers.Count; i++)
                {
                    var p = _providers[i];
                    if (p == null) continue;
                    p.ForEachCandidate(in query, context, ref consumer);
                }
                return;
            }

            visited.Next();

            for (int i = 0; i < _providers.Count; i++)
            {
                var p = _providers[i];
                if (p == null) continue;

                var inner = new DedupForwardConsumer<TConsumer>(consumer, visited);
                p.ForEachCandidate(in query, context, ref inner);
            }
        }

        private struct DedupForwardConsumer<TConsumer> : ICandidateConsumer
            where TConsumer : struct, ICandidateConsumer
        {
            private readonly TConsumer _consumer;
            private readonly IVisitedSet _visited;

            public DedupForwardConsumer(TConsumer consumer, IVisitedSet visited)
            {
                _consumer = consumer;
                _visited = visited;
            }

            public void Consume(AbilityKit.Ability.Share.ECS.EcsEntityId id)
            {
                if (_visited != null && _visited.Mark(id.ActorId))
                {
                    _consumer.Consume(id);
                }
            }
        }
    }
}
