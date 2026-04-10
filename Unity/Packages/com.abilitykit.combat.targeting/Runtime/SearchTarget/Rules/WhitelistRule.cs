using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Rules
{
    public sealed class WhitelistRule : ITargetRule
    {
        private readonly IActorIdSet _set;

        public WhitelistRule(IActorIdSet set)
        {
            _set = set;
        }

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            return _set != null && _set.Contains(candidate.ActorId);
        }
    }
}
