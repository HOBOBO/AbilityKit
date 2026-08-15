using System;
using AbilityKit.Deterministic;

namespace AbilityKit.Core.Mathematics
{
    public static class CollisionQueries
    {
        public static bool Raycast(in Ray3 ray, in Sphere sphere, out float distance, out Vec3 normal)
        {
            // 求解 |o + t d - c|^2 = r^2。判别式开方与法线归一化走定点内核
            // （DeterministicMathBridge），保证命中判定跨平台位一致。
            var o = DeterministicMathBridge.ToFixed(ray.Origin);
            var d = DeterministicMathBridge.ToFixed(ray.Direction);
            var c = DeterministicMathBridge.ToFixed(sphere.Center);
            var r = Fixed64.FromSingle(sphere.Radius);

            var oc = o - c;
            var a = FixedVec3.Dot(d, d);
            var b = Fixed64.FromInt64(2) * FixedVec3.Dot(oc, d);
            var cc = FixedVec3.Dot(oc, oc) - r * r;
            var discriminant = (b * b) - (Fixed64.FromInt64(4) * a * cc);

            if (discriminant < Fixed64.Zero)
            {
                distance = 0f;
                normal = Vec3.Zero;
                return false;
            }

            var sqrt = DeterministicMath.Sqrt(discriminant);
            var inv2a = Fixed64.FromInt64(2) * a;
            var t0 = (-b - sqrt) / inv2a;
            var t1 = (-b + sqrt) / inv2a;

            var t = t0;
            if (t < Fixed64.Zero) t = t1;
            if (t < Fixed64.Zero)
            {
                distance = 0f;
                normal = Vec3.Zero;
                return false;
            }

            distance = t.ToSingle();
            var hitDelta = (o + (d * t)) - c;
            var len = DeterministicMath.Sqrt(hitDelta.SqrMagnitude);
            normal = len > DeterministicMathBridge.Epsilon
                ? DeterministicMathBridge.ToVec3(hitDelta / len)
                : Vec3.Zero;
            return true;
        }

        public static bool Raycast(in Ray3 ray, in Aabb aabb, out float distance, out Vec3 normal)
        {
            // 分层裁剪方法。
            var tmin = float.NegativeInfinity;
            var tmax = float.PositiveInfinity;
            normal = Vec3.Zero;

            if (!Slab(ray.Origin.X, ray.Direction.X, aabb.Min.X, aabb.Max.X, ref tmin, ref tmax, new Vec3(-1f, 0f, 0f), new Vec3(1f, 0f, 0f), ref normal))
            {
                distance = 0f;
                return false;
            }

            if (!Slab(ray.Origin.Y, ray.Direction.Y, aabb.Min.Y, aabb.Max.Y, ref tmin, ref tmax, new Vec3(0f, -1f, 0f), new Vec3(0f, 1f, 0f), ref normal))
            {
                distance = 0f;
                return false;
            }

            if (!Slab(ray.Origin.Z, ray.Direction.Z, aabb.Min.Z, aabb.Max.Z, ref tmin, ref tmax, new Vec3(0f, 0f, -1f), new Vec3(0f, 0f, 1f), ref normal))
            {
                distance = 0f;
                return false;
            }

            if (tmax < 0f)
            {
                distance = 0f;
                return false;
            }

            distance = tmin >= 0f ? tmin : tmax;
            return true;
        }

        private static bool Slab(float origin, float direction, float min, float max, ref float tmin, ref float tmax, in Vec3 nMin, in Vec3 nMax, ref Vec3 normal)
        {
            if (MathUtil.Abs(direction) < MathUtil.Epsilon)
            {
                return origin >= min && origin <= max;
            }

            var invD = 1f / direction;
            var t1 = (min - origin) * invD;
            var t2 = (max - origin) * invD;
            var n1 = nMin;
            var n2 = nMax;

            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
                (n1, n2) = (n2, n1);
            }

            if (t1 > tmin)
            {
                tmin = t1;
                normal = n1;
            }

            if (t2 < tmax) tmax = t2;

            return tmin <= tmax;
        }

        public static bool Overlap(in Sphere a, in Sphere b)
        {
            var r = a.Radius + b.Radius;
            return (a.Center - b.Center).SqrMagnitude <= r * r;
        }

        public static bool Overlap(in Sphere sphere, in Aabb aabb)
        {
            // AABB 上距离球心最近的点。
            var cx = MathUtil.Clamp(sphere.Center.X, aabb.Min.X, aabb.Max.X);
            var cy = MathUtil.Clamp(sphere.Center.Y, aabb.Min.Y, aabb.Max.Y);
            var cz = MathUtil.Clamp(sphere.Center.Z, aabb.Min.Z, aabb.Max.Z);
            var d = new Vec3(cx, cy, cz) - sphere.Center;
            return d.SqrMagnitude <= sphere.Radius * sphere.Radius;
        }

