using AbilityKit.Core.Mathematics;
using AbilityKit.Diagnostics.DebugDraw;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Diagnostics.Editor.DebugDraw
{
    internal sealed class HandlesDebugDraw : IDebugDraw
    {
        public void DrawWireSphere(in Vec3 center, float radius, in DebugDrawStyle style)
        {
            if (radius <= 0f) return;
            Handles.color = ToUnityColor(in style.Color);

            var value = ToUnity(in center);
            Handles.DrawWireDisc(value, Vector3.up, radius);
            Handles.DrawWireDisc(value, Vector3.right, radius);
            Handles.DrawWireDisc(value, Vector3.forward, radius);
        }

        public void DrawWireCapsule(in Vec3 a, in Vec3 b, float radius, in DebugDrawStyle style)
        {
            if (radius <= 0f) return;
            Handles.color = ToUnityColor(in style.Color);

            var start = ToUnity(in a);
            var end = ToUnity(in b);
            var axis = end - start;
            var length = axis.magnitude;
            if (length <= 0.0001f)
            {
                Handles.DrawWireDisc(start, Vector3.up, radius);
                Handles.DrawWireDisc(start, Vector3.right, radius);
                Handles.DrawWireDisc(start, Vector3.forward, radius);
                return;
            }

            var direction = axis / length;
            var up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            var right = Vector3.Cross(direction, up).normalized;
            up = Vector3.Cross(right, direction).normalized;

            Handles.DrawLine(start + right * radius, end + right * radius);
            Handles.DrawLine(start - right * radius, end - right * radius);
            Handles.DrawLine(start + up * radius, end + up * radius);
            Handles.DrawLine(start - up * radius, end - up * radius);
            Handles.DrawWireDisc(start, direction, radius);
            Handles.DrawWireDisc(end, direction, radius);
        }

        public void DrawWireAabb(in Vec3 center, in Vec3 size, in DebugDrawStyle style)
        {
            Handles.color = ToUnityColor(in style.Color);
            var value = ToUnity(in size);
            if (value.x == 0f && value.y == 0f && value.z == 0f) return;
            Handles.DrawWireCube(ToUnity(in center), value);
        }

        public void DrawLine(in Vec3 a, in Vec3 b, in DebugDrawStyle style)
        {
            Handles.color = ToUnityColor(in style.Color);
            Handles.DrawLine(ToUnity(in a), ToUnity(in b));
        }

        private static Vector3 ToUnity(in Vec3 value) => new Vector3(value.X, value.Y, value.Z);

        private static Color ToUnityColor(in DebugDrawColor color)
        {
            return new Color(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }
    }
}
