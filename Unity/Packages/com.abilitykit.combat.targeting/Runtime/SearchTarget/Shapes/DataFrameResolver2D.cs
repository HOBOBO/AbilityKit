using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public sealed class DataFrameResolver2D : IShapeFrameResolver2D
    {
        private readonly int _originKey;
        private readonly int _forwardKey;
        private readonly Vector2 _defaultForward;

        public DataFrameResolver2D(int originKey, int forwardKey = 0, Vector2 defaultForward = default)
        {
            _originKey = originKey;
            _forwardKey = forwardKey;
            _defaultForward = defaultForward == default ? Vector2.up : defaultForward;
        }

        public bool RequiresPosition => false;

        public bool ResolveFrame(SearchContext context, out ShapeFrame2D frame)
        {
            frame = default;
            if (context == null) return false;

            if (!context.TryGetData<Vector2>(_originKey, out var origin)) return false;

            Vector2 forward;
            if (_forwardKey != 0 && context.TryGetData<Vector2>(_forwardKey, out var f))
            {
                forward = f;
            }
            else
            {
                forward = _defaultForward;
            }

            frame = new ShapeFrame2D(origin, forward);
            return true;
        }
    }
}