        public static bool Overlap(in Aabb a, in Aabb b)
        {
            return a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
                   a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
                   a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
        }

        public static float DistancePointSegmentSquared(in Vec3 p, in Vec3 a, in Vec3 b, out float t)
        {
            var ab = b - a;
            var ap = p - a;
            var abLenSq = ab.SqrMagnitude;

            if (abLenSq <= MathUtil.Epsilon)
            {
                t = 0f;
                return (p - a).SqrMagnitude;
            }

            t = Vec3.Dot(ap, ab) / abLenSq;
            t = MathUtil.Clamp01(t);
            var closest = a + ab * t;
            return (p - closest).SqrMagnitude;
        }

        public static bool Overlap(in Sphere sphere, in Capsule capsule)
        {
            _ = DistancePointSegmentSquared(sphere.Center, capsule.A, capsule.B, out _);
            var distSq = DistancePointSegmentSquared(sphere.Center, capsule.A, capsule.B, out _);
            var r = sphere.Radius + capsule.Radius;
            return distSq <= r * r;
        }

        public static bool Raycast(in Ray3 ray, in Capsule capsule, out float distance, out Vec3 normal)
        {
            // 近似处理：ray vs capsule 等价为 ray vs 沿线段扫掠的 sphere。
            // 当前先采样到线段的最近接近点；完整求解无限圆柱与两端球体相交较复杂。
            // 这里通过检测两端球体和 capsule 的 AABB 提供保守回退。
            var min = Vec3.Min(capsule.A, capsule.B) - new Vec3(capsule.Radius, capsule.Radius, capsule.Radius);
            var max = Vec3.Max(capsule.A, capsule.B) + new Vec3(capsule.Radius, capsule.Radius, capsule.Radius);
            if (!Raycast(ray, new Aabb(min, max), out var dAabb, out _))
            {
                distance = 0f;
                normal = Vec3.Zero;
                return false;
            }

            var hit = false;
            var bestD = float.PositiveInfinity;
            var bestN = Vec3.Zero;

            if (Raycast(ray, new Sphere(capsule.A, capsule.Radius), out var d0, out var n0) && d0 < bestD)
            {
                hit = true;
                bestD = d0;
                bestN = n0;
            }

            if (Raycast(ray, new Sphere(capsule.B, capsule.Radius), out var d1, out var n1) && d1 < bestD)
            {
                hit = true;
                bestD = d1;
                bestN = n1;
            }

            if (!hit)
            {
                // 命中 AABB 但未命中两端球体时，接受 AABB 命中并使用占位法线。
                hit = true;
                bestD = dAabb;
                bestN = Vec3.Zero;
            }

            distance = bestD;
            normal = bestN;
            return true;
        }

        public static bool Raycast(in Ray3 ray, in ColliderShape shape, out float distance, out Vec3 normal)
        {
            switch (shape.Type)
            {
                case ColliderShapeType.Sphere:
                    return Raycast(ray, shape.Sphere, out distance, out normal);
                case ColliderShapeType.Aabb:
                    return Raycast(ray, shape.Aabb, out distance, out normal);
                case ColliderShapeType.Capsule:
                    return Raycast(ray, shape.Capsule, out distance, out normal);
                case ColliderShapeType.OBB:
                    return Raycast(ray, shape.Obb, out distance, out normal);
                default:
                    distance = 0f;
                    normal = Vec3.Zero;
                    return false;
            }
        }

        public static bool Overlap(in Sphere sphere, in ColliderShape shape)
        {
            switch (shape.Type)
            {
                case ColliderShapeType.Sphere:
                    return Overlap(sphere, shape.Sphere);
                case ColliderShapeType.Aabb:
                    return Overlap(sphere, shape.Aabb);
                case ColliderShapeType.Capsule:
                    return Overlap(sphere, shape.Capsule);
                case ColliderShapeType.OBB:
                    return OverlapObbSphere(in shape.Obb, in sphere);
                default:
                    return false;
            }
        }

