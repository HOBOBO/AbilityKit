using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Math
{
    public readonly struct ColliderId : IEquatable<ColliderId>
    {
        public readonly int Value;

        public ColliderId(int value)
        {
            Value = value;
        }

        public bool Equals(ColliderId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ColliderId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }

    public readonly struct RaycastHit
    {
        public readonly ColliderId Collider;
        public readonly float Distance;
        public readonly Vec3 Point;
        public readonly Vec3 Normal;

        public RaycastHit(ColliderId collider, float distance, in Vec3 point, in Vec3 normal)
        {
            Collider = collider;
            Distance = distance;
            Point = point;
            Normal = normal;
        }
    }

    public interface ICollisionWorld
    {
        ColliderId Add(in Transform3 transform, in ColliderShape localShape, int layerMask = -1);
        bool Remove(ColliderId id);

        bool UpdateTransform(ColliderId id, in Transform3 transform);
        bool UpdateShape(ColliderId id, in ColliderShape localShape);
        bool UpdateLayer(ColliderId id, int layerMask);
        bool Update(ColliderId id, in Transform3 transform, in ColliderShape localShape);

        bool Raycast(in Ray3 ray, float maxDistance, int layerMask, out RaycastHit hit);
        int OverlapSphere(in Sphere sphere, int layerMask, List<ColliderId> results);
    }

    public readonly struct CollisionWorldDebugShape
    {
        public readonly ColliderId Id;
        public readonly ColliderShape WorldShape;
        public readonly int LayerMask;

        public CollisionWorldDebugShape(ColliderId id, in ColliderShape worldShape, int layerMask)
        {
            Id = id;
            WorldShape = worldShape;
            LayerMask = layerMask;
        }
    }

    public interface ICollisionWorldDebugView
    {
        int CopyWorldShapes(List<CollisionWorldDebugShape> results);
    }

    public sealed class NaiveCollisionWorld : ICollisionWorld, ICollisionWorldDebugView
    {
        private struct Entry
        {
            public Transform3 Transform;
            public ColliderShape LocalShape;
            public int LayerMask;
            public bool Alive;
        }

        private readonly List<Entry> _entries = new List<Entry>(64);
        private int _nextId = 1;

        public ColliderId Add(in Transform3 transform, in ColliderShape localShape, int layerMask = -1)
        {
            var id = new ColliderId(_nextId++);
            _entries.Add(new Entry { Transform = transform, LocalShape = localShape, LayerMask = layerMask, Alive = true });
            return id;
        }

        public bool Remove(ColliderId id)
        {
            var idx = id.Value - 1;
            if (idx < 0 || idx >= _entries.Count) return false;
            var e = _entries[idx];
            if (!e.Alive) return false;
            e.Alive = false;
            _entries[idx] = e;
            return true;
        }

        public bool UpdateTransform(ColliderId id, in Transform3 transform)
        {
            var idx = id.Value - 1;
            if (idx < 0 || idx >= _entries.Count) return false;
            var e = _entries[idx];
            if (!e.Alive) return false;
            e.Transform = transform;
            _entries[idx] = e;
            return true;
        }

        public bool UpdateShape(ColliderId id, in ColliderShape localShape)
        {
            var idx = id.Value - 1;
            if (idx < 0 || idx >= _entries.Count) return false;
            var e = _entries[idx];
            if (!e.Alive) return false;
            e.LocalShape = localShape;
            _entries[idx] = e;
            return true;
        }

        public bool UpdateLayer(ColliderId id, int layerMask)
        {
            var idx = id.Value - 1;
            if (idx < 0 || idx >= _entries.Count) return false;
            var e = _entries[idx];
            if (!e.Alive) return false;
            e.LayerMask = layerMask;
            _entries[idx] = e;
            return true;
        }

        public bool Update(ColliderId id, in Transform3 transform, in ColliderShape localShape)
        {
            var idx = id.Value - 1;
            if (idx < 0 || idx >= _entries.Count) return false;
            var e = _entries[idx];
            if (!e.Alive) return false;
            e.Transform = transform;
            e.LocalShape = localShape;
            _entries[idx] = e;
            return true;
        }

        public bool Raycast(in Ray3 ray, float maxDistance, int layerMask, out RaycastHit hit)
        {
            var best = float.PositiveInfinity;
            var bestId = default(ColliderId);
            var bestNormal = Vec3.Zero;

            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Alive) continue;
                if ((e.LayerMask & layerMask) == 0) continue;

                var worldShape = ToWorldShape(in e.Transform, in e.LocalShape);
                if (!CollisionQueries.Raycast(ray, worldShape, out var d, out var n)) continue;
                if (d < 0f || d > maxDistance) continue;

                if (d < best)
                {
                    best = d;
                    bestId = new ColliderId(i + 1);
                    bestNormal = n;
                }
            }

            if (best < float.PositiveInfinity)
            {
                var p = ray.GetPoint(best);
                hit = new RaycastHit(bestId, best, p, bestNormal);
                return true;
            }

            hit = default;
            return false;
        }

        public int OverlapSphere(in Sphere sphere, int layerMask, List<ColliderId> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var count = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Alive) continue;
                if ((e.LayerMask & layerMask) == 0) continue;

                var worldShape = ToWorldShape(in e.Transform, in e.LocalShape);
                if (!CollisionQueries.Overlap(sphere, worldShape)) continue;
                results.Add(new ColliderId(i + 1));
                count++;
            }

            return count;
        }

        public int CopyWorldShapes(List<CollisionWorldDebugShape> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Alive) continue;
                var worldShape = ToWorldShape(in e.Transform, in e.LocalShape);
                results.Add(new CollisionWorldDebugShape(new ColliderId(i + 1), in worldShape, e.LayerMask));
            }

            return results.Count;
        }

        private static ColliderShape ToWorldShape(in Transform3 t, in ColliderShape local)
        {
            switch (local.Type)
            {
                case ColliderShapeType.Sphere:
                {
                    var c = t.TransformPoint(local.Sphere.Center);
                    var r = local.Sphere.Radius * MaxAbsComponent(t.Scale);
                    return ColliderShape.CreateSphere(new Sphere(c, r));
                }
                case ColliderShapeType.Capsule:
                {
                    var a = t.TransformPoint(local.Capsule.A);
                    var b = t.TransformPoint(local.Capsule.B);
                    var r = local.Capsule.Radius * MaxAbsComponent(t.Scale);
                    return ColliderShape.CreateCapsule(new Capsule(a, b, r));
                }
                case ColliderShapeType.Aabb:
                {
                    // If rotated, treat as OBB and conservatively convert to world AABB.
                    var aabb = ToWorldAabbConservative(in t, in local.Aabb);
                    return ColliderShape.CreateAabb(aabb);
                }
                default:
                    return local;
            }
        }

        private static float MaxAbsComponent(in Vec3 v)
        {
            var ax = MathUtil.Abs(v.X);
            var ay = MathUtil.Abs(v.Y);
            var az = MathUtil.Abs(v.Z);
            return MathUtil.Max(ax, MathUtil.Max(ay, az));
        }

        private static Aabb ToWorldAabbConservative(in Transform3 t, in Aabb local)
        {
            // Transform the 8 corners and take min/max.
            var min = local.Min;
            var max = local.Max;

            var c0 = t.TransformPoint(new Vec3(min.X, min.Y, min.Z));
            var c1 = t.TransformPoint(new Vec3(min.X, min.Y, max.Z));
            var c2 = t.TransformPoint(new Vec3(min.X, max.Y, min.Z));
            var c3 = t.TransformPoint(new Vec3(min.X, max.Y, max.Z));
            var c4 = t.TransformPoint(new Vec3(max.X, min.Y, min.Z));
            var c5 = t.TransformPoint(new Vec3(max.X, min.Y, max.Z));
            var c6 = t.TransformPoint(new Vec3(max.X, max.Y, min.Z));
            var c7 = t.TransformPoint(new Vec3(max.X, max.Y, max.Z));

            var wMin = c0;
            wMin = Vec3.Min(wMin, c1);
            wMin = Vec3.Min(wMin, c2);
            wMin = Vec3.Min(wMin, c3);
            wMin = Vec3.Min(wMin, c4);
            wMin = Vec3.Min(wMin, c5);
            wMin = Vec3.Min(wMin, c6);
            wMin = Vec3.Min(wMin, c7);

            var wMax = c0;
            wMax = Vec3.Max(wMax, c1);
            wMax = Vec3.Max(wMax, c2);
            wMax = Vec3.Max(wMax, c3);
            wMax = Vec3.Max(wMax, c4);
            wMax = Vec3.Max(wMax, c5);
            wMax = Vec3.Max(wMax, c6);
            wMax = Vec3.Max(wMax, c7);

            return new Aabb(wMin, wMax);
        }
    }
}
