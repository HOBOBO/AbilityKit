using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Shapes;
using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public sealed class RectLengthFromEntityDistanceResolver2D : IRectParamResolver2D
    {
        private readonly EcsEntityId _a;
        private readonly EcsEntityId _b;
        private readonly float _halfWidth;
        private readonly float _scale;
        private readonly float _add;
        private readonly float _minLength;
        private readonly float _maxLength;

        public RectLengthFromEntityDistanceResolver2D(
            EcsEntityId a,
            EcsEntityId b,
            float width,
            float scale = 1f,
            float add = 0f,
            float minLength = 0.01f,
            float maxLength = float.PositiveInfinity)
        {
            _a = a;
            _b = b;
            _halfWidth = Mathf.Abs(width) * 0.5f;
            _scale = scale;
            _add = add;
            _minLength = Mathf.Max(0f, minLength);
            _maxLength = maxLength;
        }

        public bool RequiresPosition => true;

        public bool ResolveRectParams(SearchContext context, out float halfWidth, out float halfLength)
        {
            halfWidth = 0f;
            halfLength = 0f;

            if (_halfWidth <= 0f) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPositionXZ(_a, out var pa)) return false;
            if (!pos.TryGetPositionXZ(_b, out var pb)) return false;

            var dist = (pb - pa).magnitude;
            var length = Mathf.Max(_minLength, dist * _scale + _add);
            if (!float.IsPositiveInfinity(_maxLength)) length = Mathf.Min(_maxLength, length);

            halfWidth = _halfWidth;
            halfLength = length * 0.5f;
            return halfLength > 0f;
        }
    }
}
