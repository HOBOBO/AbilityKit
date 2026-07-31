using AbilityKit.Combat.Collision;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 球形移动体（半径 <c>r</c>）的扫掠窄相：把“半径 r 的球沿方向扫掠是否命中某形状”归约为
    /// “球心射线对该形状按 r 做 Minkowski 膨胀后做射线检测”。OBB / AABB / 球均精确——
    /// 其中 OBB 转到盒本地系后用 <see cref="OrientedBoxSweepQueries.RaycastExpandedAabbLocal"/>，
    /// 保留旋转（修复“旋转矩形按外接 AABB 拦截”的问题）；胶囊沿用 <see cref="CollisionQueries.Raycast(Ray3, Capsule)"/> 的既有近似。
    /// 贴边/已穿透时按分离方向判定（远离不阻挡），与盒扫掠同源，避免反向卡墙。
    /// 调用方（碰撞世界）自行做最近命中取舍。
    /// </summary>
    public static class SphereSweepQueries
    {
        public static bool SweepVsShape(
            in Vec3 start,
            in Vec3 direction,
            float maxDistance,
            float radius,
            in ColliderShape worldShape,
            out float distance,
            out Vec3 worldNormal)
        {
            switch (worldShape.Type)
            {
                case ColliderShapeType.Aabb:
                    return SweepBoxLike(in start, in direction, maxDistance, radius, worldShape.Aabb.Min, worldShape.Aabb.Max, Vec3.Right, Vec3.Up, Vec3.Forward, out distance, out worldNormal);
                case ColliderShapeType.OBB:
                {
                    var obb = worldShape.Obb;
                    obb.GetAxes(out var right, out var up, out var forward);
                    var min = -(obb.HalfExtents);
                    var max = obb.HalfExtents;
                    return SweepBoxLike(in start, in direction, maxDistance, radius, min, max, right, up, forward, out distance, out worldNormal, obb.Center);
                }
                case ColliderShapeType.Sphere:
                    return SweepSphere(in start, in direction, maxDistance, radius, worldShape.Sphere, out distance, out worldNormal);
                case ColliderShapeType.Capsule:
                    return SweepCapsule(in start, in direction, maxDistance, radius, worldShape.Capsule, out distance, out worldNormal);
                default:
                    distance = 0f;
                    worldNormal = Vec3.Zero;
                    return false;
            }
        }

        /// <summary>
        /// 球 vs（轴对齐或带旋转的）盒：在盒本地系下对“半范围 + r”的膨胀 AABB 做射线检测。
        /// <paramref name="right/up/forward"/> 为盒世界三轴（AABB 传世界轴，OBB 传其旋转轴）；
        /// <paramref name="center"/> 为盒世界中心（AABB 传 Vec3.Zero，因为 min/max 已是世界系）。
        /// </summary>
        private static bool SweepBoxLike(
            in Vec3 start,
            in Vec3 direction,
            float maxDistance,
            float radius,
            in Vec3 min,
            in Vec3 max,
            in Vec3 right,
            in Vec3 up,
            in Vec3 forward,
            out float distance,
            out Vec3 worldNormal,
            in Vec3 center = default)
        {
            var rel = start - center;
            var localOrigin = new Vec3(Vec3.Dot(rel, right), Vec3.Dot(rel, up), Vec3.Dot(rel, forward));
            var localDir = new Vec3(Vec3.Dot(direction, right), Vec3.Dot(direction, up), Vec3.Dot(direction, forward));
            var ext = new Vec3(radius, radius, radius);
            var expanded = new Aabb(min - ext, max + ext);

            if (!OrientedBoxSweepQueries.RaycastExpandedAabbLocal(in localOrigin, in localDir, maxDistance, in expanded, out distance, out var localNormal))
            {
                worldNormal = Vec3.Zero;
                return false;
            }

            worldNormal = (right * localNormal.X + up * localNormal.Y + forward * localNormal.Z).Normalized;
            return true;
        }

        private static bool SweepSphere(in Vec3 start, in Vec3 direction, float maxDistance, float radius, in Sphere obstacle, out float distance, out Vec3 worldNormal)
        {
            var expandedRadius = obstacle.Radius + radius;
            var d = start - obstacle.Center;
            var distSq = d.SqrMagnitude;

            if (distSq <= expandedRadius * expandedRadius)
            {
                // 贴边/已穿透：分离法向沿“障碍心→球心”（指向移动体）。
                var dist = MathUtil.Sqrt(distSq);
                var sep = dist > MathUtil.Epsilon ? d / dist : -direction;
                worldNormal = sep;
                if (Vec3.Dot(direction, sep) >= 0f)
                {
                    distance = 0f;
                    return false;
                }

                distance = 0f;
                return true;
            }

            if (!CollisionQueries.Raycast(new Ray3(start, direction), new Sphere(obstacle.Center, expandedRadius), out distance, out var n))
            {
                worldNormal = Vec3.Zero;
                return false;
            }

            if (distance < 0f || distance > maxDistance)
            {
                worldNormal = Vec3.Zero;
                return false;
            }

            worldNormal = n;
            return true;
        }

        private static bool SweepCapsule(in Vec3 start, in Vec3 direction, float maxDistance, float radius, in Capsule obstacle, out float distance, out Vec3 worldNormal)
        {
            var expandedRadius = obstacle.Radius + radius;
            var distSq = CollisionQueries.DistancePointSegmentSquared(start, obstacle.A, obstacle.B, out var t);

            if (distSq <= expandedRadius * expandedRadius)
            {
                // 贴边/已穿透：分离法向沿“ capsules 线段最近点→球心”。
                var ab = obstacle.B - obstacle.A;
                var closest = obstacle.A + ab * t;
                var d = start - closest;
                var dist = MathUtil.Sqrt(distSq);
                var sep = dist > MathUtil.Epsilon ? d / dist : -direction;
                worldNormal = sep;
                if (Vec3.Dot(direction, sep) >= 0f)
                {
                    distance = 0f;
                    return false;
                }

                distance = 0f;
                return true;
            }

            if (!CollisionQueries.Raycast(new Ray3(start, direction), new Capsule(obstacle.A, obstacle.B, expandedRadius), out distance, out var n))
            {
                worldNormal = Vec3.Zero;
                return false;
            }

            if (distance < 0f || distance > maxDistance)
            {
                worldNormal = Vec3.Zero;
                return false;
            }

            worldNormal = n;
            return true;
        }
    }
}
