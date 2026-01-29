using AbilityKit.Ability.Share.ECS;
using UnityEngine;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Rules
{
    public sealed class OrientedRectShapeRule : ITargetRule
    {
        private readonly Vector2 _originXZ;
        private readonly Vector2 _forwardXZ;
        private readonly Vector2 _rightXZ;
        private readonly Vector2 _halfExtents;

        public OrientedRectShapeRule(Vector2 originXZ, Vector2 forwardXZ, Vector2 halfExtents)
        {
            _originXZ = originXZ;
            _halfExtents = new Vector2(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y));

            if (forwardXZ.sqrMagnitude > 0f)
            {
                _forwardXZ = forwardXZ.normalized;
            }
            else
            {
                _forwardXZ = Vector2.up;
            }

            _rightXZ = new Vector2(_forwardXZ.y, -_forwardXZ.x);
        }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (_halfExtents.x <= 0f || _halfExtents.y <= 0f) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPositionXZ(candidate, out var p)) return false;

            var rel = p - _originXZ;
            var localX = Vector2.Dot(rel, _rightXZ);
            var localY = Vector2.Dot(rel, _forwardXZ);

            return Mathf.Abs(localX) <= _halfExtents.x && Mathf.Abs(localY) <= _halfExtents.y;
        }
    }
}
