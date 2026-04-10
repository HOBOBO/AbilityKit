using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Providers
{
    public sealed class ExceptCandidateProvider : ICandidateProvider
    {
        private readonly ICandidateProvider _source;
        private readonly ICandidateProvider _excluded;

        public ExceptCandidateProvider(ICandidateProvider source, ICandidateProvider excluded)
        {
            _source = source;
            _excluded = excluded;
        }

        public bool RequiresPosition => (_source != null && _source.RequiresPosition) || (_excluded != null && _excluded.RequiresPosition);

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_source == null) return;

            if (!context.TryGetService<IVisitedSet>(out var visited) || visited == null)
            {
                _source.ForEachCandidate(in query, context, ref consumer);
                return;
            }

            visited.Next();

            if (_excluded != null)
            {
                var mark = new MarkConsumer(visited);
                _excluded.ForEachCandidate(in query, context, ref mark);
            }

            var filter = new ExceptForwardConsumer<TConsumer>(consumer, visited);
            _source.ForEachCandidate(in query, context, ref filter);
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

        private struct ExceptForwardConsumer<TConsumer> : ICandidateConsumer
            where TConsumer : struct, ICandidateConsumer
        {
            private readonly TConsumer _consumer;
            private readonly IVisitedSet _visited;

            public ExceptForwardConsumer(TConsumer consumer, IVisitedSet visited)
            {
                _consumer = consumer;
                _visited = visited;
            }

            public void Consume(EcsEntityId id)
            {
                if (_visited != null && _visited.IsMarked(id.ActorId)) return;
                _consumer.Consume(id);
            }
        }
    }
}
