using AbilityKit.Ability.Share.Common.DebugDraw;
using AbilityKit.Ability.Share.Math;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.DebugDraw
{
    internal sealed class HandlesDebugDraw : IDebugDraw
    {
        public void DrawWireSphere(in Vec3 center, float radius, in DebugDrawStyle style)
        {
            if (radius <= 0f) return;
            Handles.color = ToUnityColor(in style.Color);

            var c = ToUnity(in center);
            Handles.DrawWireDisc(c, Vector3.up, radius);
            Handles.DrawWireDisc(c, Vector3.right, radius);
            Handles.DrawWireDisc(c, Vector3.forward, radius);
        }

        public void DrawWireCapsule(in Vec3 a, in Vec3 b, float radius, in DebugDrawStyle style)
        {
            if (radius <= 0f) return;
            Handles.color = ToUnityColor(in style.Color);

            var pa = ToUnity(in a);
            var pb = ToUnity(in b);

            var axis = pb - pa;
            var len = axis.magnitude;
            if (len <= 0.0001f)
            {
                Handles.DrawWireDisc(pa, Vector3.up, radius);
                Handles.DrawWireDisc(pa, Vector3.right, radius);
                Handles.DrawWireDisc(pa, Vector3.forward, radius);
                return;
            }

            var dir = axis / len;
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            var right = Vector3.Cross(dir, up).normalized;
            up = Vector3.Cross(right, dir).normalized;

            Handles.DrawLine(pa + right * radius, pb + right * radius);
            Handles.DrawLine(pa - right * radius, pb - right * radius);
            Handles.DrawLine(pa + up * radius, pb + up * radius);
            Handles.DrawLine(pa - up * radius, pb - up * radius);

            Handles.DrawWireDisc(pa, dir, radius);
            Handles.DrawWireDisc(pb, dir, radius);
        }

        public void DrawWireAabb(in Vec3 center, in Vec3 size, in DebugDrawStyle style)
        {
            Handles.color = ToUnityColor(in style.Color);

            var c = ToUnity(in center);
            var s = ToUnity(in size);
            if (s.x == 0f && s.y == 0f && s.z == 0f) return;
            Handles.DrawWireCube(c, s);
        }

        public void DrawLine(in Vec3 a, in Vec3 b, in DebugDrawStyle style)
        {
            Handles.color = ToUnityColor(in style.Color);
            Handles.DrawLine(ToUnity(in a), ToUnity(in b));
        }

        private static Vector3 ToUnity(in Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        private static Color ToUnityColor(in DebugDrawColor c)
        {
            return new Color(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        }
    }
}
