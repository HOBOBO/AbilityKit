using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Rules
{
    public sealed class RequireValidIdRule : ITargetRule
    {
        public static readonly RequireValidIdRule Instance = new RequireValidIdRule();

        public RequireValidIdRule() { }

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            return candidate.IsValid;
        }
    }
}
