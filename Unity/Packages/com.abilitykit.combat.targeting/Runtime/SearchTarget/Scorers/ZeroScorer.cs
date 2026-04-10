using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Scorers
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
