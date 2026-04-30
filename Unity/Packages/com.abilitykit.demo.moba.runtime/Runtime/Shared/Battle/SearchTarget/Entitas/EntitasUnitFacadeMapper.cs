using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Battle.SearchTarget;
using ST = AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public sealed class EntitasUnitFacadeMapper : ITargetMapper<IUnitFacade>
    {
        public bool TryMap(SearchContext context, ST.IEntityId id, out IUnitFacade value)
        {
            if (!context.TryGetService<IUnitResolver>(out var resolver) || resolver == null)
            {
                value = null;
                return false;
            }

            var ecsId = new EcsEntityId(id.ActorId);
            return resolver.TryResolve(ecsId, out value);
        }
    }
}
