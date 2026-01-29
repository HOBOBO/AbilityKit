using AbilityKit.Ability.Share.ECS;
using UnityEngine;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Rules
{
    public sealed class CircleShapeRule : ITargetRule
    {
        private readonly Vector2 _originXZ;
        private readonly float _radius;
        private readonly float _radiusSqr;

        public CircleShapeRule(Vector2 originXZ, float radius)
        {
            _originXZ = originXZ;
            _radius = radius;
            _radiusSqr = radius * radius;
        }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (_radius <= 0f) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPositionXZ(candidate, out var p)) return false;

            var d = p - _originXZ;
            return d.sqrMagnitude <= _radiusSqr;
        }
    }
}
