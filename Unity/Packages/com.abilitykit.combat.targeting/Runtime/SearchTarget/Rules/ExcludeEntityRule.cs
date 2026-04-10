using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Rules
{
    public sealed class ExcludeEntityRule : ITargetRule
    {
        private readonly EcsEntityId _excluded;

        public ExcludeEntityRule(EcsEntityId excluded)
        {
            _excluded = excluded;
        }

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            return candidate.ActorId != _excluded.ActorId;
        }
    }
}
