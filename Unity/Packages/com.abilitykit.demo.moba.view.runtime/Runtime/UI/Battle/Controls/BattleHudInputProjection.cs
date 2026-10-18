using UnityEngine;

namespace AbilityKit.Game.Battle.View
{
    internal static class BattleHudInputProjection
    {
        public static Vector2 ToCameraPlane(Vector2 input, Transform cameraTransform)
        {
            if (cameraTransform == null) return input;

            var forward = cameraTransform.forward;
            forward.y = 0f;
            var forwardLen = forward.magnitude;
            if (forwardLen > 0.0001f) forward /= forwardLen;

            var right = cameraTransform.right;
            right.y = 0f;
            var rightLen = right.magnitude;
            if (rightLen > 0.0001f) right /= rightLen;

            var world = right * input.x + forward * input.y;
            return new Vector2(world.x, world.z);
        }
    }
}
