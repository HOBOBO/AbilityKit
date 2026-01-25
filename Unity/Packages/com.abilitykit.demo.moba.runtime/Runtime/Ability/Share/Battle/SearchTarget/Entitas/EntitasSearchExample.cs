using System.Collections.Generic;
using AbilityKit.Ability.Share.Battle.SearchTarget.Providers;
using AbilityKit.Ability.Share.Battle.SearchTarget.Scorers;
using AbilityKit.Ability.Share.Battle.SearchTarget.Selectors;
using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Entitas
{
    public static class EntitasSearchExample
    {
        public static void SearchUnitsFromExplicitIds(
            IUnitResolver unitResolver,
            IReadOnlyList<EcsEntityId> ids,
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
}
