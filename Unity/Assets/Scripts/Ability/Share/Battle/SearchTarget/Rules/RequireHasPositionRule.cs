using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Rules
{
    public sealed class RequireHasPositionRule : ITargetRule
    {
        public static readonly RequireHasPositionRule Instance = new RequireHasPositionRule();

        private RequireHasPositionRule() { }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            return pos.TryGetPositionXZ(candidate, out _);
        }
    }
}
