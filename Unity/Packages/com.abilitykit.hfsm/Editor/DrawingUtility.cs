using UnityEngine;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Utility methods for IMGUI drawing operations.
    /// </summary>
    public static class DrawingUtility
    {
        /// <summary>
        /// Converts screen position to content position based on pan and zoom.
        /// </summary>
        public static Vector2 ScreenToContent(Vector2 screenPosition, Vector2 panOffset, float zoomFactor)
        {
            return (screenPosition - panOffset) / zoomFactor;
        }

        /// <summary>
        /// Converts content position to screen position based on pan and zoom.
        /// </summary>
        public static Vector2 ContentToScreen(Vector2 contentPosition, Vector2 panOffset, float zoomFactor)
        {
            return contentPosition * zoomFactor + panOffset;
        }

        /// <summary>
        /// Gets the center point of a node in content coordinates.
        /// </summary>
        public static Vector2 GetNodeCenter(Graph.HfsmNodeBase node)
        {
            return node.Position + node.Size * 0.5f;
        }

        /// <summary>
        /// Calculates transition line offset to avoid overlapping.
        /// </summary>
        public static Vector2 CalculateTransitionOffset(Vector2 from, Vector2 to, float offsetAmount = 15f)
        {
            Vector2 direction = to - from;
            Vector2 offset = Vector2.zero;

            float absX = Mathf.Abs(direction.x);
            float absY = Mathf.Abs(direction.y);

            if (absY > absX)
            {
                offset.x = direction.y < 0 ? offsetAmount : -offsetAmount;
            }
            else
            {
                offset.y = direction.x < 0 ? offsetAmount : -offsetAmount;
            }

            return offset;
        }
    }
}
