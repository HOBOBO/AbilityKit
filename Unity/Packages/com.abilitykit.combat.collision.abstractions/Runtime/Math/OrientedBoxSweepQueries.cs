using AbilityKit.Combat.Collision;
using AbilityKit.Deterministic;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 有向方盒扫掠（OBB sweep）的共享窄相：把"沿方向扫掠一个 OBB 是否命中某形状"
    /// 归约为"在盒本地坐标系下、对 Minkowski 膨胀后的 AABB 做射线测试"。
    /// 供 <see cref="NaiveCollisionWorld"/> 与 <see cref="GridCollisionWorld"/> 共用，避免重复该易错的 box-local 技巧。
    /// </summary>
    public static class OrientedBoxSweepQueries
    {
        /// <summary>世界向量 → 盒本地坐标。</summary>
        public static Vec3 ToBoxLocal(in Vec3 worldVector, in OrientedBoxSweep box)
        {
            return new Vec3(
                Vec3.Dot(worldVector, box.Right),
                Vec3.Dot(worldVector, box.Up),
                Vec3.Dot(worldVector, box.Forward));
        }

        /// <summary>盒本地向量 → 世界坐标。</summary>
        public static Vec3 FromBoxLocal(in Vec3 localVector, in OrientedBoxSweep box)
        {
            return box.Right * localVector.X + box.Up * localVector.Y + box.Forward * localVector.Z;
        }

        /// <summary>
        /// 候选形状在盒本地坐标系下的 AABB（用于 Minkowski 膨胀后做射线测试）。
        /// </summary>
        public static Aabb ToBoxLocalBounds(in ColliderShape shape, in OrientedBoxSweep box)
        {
            switch (shape.Type)
            {
                case ColliderShapeType.Sphere:
                {
                    var center = ToBoxLocal(shape.Sphere.Center - box.Center, in box);
                    var radius = shape.Sphere.Radius;
                    var extent = new Vec3(radius, radius, radius);
                    return new Aabb(center - extent, center + extent);
                }
                case ColliderShapeType.Capsule:
                {
                    var a = ToBoxLocal(shape.Capsule.A - box.Center, in box);
                    var b = ToBoxLocal(shape.Capsule.B - box.Center, in box);
                    var radius = shape.Capsule.Radius;
                    var extent = new Vec3(radius, radius, radius);
                    return new Aabb(Vec3.Min(a, b) - extent, Vec3.Max(a, b) + extent);
                }
                case ColliderShapeType.OBB:
                {
                    // OBB 中心转到盒本地系；extent = OBB 三轴在盒本地三轴上的投影之和（标准 OBB-AABB 投影）。
                    var obb = shape.Obb;
                    var center = ToBoxLocal(obb.Center - box.Center, in box);
                    var oR = obb.Right;
                    var oU = obb.Up;
                    var oF = obb.Forward;
                    var he = obb.HalfExtents;
                    var extent = new Vec3(
                        MathUtil.Abs(Vec3.Dot(box.Right, oR)) * he.X + MathUtil.Abs(Vec3.Dot(box.Right, oU)) * he.Y + MathUtil.Abs(Vec3.Dot(box.Right, oF)) * he.Z,
                        MathUtil.Abs(Vec3.Dot(box.Up, oR)) * he.X + MathUtil.Abs(Vec3.Dot(box.Up, oU)) * he.Y + MathUtil.Abs(Vec3.Dot(box.Up, oF)) * he.Z,
                        MathUtil.Abs(Vec3.Dot(box.Forward, oR)) * he.X + MathUtil.Abs(Vec3.Dot(box.Forward, oU)) * he.Y + MathUtil.Abs(Vec3.Dot(box.Forward, oF)) * he.Z);
                    return new Aabb(center - extent, center + extent);
                }
                case ColliderShapeType.Aabb:
                default:
                {
                    var centerWorld = (shape.Aabb.Min + shape.Aabb.Max) * 0.5f;
                    var worldExtent = (shape.Aabb.Max - shape.Aabb.Min) * 0.5f;
                    var center = ToBoxLocal(centerWorld - box.Center, in box);
                    var extent = new Vec3(
                        MathUtil.Abs(box.Right.X) * worldExtent.X + MathUtil.Abs(box.Right.Y) * worldExtent.Y + MathUtil.Abs(box.Right.Z) * worldExtent.Z,
                        MathUtil.Abs(box.Up.X) * worldExtent.X + MathUtil.Abs(box.Up.Y) * worldExtent.Y + MathUtil.Abs(box.Up.Z) * worldExtent.Z,
                        MathUtil.Abs(box.Forward.X) * worldExtent.X + MathUtil.Abs(box.Forward.Y) * worldExtent.Y + MathUtil.Abs(box.Forward.Z) * worldExtent.Z);
                    return new Aabb(center - extent, center + extent);
                }
            }
        }

        /// <summary>
        /// 单候选窄相：沿 <paramref name="localRay"/>（盒本地系、起点 Zero）扫掠 <paramref name="box"/>
        /// 是否命中世界空间形状 <paramref name="worldShape"/>。命中且距离 ∈ [0, maxDistance] 时返回真，
        /// 输出沿扫掠方向的距离与世界法向（已从盒本地系转回世界系并归一）。
        /// 调用方自行做最近命中（best）取舍。
        /// </summary>
        public static bool SweepVsShape(
            in OrientedBoxSweep box,
            in Ray3 localRay,
            float maxDistance,
            in ColliderShape worldShape,
            out float distance,
            out Vec3 worldNormal)
        {
            var bounds = ToBoxLocalBounds(in worldShape, in box);
            var expanded = new Aabb(bounds.Min - box.HalfExtents, bounds.Max + box.HalfExtents);

            if (!RaycastExpandedAabbLocal(in localRay.Origin, in localRay.Direction, maxDistance, in expanded, out distance, out var localNormal))
            {
                worldNormal = Vec3.Zero;
                return false;
            }

            worldNormal = DeterministicMathBridge.Normalize(FromBoxLocal(in localNormal, in box));
            return true;
        }

        /// <summary>
        /// 共享窄相核：在某本地系下对 Minkowski 膨胀 AABB 做射线检测，并处理“起点已落在膨胀盒内”
        /// 的贴边/已穿透情形——返回（本地系）距离与法向，由调用方转回世界系。供 OBB 盒扫掠
        /// （<see cref="SweepVsShape"/>）与球扫掠（<see cref="SphereSweepQueries"/>）共用。
        /// 返回真=有阻挡命中（贴面加深或正常命中）；假=无阻挡（远离分离或未命中）。
        /// </summary>
        internal static bool RaycastExpandedAabbLocal(
            in Vec3 localOrigin,
            in Vec3 localDir,
            float maxDistance,
            in Aabb expanded,
            out float distance,
            out Vec3 localNormal)
        {
            // 贴边/已穿透：起点落在膨胀盒内时，分层射线法会返回距离≈0、法向可能反向的伪命中，
            // 导致反向移动也被当作碰撞而卡墙。改用“穿透最浅轴”作为分离法向：沿分离向（远离）
            // 不阻挡，沿穿透向（更深）报告 0 距离命中。
            if (PointInsideExpanded(in localOrigin, in expanded))
            {
                var separating = MinSeparatingAxis(in localOrigin, in expanded);
                localNormal = separating;
                if (Vec3.Dot(localDir, separating) >= 0f)
                {
                    distance = 0f;
                    return false;
                }

                distance = 0f;
                return true;
            }

            if (!CollisionQueries.Raycast(new Ray3(localOrigin, localDir), in expanded, out distance, out var n))
            {
                localNormal = Vec3.Zero;
                return false;
            }

            if (distance < 0f || distance > maxDistance)
            {
                localNormal = Vec3.Zero;
                return false;
            }

            localNormal = n;
            return true;
        }

        /// <summary>
        /// 扫掠起点（盒本地系）是否落在 Minkowski 膨胀盒内（含边界）——即移动体已与障碍物重叠或贴边。
        /// </summary>
        internal static bool PointInsideExpanded(in Vec3 localOrigin, in Aabb expanded)
        {
            return localOrigin.X >= expanded.Min.X && localOrigin.X <= expanded.Max.X
                && localOrigin.Y >= expanded.Min.Y && localOrigin.Y <= expanded.Max.Y
                && localOrigin.Z >= expanded.Min.Z && localOrigin.Z <= expanded.Max.Z;
        }

        /// <summary>
        /// 盒心落在膨胀盒内时，取“穿透最浅轴”作为分离法向（盒本地系、单位轴向）。
        /// 该法向指向离障碍物最近的一侧（贴边面穿透为 0 时同样适用），用来判定运动是在
        /// 远离障碍物（沿分离向）还是继续穿透。等价于两 AABB 的最小平移向量（MTV）方向。
        /// </summary>
        internal static Vec3 MinSeparatingAxis(in Vec3 localOrigin, in Aabb expanded)
        {
            var toMaxX = expanded.Max.X - localOrigin.X;
            var toMinX = localOrigin.X - expanded.Min.X;
            var toMaxY = expanded.Max.Y - localOrigin.Y;
            var toMinY = localOrigin.Y - expanded.Min.Y;
            var toMaxZ = expanded.Max.Z - localOrigin.Z;
            var toMinZ = localOrigin.Z - expanded.Min.Z;

            var best = toMaxX;
            var dir = new Vec3(1f, 0f, 0f);
            if (toMinX < best) { best = toMinX; dir = new Vec3(-1f, 0f, 0f); }
            if (toMaxY < best) { best = toMaxY; dir = new Vec3(0f, 1f, 0f); }
            if (toMinY < best) { best = toMinY; dir = new Vec3(0f, -1f, 0f); }
            if (toMaxZ < best) { best = toMaxZ; dir = new Vec3(0f, 0f, 1f); }
            if (toMinZ < best) { best = toMinZ; dir = new Vec3(0f, 0f, -1f); }
            return dir;
        }
    }
}
