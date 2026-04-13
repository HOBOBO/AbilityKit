using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS; using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public sealed class EntitasUnitFacadeMapper : ITargetMapper<IUnitFacade>
    {
        public bool TryMap(SearchContext context, EcsEntityId id, out IUnitFacade value)
        {
            if (!context.TryGetService<IUnitResolver>(out var resolver) || resolver == null)
            {
                value = null;
                return false;
            }

            return resolver.TryResolve(id, out value);
        }
    }
}
