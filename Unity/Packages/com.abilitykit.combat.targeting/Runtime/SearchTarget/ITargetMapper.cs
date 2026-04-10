using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public interface ITargetMapper<T>
    {
        bool TryMap(SearchContext context, EcsEntityId id, out T value);
    }
}
