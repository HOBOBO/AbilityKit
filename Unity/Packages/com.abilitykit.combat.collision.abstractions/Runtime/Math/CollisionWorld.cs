using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Mathematics
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

        public static ColliderId Invalid => default;
        public static bool operator ==(ColliderId lhs, ColliderId rhs) => lhs.Value == rhs.Value;
        public static bool operator !=(ColliderId lhs, ColliderId rhs) => lhs.Value != rhs.Value;
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

        public static RaycastHit Invalid => default;
    }

    /// <summary>
    /// 碰撞世界接口
    ///
    /// 核心职责：
    /// - 管理碰撞体生命周期（Add/Remove/Update）
    /// - 提供碰撞查询（Raycast/Overlap/Sweep）
    /// - 层过滤和层关系判断
    ///
    /// 层系统说明：
    /// - 每个碰撞体有一个层 ID（0-63）
    /// - LayerFilter 控制哪些层可以被检测
    /// - CollisionLayerMatrix 定义层之间的碰撞关系
    /// </summary>
    public interface ICollisionWorld
    {
        // ============ 实体管理 ============

        /// <summary>
        /// 添加碰撞体
        /// </summary>
        /// <param name="transform">世界变换</param>
        /// <param name="localShape">局部形状</param>
        /// <param name="layerId">层 ID（0-63）</param>
        /// <returns>碰撞体 ID</returns>
        ColliderId Add(in Transform3 transform, in ColliderShape localShape, int layerId);

        /// <summary>
        /// 移除碰撞体
        /// </summary>
        bool Remove(ColliderId id);

        /// <summary>
        /// 更新碰撞体变换
        /// </summary>
        bool UpdateTransform(ColliderId id, in Transform3 transform);

        /// <summary>
        /// 更新碰撞体形状
        /// </summary>
        bool UpdateShape(ColliderId id, in ColliderShape localShape);

        /// <summary>
        /// 更新碰撞体层
        /// </summary>
        bool UpdateLayer(ColliderId id, int layerId);

        /// <summary>
        /// 更新碰撞体变换和形状
        /// </summary>
        bool Update(ColliderId id, in Transform3 transform, in ColliderShape localShape);

        // ============ 查询 ============

        /// <summary>
        /// 射线检测
        /// </summary>
        /// <param name="ray">射线</param>
        /// <param name="maxDistance">最大距离</param>
        /// <param name="filter">层过滤器</param>
        /// <param name="hit">命中结果</param>
        /// <returns>是否命中</returns>
        bool Raycast(in Ray3 ray, float maxDistance, in Combat.Collision.LayerFilter filter, out RaycastHit hit);

        /// <summary>
        /// 球体重叠检测
        /// </summary>
        /// <param name="sphere">球体</param>
        /// <param name="filter">层过滤器</param>
        /// <param name="results">结果列表</param>
        /// <returns>命中数量</returns>
        int OverlapSphere(in Sphere sphere, in Combat.Collision.LayerFilter filter, List<ColliderId> results);

        // ============ 层关系 ============

        /// <summary>
        /// 检查两个层之间是否应该检测碰撞
        /// 使用 CollisionLayerMatrix 进行层关系判断
        /// </summary>
        /// <param name="layerA">层 A</param>
        /// <param name="layerB">层 B</param>
        /// <returns>是否应该检测碰撞</returns>
        bool ShouldCollide(int layerA, int layerB);

        /// <summary>
        /// 获取碰撞体所在的层
        /// </summary>
        /// <param name="id">碰撞体 ID</param>
        /// <param name="layerId">层 ID（输出）</param>
        /// <returns>是否成功获取</returns>
        bool GetLayer(ColliderId id, out int layerId);
    }

    public readonly struct OrientedBoxSweep
    {
        public readonly Vec3 Center;
        public readonly Vec3 Right;
        public readonly Vec3 Up;
        public readonly Vec3 Forward;
        public readonly Vec3 HalfExtents;

        public OrientedBoxSweep(in Vec3 center, in Vec3 right, in Vec3 up, in Vec3 forward, in Vec3 halfExtents)
        {
            Center = center;
            Right = DeterministicMathBridge.Normalize(in right);
            Up = DeterministicMathBridge.Normalize(in up);
            Forward = DeterministicMathBridge.Normalize(in forward);
            HalfExtents = new Vec3(
                MathUtil.Max(0f, halfExtents.X),
                MathUtil.Max(0f, halfExtents.Y),
                MathUtil.Max(0f, halfExtents.Z));
        }
    }

    public interface IOrientedBoxSweepCollisionWorld
    {
        bool SweepOrientedBox(
            in OrientedBoxSweep box,
            in Vec3 direction,
            float maxDistance,
            in Combat.Collision.LayerFilter filter,
            out RaycastHit hit);
    }

    /// <summary>
    /// 支持球形移动体扫掠的碰撞世界。把"半径 r 的球沿方向扫掠是否命中"归约为
    /// "球心射线对每个障碍按 r 做 Minkowski 膨胀后做射线检测"——对 OBB/AABB/球精确
    /// （OBB 保留旋转），胶囊沿用既有近似。供 MOBA 圆形移动体使用。
    /// </summary>
    public interface ISphereSweepCollisionWorld
    {
        bool SweepSphere(
            in Vec3 start,
            in Vec3 direction,
            float maxDistance,
            float radius,
            in Combat.Collision.LayerFilter filter,
            out RaycastHit hit);
    }

    public sealed class NaiveCollisionWorld : ICollisionWorld, IOrientedBoxSweepCollisionWorld, ISphereSweepCollisionWorld, Combat.Collision.ICollisionLayerRelation
    {
        private struct Entry
        {
            public Transform3 Transform;
            public ColliderShape LocalShape;
            public int LayerId;
            public bool Alive;
        }

        private readonly List<Entry> _entries = new List<Entry>(64);
        private int _nextId = 1;
        private readonly Combat.Collision.CollisionLayerMatrix _layerMatrix;

        public NaiveCollisionWorld()
        {
            _layerMatrix = new Combat.Collision.CollisionLayerMatrix();
        }

        // ============ ICollisionLayerRelation 实现 ============

        public void SetRelation(int layerA, int layerB, CollisionResponse response)
        {
            _layerMatrix.SetRelation(layerA, layerB, response);
        }

        public CollisionResponse GetRelation(int layerA, int layerB)
        {
            return _layerMatrix.GetRelation(layerA, layerB);
        }

        public bool ShouldDetect(int layerA, int layerB)
        {
            return _layerMatrix.ShouldDetect(layerA, layerB);
        }

        // ============ ICollisionWorld 实现 ============

        public ColliderId Add(in Transform3 transform, in ColliderShape localShape, int layerId)
        {
            var id = new ColliderId(_nextId++);
            _entries.Add(new Entry { Transform = transform, LocalShape = localShape, LayerId = layerId, Alive = true });
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

        public bool UpdateLayer(ColliderId id, int layerId)
        {
            var idx = id.Value - 1;
            if (idx < 0 || idx >= _entries.Count) return false;
            var e = _entries[idx];
            if (!e.Alive) return false;
            e.LayerId = layerId;
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

        public bool GetLayer(ColliderId id, out int layerId)
        {
            layerId = 0;
            var idx = id.Value - 1;
            if (idx < 0 || idx >= _entries.Count) return false;
            var e = _entries[idx];
            if (!e.Alive) return false;
            layerId = e.LayerId;
            return true;
        }

        public bool ShouldCollide(int layerA, int layerB)
        {
            return _layerMatrix.ShouldDetect(layerA, layerB);
        }

        public bool Raycast(in Ray3 ray, float maxDistance, in Combat.Collision.LayerFilter filter, out RaycastHit hit)
        {
            var best = float.PositiveInfinity;
            var bestId = default(ColliderId);
            var bestNormal = Vec3.Zero;

            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Alive) continue;
                if (!filter.IsLayerIncluded(e.LayerId)) continue;
                if (filter.ShouldIgnore(i + 1)) continue;

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
                hit = new RaycastHit(bestId, best, ray.GetPoint(best), bestNormal);
                return true;
            }

            hit = default;
            return false;
        }

        public bool SweepOrientedBox(in OrientedBoxSweep box, in Vec3 direction, float maxDistance, in Combat.Collision.LayerFilter filter, out RaycastHit hit)
        {
            var dir = DeterministicMathBridge.Normalize(in direction);
            if (dir.SqrMagnitude <= 0f || maxDistance < 0f)
            {
                hit = default;
                return false;
            }

            var best = float.PositiveInfinity;
            var bestId = default(ColliderId);
            var bestNormal = Vec3.Zero;
            var localRay = new Ray3(Vec3.Zero, OrientedBoxSweepQueries.ToBoxLocal(dir, in box));

            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Alive) continue;
                if (!filter.IsLayerIncluded(e.LayerId)) continue;
                if (filter.ShouldIgnore(i + 1)) continue;

                var worldShape = ToWorldShape(in e.Transform, in e.LocalShape);
                if (!OrientedBoxSweepQueries.SweepVsShape(in box, in localRay, maxDistance, in worldShape, out var distance, out var candidateNormal)) continue;
                if (distance >= best) continue;

                best = distance;
                bestId = new ColliderId(i + 1);
                bestNormal = candidateNormal;
            }

            if (best < float.PositiveInfinity)
            {
                hit = new RaycastHit(bestId, best, box.Center + dir * best, bestNormal);
                return true;
            }

            hit = default;
            return false;
        }

        public bool SweepSphere(in Vec3 start, in Vec3 direction, float maxDistance, float radius, in Combat.Collision.LayerFilter filter, out RaycastHit hit)
        {
            var dir = DeterministicMathBridge.Normalize(in direction);
            if (dir.SqrMagnitude <= 0f || maxDistance < 0f)
            {
                hit = default;
                return false;
            }

            var best = float.PositiveInfinity;
            var bestId = default(ColliderId);
            var bestNormal = Vec3.Zero;

            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Alive) continue;
                if (!filter.IsLayerIncluded(e.LayerId)) continue;
                if (filter.ShouldIgnore(i + 1)) continue;

                var worldShape = ToWorldShape(in e.Transform, in e.LocalShape);
                if (!SphereSweepQueries.SweepVsShape(in start, in dir, maxDistance, radius, in worldShape, out var d, out var n)) continue;
                if (d >= best) continue;

                best = d;
                bestId = new ColliderId(i + 1);
                bestNormal = n;
            }

            if (best < float.PositiveInfinity)
            {
                hit = new RaycastHit(bestId, best, start + dir * best, bestNormal);
                return true;
            }

            hit = default;
            return false;
        }

        public int OverlapSphere(in Sphere sphere, in Combat.Collision.LayerFilter filter, List<ColliderId> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var count = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Alive) continue;
                if (!filter.IsLayerIncluded(e.LayerId)) continue;
                if (filter.ShouldIgnore(i + 1)) continue;

                var worldShape = ToWorldShape(in e.Transform, in e.LocalShape);
                if (!CollisionQueries.Overlap(sphere, worldShape)) continue;
                results.Add(new ColliderId(i + 1));
                count++;
            }

            return count;
        }

        private static ColliderShape ToWorldShape(in Transform3 t, in ColliderShape local)
        {
            switch (local.Type)
            {
                case ColliderShapeType.Sphere:
                {
                    var c = t.TransformPoint(local.Sphere.Center);
                    var r = local.Sphere.Radius * MaxAbsComponent(t.Scale);
                    return ColliderShape.CreateSphere(c, r);
                }
                case ColliderShapeType.Capsule:
                {
                    var a = t.TransformPoint(local.Capsule.A);
                    var b = t.TransformPoint(local.Capsule.B);
                    var r = local.Capsule.Radius * MaxAbsComponent(t.Scale);
                    return ColliderShape.CreateCapsule(a, b, r);
                }
                case ColliderShapeType.Aabb:
                {
                    var aabb = ToWorldAabbConservative(in t, in local.Aabb);
                    return ColliderShape.CreateAabb(aabb.Min, aabb.Max);
                }
                case ColliderShapeType.OBB:
                {
                    var c = t.TransformPoint(local.Obb.Center);
                    var rot = t.Rotation * local.Obb.Rotation;
                    var ext = local.Obb.HalfExtents * MaxAbsComponent(t.Scale);
                    return ColliderShape.CreateObb(c, rot, ext);
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
