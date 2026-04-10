using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Shapes;
using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Rules
{
    public enum RectPivotMode
    {
        Center,
        Start
    }

    public sealed class ResolvedOrientedRectRule2D : ITargetRule
    {
        private readonly IShapeFrameResolver2D _frame;
        private readonly IRectParamResolver2D _params;
        private readonly RectPivotMode _pivot;

        public ResolvedOrientedRectRule2D(IShapeFrameResolver2D frame, IRectParamResolver2D @params, RectPivotMode pivot)
        {
            _frame = frame;
            _params = @params;
            _pivot = pivot;
        }

        public bool RequiresPosition => true;

        public bool FrameRequiresPosition => _frame != null && _frame.RequiresPosition;
        public bool ParamsRequiresPosition => _params != null && _params.RequiresPosition;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (_frame == null || _params == null) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPositionXZ(candidate, out var p)) return false;

            if (!_frame.ResolveFrame(context, out var frame)) return false;
            if (!_params.ResolveRectParams(context, out var halfWidth, out var halfLength)) return false;

            Vector2 rel;
            if (_pivot == RectPivotMode.Center)
            {
                rel = p - frame.Origin;
                var localX = Vector2.Dot(rel, frame.Right);
                var localY = Vector2.Dot(rel, frame.Forward);
                return Mathf.Abs(localX) <= halfWidth && Mathf.Abs(localY) <= halfLength;
            }

            // Start pivot: frame.Origin is the start point.
            rel = p - frame.Origin;
            var x = Vector2.Dot(rel, frame.Right);
            var y = Vector2.Dot(rel, frame.Forward);
            return Mathf.Abs(x) <= halfWidth && y >= 0f && y <= halfLength * 2f;
        }
    }
}
