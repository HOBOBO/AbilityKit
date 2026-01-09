using AbilityKit.Ability.Share.ECS;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Scorers
{
    public sealed class DistanceToEntityScorer2D : ITargetScorer
    {
        private readonly EcsEntityId _source;

        public DistanceToEntityScorer2D(EcsEntityId source)
        {
            _source = source;
        }

        public bool RequiresPosition => true;

        public float Score(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return float.NegativeInfinity;
            if (!pos.TryGetPositionXZ(_source, out var src)) return float.NegativeInfinity;
            if (!pos.TryGetPositionXZ(candidate, out var p)) return float.NegativeInfinity;

            // Higher score should be better. Use negative distance^2 for nearest-first.
            var dx = p.x - src.x;
            var dy = p.y - src.y;
            return -(dx * dx + dy * dy);
        }
    }
}
