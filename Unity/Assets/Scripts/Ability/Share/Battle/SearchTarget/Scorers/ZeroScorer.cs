using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Scorers
{
    public sealed class ZeroScorer : ITargetScorer
    {
        public bool RequiresPosition => false;

        public float Score(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            return 0f;
        }
    }
}
