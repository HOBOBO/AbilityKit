using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public sealed class DataSectorParamsResolver2D : ISectorParamResolver2D
    {
        private readonly int _radiusKey;
        private readonly int _halfAngleDegKey;

        public DataSectorParamsResolver2D(int radiusKey, int halfAngleDegKey)
        {
            _radiusKey = radiusKey;
            _halfAngleDegKey = halfAngleDegKey;
        }

        public bool RequiresPosition => false;

        public bool ResolveSectorParams(SearchContext context, out float radius, out float cosHalfAngle)
        {
            radius = 0f;
            cosHalfAngle = 0f;
            if (context == null) return false;

            if (!context.TryGetData<float>(_radiusKey, out radius)) return false;
            if (!context.TryGetData<float>(_halfAngleDegKey, out var halfDeg)) return false;

            if (radius < 0f) radius = -radius;

            cosHalfAngle = Mathf.Cos(Mathf.Deg2Rad * Mathf.Clamp(halfDeg, 0f, 180f));
            return radius > 0f;
        }
    }
}
