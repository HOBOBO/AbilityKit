using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public sealed class RectParamsConstantResolver2D : IRectParamResolver2D
    {
        private readonly float _halfWidth;
        private readonly float _halfLength;

        public RectParamsConstantResolver2D(float width, float length)
        {
            _halfWidth = Mathf.Abs(width) * 0.5f;
            _halfLength = Mathf.Abs(length) * 0.5f;
        }

        public bool RequiresPosition => false;

        public bool ResolveRectParams(SearchContext context, out float halfWidth, out float halfLength)
        {
            halfWidth = _halfWidth;
            halfLength = _halfLength;
            return halfWidth > 0f && halfLength > 0f;
        }
    }
}
