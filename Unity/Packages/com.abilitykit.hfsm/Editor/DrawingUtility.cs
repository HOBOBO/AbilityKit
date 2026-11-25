using UnityEngine;

namespace AbilityKit.HFSM.Editor
{
    /// <summary>
    /// 提供 IMGUI 绘制辅助方法。
    /// </summary>
    public static class DrawingUtility
    {
        /// <summary>
        /// 根据平移和缩放，将屏幕坐标转换为内容坐标。
        /// </summary>
        public static Vector2 ScreenToContent(Vector2 screenPosition, Vector2 panOffset, float zoomFactor)
        {
            return (screenPosition - panOffset) / zoomFactor;
        }

        /// <summary>
        /// 根据平移和缩放，将内容坐标转换为屏幕坐标。
        /// </summary>
        public static Vector2 ContentToScreen(Vector2 contentPosition, Vector2 panOffset, float zoomFactor)
        {
            return contentPosition * zoomFactor + panOffset;
        }

        /// <summary>
        /// 获取节点中心的内容坐标。
        /// </summary>
        public static Vector2 GetNodeCenter(Graph.NodeBase node)
        {
            return node.Position + node.Size * 0.5f;
        }

        /// <summary>
        /// 计算转换线偏移，避免重叠。
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
