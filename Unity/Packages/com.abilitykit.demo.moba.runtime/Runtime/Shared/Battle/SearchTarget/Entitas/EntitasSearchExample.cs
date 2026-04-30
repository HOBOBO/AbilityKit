using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;
using AbilityKit.ECS;
using ST = AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public static class EntitasSearchExample
    {
        public static void SearchUnitsFromExplicitIds(
            IUnitResolver unitResolver,
            IReadOnlyList<ST.EntityId> ids,
            List<IUnitFacade> results)
        {
            var ctx = new SearchContext();
            ctx.SetService<IUnitResolver>(unitResolver);
            ctx.SetService<IEntityKeyProvider>(new EntitasActorIdKeyProvider());

            var query = new SearchQuery(
                new ExplicitListCandidateProvider(ids),
                rules: null,
                scorer: new ZeroScorer(),
                selector: new TopKByScoreSelector(),
                maxCount: 0);

            var engine = new TargetSearchEngine();
            engine.Search(query, ctx, results, new EntitasUnitFacadeMapper());
        }
    }

    public sealed class ExplicitListCandidateProvider : ICandidateProvider
    {
        private readonly IReadOnlyList<ST.EntityId> _ids;

        public ExplicitListCandidateProvider(IReadOnlyList<ST.EntityId> ids)
        {
            _ids = ids;
        }

        public bool RequiresPosition => false;

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_ids == null) return;
            for (int i = 0; i < _ids.Count; i++)
            {
                consumer.Consume(_ids[i]);
            }
        }
    }
}
