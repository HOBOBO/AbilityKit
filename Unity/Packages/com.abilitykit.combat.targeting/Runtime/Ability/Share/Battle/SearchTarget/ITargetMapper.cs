using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface ITargetMapper<T>
    {
        bool TryMap(SearchContext context, EcsEntityId id, out T value);
    }
}
