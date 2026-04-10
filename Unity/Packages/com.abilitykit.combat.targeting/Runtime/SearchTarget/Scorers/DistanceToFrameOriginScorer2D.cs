using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Shapes;

namespace AbilityKit.Battle.SearchTarget.Scorers
{
    public sealed class DistanceToFrameOriginScorer2D : ITargetScorer
    {
        private readonly IShapeFrameResolver2D _frame;

        public DistanceToFrameOriginScorer2D(IShapeFrameResolver2D frame)
        {
            _frame = frame;
        }

        public bool RequiresPosition => true;

        public float Score(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (_frame == null) return float.NegativeInfinity;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return float.NegativeInfinity;
            if (!pos.TryGetPositionXZ(candidate, out var p)) return float.NegativeInfinity;
            if (!_frame.ResolveFrame(context, out var frame)) return float.NegativeInfinity;

            // Higher score should be better. Use negative distance^2 for nearest-first.
            var d = p - frame.Origin;
            return -(d.x * d.x + d.y * d.y);
        }
    }
}
