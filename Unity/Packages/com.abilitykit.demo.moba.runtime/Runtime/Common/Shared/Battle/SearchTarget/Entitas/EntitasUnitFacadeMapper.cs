using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Battle.SearchTarget;
using ST = AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Entitas
{
    public sealed class EntitasUnitFacadeMapper : ITargetMapper<IUnitFacade>
    {
        private readonly IUnitResolver _resolver;

        public EntitasUnitFacadeMapper(IUnitResolver resolver)
        {
            _resolver = resolver ?? throw new System.ArgumentNullException(nameof(resolver));
        }

        public bool TryMap(SearchContext context, ST.EntityId id, out IUnitFacade value)
        {
            if (!id.IsValid || id.Value > int.MaxValue)
            {
                value = null;
                return false;
            }

            var ecsId = new EcsEntityId((int)id.Value);
            return _resolver.TryResolve(ecsId, out value);
        }
    }
}
