using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public sealed class CircleParamsConstantResolver2D : ICircleParamResolver2D
    {
        private readonly float _radius;

        public CircleParamsConstantResolver2D(float radius)
        {
            _radius = Mathf.Abs(radius);
        }

        public bool RequiresPosition => false;

        public bool ResolveCircleParams(SearchContext context, out float radius)
        {
            radius = _radius;
            return radius > 0f;
        }
    }
}
