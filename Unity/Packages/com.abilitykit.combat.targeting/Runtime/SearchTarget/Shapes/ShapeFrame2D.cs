using UnityEngine;

namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public readonly struct ShapeFrame2D
    {
        public readonly Vector2 Origin;
        public readonly Vector2 Forward;
        public readonly Vector2 Right;

        public ShapeFrame2D(Vector2 origin, Vector2 forward)
        {
            Origin = origin;
            if (forward.sqrMagnitude > 0f)
            {
                Forward = forward.normalized;
            }
            else
            {
                Forward = Vector2.up;
            }

            Right = new Vector2(Forward.y, -Forward.x);
        }
    }
}
