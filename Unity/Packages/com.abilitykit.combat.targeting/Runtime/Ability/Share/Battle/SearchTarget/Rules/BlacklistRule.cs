using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Rules
{
    public sealed class BlacklistRule : ITargetRule
    {
        private readonly IActorIdSet _set;

        public BlacklistRule(IActorIdSet set)
        {
            _set = set;
        }

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            return _set == null || !_set.Contains(candidate.ActorId);
        }
    }
}
