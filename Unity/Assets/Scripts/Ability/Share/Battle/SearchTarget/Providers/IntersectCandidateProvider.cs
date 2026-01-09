using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Providers
{
    public sealed class IntersectCandidateProvider : ICandidateProvider
    {
        private readonly ICandidateProvider _a;
        private readonly ICandidateProvider _b;

        public IntersectCandidateProvider(ICandidateProvider a, ICandidateProvider b)
        {
            _a = a;
            _b = b;
        }

        public bool RequiresPosition => (_a != null && _a.RequiresPosition) || (_b != null && _b.RequiresPosition);

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_a == null || _b == null) return;

            if (!context.TryGetService<IVisitedSet>(out var visited) || visited == null)
            {
                // Fallback: no visited set, run only A.
                _a.ForEachCandidate(in query, context, ref consumer);
                return;
            }

            visited.Next();

            var mark = new MarkConsumer(visited);
            _b.ForEachCandidate(in query, context, ref mark);

            var filter = new IntersectForwardConsumer<TConsumer>(consumer, visited);
            _a.ForEachCandidate(in query, context, ref filter);
        }

        private struct MarkConsumer : ICandidateConsumer
        {
            private readonly IVisitedSet _visited;

            public MarkConsumer(IVisitedSet visited)
            {
                _visited = visited;
            }

            public void Consume(EcsEntityId id)
            {
                _visited?.Mark(id.ActorId);
            }
        }

        private struct IntersectForwardConsumer<TConsumer> : ICandidateConsumer
            where TConsumer : struct, ICandidateConsumer
        {
            private readonly TConsumer _consumer;
            private readonly IVisitedSet _visited;

            public IntersectForwardConsumer(TConsumer consumer, IVisitedSet visited)
            {
                _consumer = consumer;
                _visited = visited;
            }

            public void Consume(EcsEntityId id)
            {
                if (_visited != null && _visited.IsMarked(id.ActorId))
                {
                    _consumer.Consume(id);
                }
            }
        }
    }
}
