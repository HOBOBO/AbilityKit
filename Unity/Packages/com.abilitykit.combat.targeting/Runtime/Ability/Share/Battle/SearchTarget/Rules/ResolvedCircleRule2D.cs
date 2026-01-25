using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Battle.SearchTarget.Shapes;
using UnityEngine;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Rules
{
    public sealed class ResolvedCircleRule2D : ITargetRule
    {
        private readonly IShapeFrameResolver2D _frame;
        private readonly ICircleParamResolver2D _params;

        public ResolvedCircleRule2D(IShapeFrameResolver2D frame, ICircleParamResolver2D @params)
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
            if (!_params.ResolveCircleParams(context, out var radius)) return false;

            var d = p - frame.Origin;
            return d.sqrMagnitude <= radius * radius;
        }
    }
}
