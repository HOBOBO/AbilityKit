using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Battle.SearchTarget;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Entitas
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
