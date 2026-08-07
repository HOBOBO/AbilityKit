using System;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Demo.Moba.Services.Search
{
    /// <summary>
    /// MOBA 包外矩形目标规则。宽度和长度均为完整尺寸，形状中心位于 origin。
    /// </summary>
    internal sealed class MobaRectangleShapeRule : ITargetRule
    {
        private readonly Vec2 _origin;
        private readonly Vec2 _forward;
        private readonly Vec2 _right;
        private readonly float _halfWidth;
        private readonly float _halfLength;

        public MobaRectangleShapeRule(Vec2 origin, Vec2 forward, float width, float length)
        {
            _origin = origin;
            _forward = NormalizeOrUp(forward);
            _right = new Vec2(_forward.Y, -_forward.X);
            _halfWidth = width * 0.5f;
            _halfLength = length * 0.5f;
        }

        public bool IsMatch(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            if (_halfWidth <= 0f || _halfLength <= 0f) return false;
            var positions = context.PositionProvider;
            if (positions == null) return false;
            if (!positions.TryGetPosition(candidate, out var position)) return false;

            var relative = position.Subtract(_origin);
            var localForward = relative.Dot(_forward);
            if (Math.Abs(localForward) > _halfLength) return false;

            var localRight = relative.Dot(_right);
            return Math.Abs(localRight) <= _halfWidth;
        }

        private static Vec2 NormalizeOrUp(Vec2 value)
        {
            if (value.SqrMagnitude <= 0.000001f) return Vec2.Up;
            var inverseMagnitude = 1f / (float)Math.Sqrt(value.SqrMagnitude);
            return new Vec2(value.X * inverseMagnitude, value.Y * inverseMagnitude);
        }
    }

    /// <summary>
    /// MOBA 包外胶囊目标规则。length 表示两端圆心之间的中心线长度，radius 表示端帽半径。
    /// </summary>
    internal sealed class MobaCapsuleShapeRule : ITargetRule
    {
        private readonly Vec2 _origin;
        private readonly Vec2 _forward;
        private readonly float _halfSegmentLength;
        private readonly float _radius;
        private readonly float _radiusSqr;

        public MobaCapsuleShapeRule(Vec2 origin, Vec2 forward, float radius, float length)
        {
            _origin = origin;
            _forward = NormalizeOrUp(forward);
            _halfSegmentLength = length * 0.5f;
            _radius = radius;
            _radiusSqr = radius * radius;
        }

        public bool IsMatch(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            if (_radius <= 0f || _halfSegmentLength <= 0f) return false;
            var positions = context.PositionProvider;
            if (positions == null) return false;
            if (!positions.TryGetPosition(candidate, out var position)) return false;

            var relative = position.Subtract(_origin);
            var projected = relative.Dot(_forward);
            var clamped = Math.Max(-_halfSegmentLength, Math.Min(_halfSegmentLength, projected));
            var closestX = _origin.X + _forward.X * clamped;
            var closestY = _origin.Y + _forward.Y * clamped;
            var dx = position.X - closestX;
            var dy = position.Y - closestY;
            return dx * dx + dy * dy <= _radiusSqr;
        }

        private static Vec2 NormalizeOrUp(Vec2 value)
        {
            if (value.SqrMagnitude <= 0.000001f) return Vec2.Up;
            var inverseMagnitude = 1f / (float)Math.Sqrt(value.SqrMagnitude);
            return new Vec2(value.X * inverseMagnitude, value.Y * inverseMagnitude);
        }
    }
}
