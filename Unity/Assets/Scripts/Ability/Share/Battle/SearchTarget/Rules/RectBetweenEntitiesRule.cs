using AbilityKit.Ability.Share.ECS;
using UnityEngine;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Rules
{
    public enum RectAnchorMode
    {
        Center,
        Start
    }

    public sealed class RectBetweenEntitiesRule : ITargetRule
    {
        private readonly EcsEntityId _source;
        private readonly EcsEntityId _target;
        private readonly float _halfWidth;
        private readonly float _lengthScale;
        private readonly float _lengthAdd;
        private readonly float _minLength;
        private readonly RectAnchorMode _anchor;

        public RectBetweenEntitiesRule(
            EcsEntityId source,
            EcsEntityId target,
            float width,
            float lengthScale = 1f,
            float lengthAdd = 0f,
            float minLength = 0.01f,
            RectAnchorMode anchor = RectAnchorMode.Start)
        {
            _source = source;
            _target = target;
            _halfWidth = Mathf.Abs(width) * 0.5f;
            _lengthScale = lengthScale;
            _lengthAdd = lengthAdd;
            _minLength = Mathf.Max(0f, minLength);
            _anchor = anchor;
        }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (_halfWidth <= 0f) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;

            if (!pos.TryGetPositionXZ(_source, out var sourcePos)) return false;
            if (!pos.TryGetPositionXZ(_target, out var targetPos)) return false;
            if (!pos.TryGetPositionXZ(candidate, out var p)) return false;

            var dir = targetPos - sourcePos;
            var dist = dir.magnitude;
            var length = Mathf.Max(_minLength, dist * _lengthScale + _lengthAdd);

            var forward = dist > 1e-6f ? (dir / dist) : Vector2.up;
            var right = new Vector2(forward.y, -forward.x);

            if (_anchor == RectAnchorMode.Center)
            {
                var halfLength = length * 0.5f;
                var origin = sourcePos + forward * halfLength;

                var rel = p - origin;
                var localX = Vector2.Dot(rel, right);
                var localY = Vector2.Dot(rel, forward);

                return Mathf.Abs(localX) <= _halfWidth && Mathf.Abs(localY) <= halfLength;
            }
            else
            {
                // Start anchored: rectangle spans [0, length] along forward from source.
                var rel = p - sourcePos;
                var localX = Vector2.Dot(rel, right);
                var localY = Vector2.Dot(rel, forward);

                return Mathf.Abs(localX) <= _halfWidth && localY >= 0f && localY <= length;
            }
        }
    }
}
