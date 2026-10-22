using AbilityKit.Core.Mathematics;
using AbilityKit.Diagnostics.DebugDraw;
using UnityEngine;

namespace AbilityKit.Game.Editor.Gizmos
{
    internal static class MobaGizmoGeometry
    {
        private const int CircleSegments = 48;

        public static void DrawGroundCircle(
            IDebugDraw draw,
            in Vec3 center,
            float radius,
            in DebugDrawStyle style)
        {
            if (draw == null || radius <= 0f) return;

            var previous = new Vec3(center.X + radius, center.Y, center.Z);
            for (var i = 1; i <= CircleSegments; i++)
            {
                var angle = i * (Mathf.PI * 2f / CircleSegments);
                var next = new Vec3(
                    center.X + Mathf.Cos(angle) * radius,
                    center.Y,
                    center.Z + Mathf.Sin(angle) * radius);
                draw.DrawLine(in previous, in next, in style);
                previous = next;
            }
        }
    }
}
