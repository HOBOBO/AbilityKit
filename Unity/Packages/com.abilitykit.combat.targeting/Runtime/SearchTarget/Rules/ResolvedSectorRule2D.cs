using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Shapes;
using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Rules
{
    public sealed class ResolvedSectorRule2D : ITargetRule
    {
        private readonly IShapeFrameResolver2D _frame;
        private readonly ISectorParamResolver2D _params;

        public ResolvedSectorRule2D(IShapeFrameResolver2D frame, ISectorParamResolver2D @params)
        {
            _frame = frame;
            _params = @params;
        }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (_frame == null || _params == null) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPositionXZ(candidate, out var p)) return false;

            if (!_frame.ResolveFrame(context, out var frame)) return false;
            if (!_params.ResolveSectorParams(context, out var radius, out var cosHalfAngle)) return false;

            var rel = p - frame.Origin;
            var d2 = rel.sqrMagnitude;
            if (d2 > radius * radius) return false;

            var mag = rel.magnitude;
            if (mag <= 1e-6f) return true;

            var dir = rel / mag;
            var dot = Vector2.Dot(dir, frame.Forward);
            return dot >= cosHalfAngle;
        }
    }
}
