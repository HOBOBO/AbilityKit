using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface ITargetScorer
    {
        float Score(in SearchQuery query, SearchContext context, EcsEntityId candidate);

        bool RequiresPosition { get; }
    }
}
