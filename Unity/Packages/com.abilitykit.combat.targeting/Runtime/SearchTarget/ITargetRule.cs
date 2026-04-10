using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget
{
    public interface ITargetRule
    {
        bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate);

        bool RequiresPosition { get; }
    }
}
