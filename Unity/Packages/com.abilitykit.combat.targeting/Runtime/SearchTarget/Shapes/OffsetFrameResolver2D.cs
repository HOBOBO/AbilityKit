using UnityEngine;

namespace AbilityKit.Ability.Share.Battle.SearchTarget.Shapes
{
    public sealed class OffsetFrameResolver2D : IShapeFrameResolver2D
    {
        private readonly IShapeFrameResolver2D _inner;
        private readonly Vector2 _offsetLocal;

        public OffsetFrameResolver2D(IShapeFrameResolver2D inner, Vector2 offsetLocal)
        {
            _inner = inner;
            _offsetLocal = offsetLocal;
        }

        public bool RequiresPosition => _inner != null && _inner.RequiresPosition;

        public bool ResolveFrame(SearchContext context, out ShapeFrame2D frame)
        {
            frame = default;
            if (_inner == null) return false;
            if (!_inner.ResolveFrame(context, out var f)) return false;

            // Local offset: (x=right, y=forward)
            var origin = f.Origin + f.Right * _offsetLocal.x + f.Forward * _offsetLocal.y;
            frame = new ShapeFrame2D(origin, f.Forward);
            return true;
        }
    }
}
