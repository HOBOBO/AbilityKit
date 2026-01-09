using AbilityKit.Ability.Share.ECS;
using UnityEngine;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Shapes
{
    public sealed class CircleRadiusFromEntityDistanceResolver2D : ICircleParamResolver2D
    {
        private readonly EcsEntityId _a;
        private readonly EcsEntityId _b;
        private readonly float _scale;
        private readonly float _add;
        private readonly float _min;
        private readonly float _max;

        public CircleRadiusFromEntityDistanceResolver2D(
            EcsEntityId a,
            EcsEntityId b,
            float scale = 1f,
            float add = 0f,
            float minRadius = 0f,
            float maxRadius = float.PositiveInfinity)
        {
            _a = a;
            _b = b;
            _scale = scale;
            _add = add;
            _min = Mathf.Max(0f, minRadius);
            _max = maxRadius;
        }

        public bool RequiresPosition => true;

        public bool ResolveCircleParams(SearchContext context, out float radius)
        {
            radius = 0f;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPositionXZ(_a, out var pa)) return false;
            if (!pos.TryGetPositionXZ(_b, out var pb)) return false;

            var dist = (pb - pa).magnitude;
            var r = dist * _scale + _add;
            r = Mathf.Max(_min, r);
            if (!float.IsPositiveInfinity(_max)) r = Mathf.Min(_max, r);

            radius = r;
            return radius > 0f;
        }
    }
}
