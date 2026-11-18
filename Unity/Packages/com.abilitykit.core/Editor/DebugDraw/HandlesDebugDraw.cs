using AbilityKit.Core.Debugging;
using AbilityKit.Core.Mathematics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Core.Editor.Debugging
{
    /// <summary>
    /// 通过 Unity Handles 同步执行旧世界坐标绘制请求的内部适配器。
    /// 方法修改共享 Handles.color，不单独恢复；旧驱动在每个内容提供方外围建立绘制作用域。
    /// </summary>
    internal sealed class HandlesDebugDraw : IDebugDraw
    {
        /// <summary>
        /// 半径非正时忽略，否则以三个正交圆盘近似球体线框。
        /// 不验证非有限坐标或半径，方法本身不恢复颜色。
        /// </summary>
        /// <param name="center">世界坐标球心。</param>
        /// <param name="radius">世界单位半径。</param>
        /// <param name="style">按字节通道归一化的颜色。</param>
        public void DrawWireSphere(in Vec3 center, float radius, in DebugDrawStyle style)
        {
            if (radius <= 0f) return;
            Handles.color = ToUnityColor(in style.Color);

            var c = ToUnity(in center);
            Handles.DrawWireDisc(c, Vector3.up, radius);
            Handles.DrawWireDisc(c, Vector3.right, radius);
            Handles.DrawWireDisc(c, Vector3.forward, radius);
        }

        /// <summary>
        /// 半径非正时忽略；轴长不超过零点零零零一时，在首端绘制三个圆盘。
        /// 其他情况绘制四条平行边与两个端面圆盘，不补半球弧线；不验证非有限输入。
        /// </summary>
        /// <param name="a">第一世界坐标端点。</param>
        /// <param name="b">第二世界坐标端点。</param>
        /// <param name="radius">世界单位半径。</param>
        /// <param name="style">颜色样式。</param>
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

        /// <summary>
        /// 先写入颜色，尺寸三个分量全为零时不绘制，否则将完整边长传给 DrawWireCube。
        /// 不拒绝负尺寸或非有限值，也不在此恢复颜色。
        /// </summary>
        /// <param name="center">世界坐标中心。</param>
        /// <param name="size">各轴完整边长，不是半尺寸。</param>
        /// <param name="style">颜色样式。</param>
        public void DrawWireAabb(in Vec3 center, in Vec3 size, in DebugDrawStyle style)
        {
            Handles.color = ToUnityColor(in style.Color);

            var c = ToUnity(in center);
            var s = ToUnity(in size);
            if (s.x == 0f && s.y == 0f && s.z == 0f) return;
            Handles.DrawWireCube(c, s);
        }

        /// <summary>
        /// 写入颜色并直接绘制世界坐标线段，不过滤重合端点或非有限输入。
        /// </summary>
        /// <param name="a">世界坐标起点。</param>
        /// <param name="b">世界坐标终点。</param>
        /// <param name="style">颜色样式。</param>
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
