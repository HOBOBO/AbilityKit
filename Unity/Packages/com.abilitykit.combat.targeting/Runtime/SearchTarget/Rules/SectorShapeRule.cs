using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;
using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Rules
{
    public sealed class SectorShapeRule : ITargetRule
    {
        private readonly Vector2 _originXZ;
        private readonly Vector2 _forwardXZ;
        private readonly float _radius;
        private readonly float _radiusSqr;
        private readonly float _cosHalfAngle;

        public SectorShapeRule(Vector2 originXZ, Vector2 forwardXZ, float radius, float halfAngleDegrees)
        {
            _originXZ = originXZ;
            _radius = radius;
            _radiusSqr = radius * radius;

            if (forwardXZ.sqrMagnitude > 0f)
            {
                _forwardXZ = forwardXZ.normalized;
            }
            else
            {
                _forwardXZ = Vector2.up;
            }

            _cosHalfAngle = Mathf.Cos(Mathf.Deg2Rad * Mathf.Clamp(halfAngleDegrees, 0f, 180f));
        }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (_radius <= 0f) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPositionXZ(candidate, out var p)) return false;

            var rel = p - _originXZ;
            var d2 = rel.sqrMagnitude;
            if (d2 > _radiusSqr) return false;

            var mag = rel.magnitude;
            if (mag <= 1e-6f) return true;

            var dir = rel / mag;
            var dot = Vector2.Dot(dir, _forwardXZ);
            return dot >= _cosHalfAngle;
        }
    }
}