        public static bool Raycast(in Ray3 ray, in Obb obb, out float distance, out Vec3 normal)
        {
            obb.GetAxes(out var right, out var up, out var forward);
            var e = obb.HalfExtents;

            var obbToRay = ray.Origin - obb.Center;

            // OBB 到射线空间的变换矩阵（局部坐标系基向量）
            var f0 = right;
            var f1 = up;
            var f2 = forward;

            var ox = Vec3.Dot(obbToRay, f0);
            var oy = Vec3.Dot(obbToRay, f1);
            var oz = Vec3.Dot(obbToRay, f2);

            var dx = Vec3.Dot(ray.Direction, f0);
            var dy = Vec3.Dot(ray.Direction, f1);
            var dz = Vec3.Dot(ray.Direction, f2);

            var ex = e.X;
            var ey = e.Y;
            var ez = e.Z;

            var tmin = float.NegativeInfinity;
            var tmax = float.PositiveInfinity;
            normal = Vec3.Zero;

            // X 轴 slab
            if (!Slab(ox, dx, -ex, ex, ref tmin, ref tmax, -f0, f0, ref normal)) { distance = 0f; return false; }
            // Y 轴 slab
            if (!Slab(oy, dy, -ey, ey, ref tmin, ref tmax, -f1, f1, ref normal)) { distance = 0f; return false; }
            // Z 轴 slab
            if (!Slab(oz, dz, -ez, ez, ref tmin, ref tmax, -f2, f2, ref normal)) { distance = 0f; return false; }

            if (tmax < 0f) { distance = 0f; return false; }

            distance = tmin >= 0f ? tmin : tmax;
            return true;
        }

        public static bool Overlap(in Aabb a, in Capsule capsule)
        {
            var closest = new Vec3(
                MathUtil.Clamp(capsule.Center.X, a.Min.X, a.Max.X),
                MathUtil.Clamp(capsule.Center.Y, a.Min.Y, a.Max.Y),
                MathUtil.Clamp(capsule.Center.Z, a.Min.Z, a.Max.Z));

            var distSq = (closest - capsule.Center).SqrMagnitude;
            return distSq <= capsule.Radius * capsule.Radius;
        }

        public static bool Overlap(in Capsule a, in Capsule b)
        {
            var distSq = DistancePointSegmentSquared(a.Center, b.A, b.B, out _);
            var r = a.Radius + b.Radius;
            return distSq <= r * r;
        }

        private static bool OverlapObbSphere(in Obb obb, in Sphere sphere)
        {
            obb.GetAxes(out var right, out var up, out var forward);
            var e = obb.HalfExtents;

            var d = sphere.Center - obb.Center;

            var qx = Vec3.Dot(d, right);
            var qy = Vec3.Dot(d, up);
            var qz = Vec3.Dot(d, forward);

            var ex = e.X;
            var ey = e.Y;
            var ez = e.Z;

            var cx = qx;
            if (cx < -ex) cx = -ex;
            if (cx > ex) cx = ex;

            var cy = qy;
            if (cy < -ey) cy = -ey;
            if (cy > ey) cy = ey;

            var cz = qz;
            if (cz < -ez) cz = -ez;
            if (cz > ez) cz = ez;

            var dx = qx - cx;
            var dy = qy - cy;
            var dz = qz - cz;
            return dx * dx + dy * dy + dz * dz <= sphere.Radius * sphere.Radius;
        }

        public static bool Overlap(in Obb a, in Obb b)
        {
            a.GetAxes(out var ar0, out var ar1, out var ar2);
            b.GetAxes(out var br0, out var br1, out var br2);

            var ae = a.HalfExtents;
            var be = b.HalfExtents;

            var d = b.Center - a.Center;

            var ad = new[] { Vec3.Dot(d, ar0), Vec3.Dot(d, ar1), Vec3.Dot(d, ar2) };
            var bd = new[] { Vec3.Dot(d, br0), Vec3.Dot(d, br1), Vec3.Dot(d, br2) };
            var r = new[] { ae.X, ae.Y, ae.Z };
            var s = new[] { be.X, be.Y, be.Z };

            var ra = r[0];
            var rb = s[0] * MathUtil.Abs(Vec3.Dot(ar0, br0)) + s[1] * MathUtil.Abs(Vec3.Dot(ar0, br1)) + s[2] * MathUtil.Abs(Vec3.Dot(ar0, br2));
            if (MathUtil.Abs(ad[0]) > ra + rb) return false;

            ra = r[1];
            rb = s[0] * MathUtil.Abs(Vec3.Dot(ar1, br0)) + s[1] * MathUtil.Abs(Vec3.Dot(ar1, br1)) + s[2] * MathUtil.Abs(Vec3.Dot(ar1, br2));
            if (MathUtil.Abs(ad[1]) > ra + rb) return false;

            ra = r[2];
            rb = s[0] * MathUtil.Abs(Vec3.Dot(ar2, br0)) + s[1] * MathUtil.Abs(Vec3.Dot(ar2, br1)) + s[2] * MathUtil.Abs(Vec3.Dot(ar2, br2));
            if (MathUtil.Abs(ad[2]) > ra + rb) return false;

            return true;
        }
    }
}
