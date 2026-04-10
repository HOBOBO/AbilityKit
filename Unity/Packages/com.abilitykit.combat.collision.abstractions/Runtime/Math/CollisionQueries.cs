using System;

namespace AbilityKit.Core.Math
{
    public static class CollisionQueries
    {
        public static bool Raycast(in Ray3 ray, in Sphere sphere, out float distance, out Vec3 normal)
        {
            // Solve |o + t d - c|^2 = r^2
            var oc = ray.Origin - sphere.Center;
            var a = Vec3.Dot(ray.Direction, ray.Direction);
            var b = 2f * Vec3.Dot(oc, ray.Direction);
            var c = Vec3.Dot(oc, oc) - sphere.Radius * sphere.Radius;
            var discriminant = b * b - 4f * a * c;

            if (discriminant < 0f)
            {
                distance = 0f;
                normal = Vec3.Zero;
                return false;
            }

            var sqrt = MathUtil.Sqrt(discriminant);
            var inv2a = 1f / (2f * a);
            var t0 = (-b - sqrt) * inv2a;
            var t1 = (-b + sqrt) * inv2a;

            var t = t0;
            if (t < 0f) t = t1;
            if (t < 0f)
            {
                distance = 0f;
                normal = Vec3.Zero;
                return false;
            }

            distance = t;
            var hitPoint = ray.GetPoint(t);
            var n = hitPoint - sphere.Center;
            var len = n.Magnitude;
            normal = len > MathUtil.Epsilon ? n / len : Vec3.Zero;
            return true;
        }

        public static bool Raycast(in Ray3 ray, in Aabb aabb, out float distance, out Vec3 normal)
        {
            // Slabs method
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
            // Closest point on AABB to sphere center
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
            // Approximation: ray vs capsule = ray vs swept sphere along segment.
            // For now: sample closest approach to segment; if ray intersects the infinite cylinder + end spheres is complex.
            // Provide a conservative fallback by raycasting against end spheres and AABB of capsule.
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
                // AABB hit but no end spheres hit -> accept AABB hit with placeholder normal
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
                default:
                    return false;
            }
        }
    }
}
