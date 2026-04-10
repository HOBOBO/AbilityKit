using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public sealed class SectorParamsConstantResolver2D : ISectorParamResolver2D
    {
        private readonly float _radius;
        private readonly float _cosHalfAngle;

        public SectorParamsConstantResolver2D(float radius, float halfAngleDegrees)
        {
            _radius = Mathf.Abs(radius);
            _cosHalfAngle = Mathf.Cos(Mathf.Deg2Rad * Mathf.Clamp(halfAngleDegrees, 0f, 180f));
        }

        public bool RequiresPosition => false;

        public bool ResolveSectorParams(SearchContext context, out float radius, out float cosHalfAngle)
        {
            radius = _radius;
            cosHalfAngle = _cosHalfAngle;
            return radius > 0f;
        }
    }
}
