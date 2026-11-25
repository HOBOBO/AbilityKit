using System;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 封装投射物的确定性分段扫描及碰撞查询边界，不持有弹体或改变其生命周期。
    /// </summary>
    internal sealed class ProjectileCollisionQueries
    {
        private static readonly Fixed64 EpsilonAdvance = Fixed64.FromRatio(1, 1000);
        private readonly ICollisionWorld _collision;

        internal ProjectileCollisionQueries(ICollisionWorld collision)
        {
            _collision = collision ?? throw new ArgumentNullException(nameof(collision));
        }

        internal static Fixed64 EstimateElapsedWithinTick(Fixed64 fixedDeltaSeconds, Fixed64 travelled, Fixed64 stepDistance)
        {
            if (fixedDeltaSeconds <= Fixed64.Zero || travelled <= Fixed64.Zero || stepDistance <= Fixed64.Zero)
                return Fixed64.Zero;
            var ratio = travelled / stepDistance;
            if (ratio < Fixed64.Zero) ratio = Fixed64.Zero;
            if (ratio > Fixed64.One) ratio = Fixed64.One;
            return fixedDeltaSeconds * ratio;
        }

        internal bool TrySweepTrackingArcSegments(
            in FixedVec3 start,
            in FixedVec3 end,
            Fixed64 progressStart,
            Fixed64 progressEnd,
            Fixed64 arcHeight,
            Fixed64 arcStartY,
            Fixed64 arcEndY,
            int layerMask,
            int[] ignoredColliderIds,
            int ignoredColliderCount,
            in FixedVec3 halfExtents,
            out RaycastHit hit,
            out Fixed64 distanceAlongPath,
            out FixedVec3 hitDirection)
        {
            /*
             * 固定分段数保持扫描顺序确定，避免把高抛轨迹当作一条长弦检测。
             * 分段扫描不改变模拟路径的权威归属。
             */
            const int segmentCount = 16;
            var previous = start;
            var travelled = Fixed64.Zero;
            var endCurveY = EvaluateTrackingArcY(progressEnd, arcHeight, arcStartY, arcEndY);
            var endCorrection = end.Y - endCurveY;

            for (var segment = 1; segment <= segmentCount; segment++)
            {
                var t = Fixed64.FromRatio(segment, segmentCount);
                var progress = progressStart + (progressEnd - progressStart) * t;
                var curveY = EvaluateTrackingArcY(progress, arcHeight, arcStartY, arcEndY);
                var correctedY = curveY + endCorrection * t;
                var point = new FixedVec3(
                    start.X + (end.X - start.X) * t,
                    correctedY,
                    start.Z + (end.Z - start.Z) * t);
                var delta = point - previous;
                var segmentLength = delta.Magnitude;
                if (segmentLength <= Fixed64.Zero)
                {
                    previous = point;
                    continue;
                }

                var direction = delta / segmentLength;
                if (TrySweepSkippingIgnored(previous, direction, segmentLength, layerMask, ignoredColliderIds, ignoredColliderCount, halfExtents, out hit))
                {
                    distanceAlongPath = travelled + Fixed64.FromSingle(hit.Distance);
                    hitDirection = direction;
                    return true;
                }

                travelled += segmentLength;
                previous = point;
            }

            hit = default;
            distanceAlongPath = travelled;
            hitDirection = FixedVec3.Zero;
            return false;
        }

        internal static Fixed64 EvaluateTrackingArcY(
            Fixed64 progress,
            Fixed64 arcHeight,
            Fixed64 arcStartY,
            Fixed64 arcEndY)
        {
            if (progress < Fixed64.Zero) progress = Fixed64.Zero;
            if (progress > Fixed64.One) progress = Fixed64.One;
            var arc = Fixed64.FromInt32(4) * arcHeight * progress * (Fixed64.One - progress);
            return arcStartY + (arcEndY - arcStartY) * progress + arc;
        }

        internal bool TrySweepSkippingIgnored(in FixedVec3 origin, in FixedVec3 dir, Fixed64 maxDistance, int layerMask, int[] ignoredColliderIds, int ignoredColliderCount, in FixedVec3 halfExtents, out RaycastHit hit)
        {
            /*
             * 使用固定重试次数，保持确定性并避免无限循环。
             */
            const int maxAttempts = 4;

            var o = origin;
            var remaining = maxDistance;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!TrySweep(o, dir, remaining, layerMask, ignoredColliderIds, ignoredColliderCount, halfExtents, out hit))
                {
                    hit = default;
                    return false;
                }

                if (!ContainsCollider(ignoredColliderIds, ignoredColliderCount, hit.Collider))
                {
                    return true;
                }

                /*
                 * 跳过被忽略的命中点，并从命中点稍后位置继续尝试。
                 */
                o = hit.Point.ToFixed() + dir * EpsilonAdvance;
                remaining -= Fixed64.FromSingle(hit.Distance) + EpsilonAdvance;
                if (remaining <= Fixed64.Zero)
                {
                    hit = default;
                    return false;
                }
            }

            hit = default;
            return false;
        }

        private bool TrySweep(in FixedVec3 origin, in FixedVec3 dir, Fixed64 maxDistance, int layerMask, int[] ignoredColliderIds, int ignoredColliderCount, in FixedVec3 halfExtents, out RaycastHit hit)
        {
            /*
             * 碰撞世界仍是 float 查询面，在边界做一次性确定性换算：
             * 输入来自定点状态（换算位一致），输出（命中距离/点）由 float 查询内部决定，
             * 其确定性归属仍由碰撞包负责。
             */
            var originVec = origin.ToVec3();
            var dirVec = dir.ToVec3();
            var maxDistanceFloat = maxDistance.ToSingle();

            if (halfExtents.SqrMagnitude > Fixed64.Zero && _collision is IOrientedBoxSweepCollisionWorld boxSweepWorld)
            {
                var forward = dirVec.Normalized;
                var right = Vec3.Cross(Vec3.Up, forward).Normalized;
                if (right.SqrMagnitude <= 0f) right = Vec3.Right;
                var up = Vec3.Cross(forward, right).Normalized;
                var box = new OrientedBoxSweep(originVec, right, up, forward, halfExtents.ToVec3());
                var filter = new LayerFilter(layerMask, ignoredColliderIds);
                return boxSweepWorld.SweepOrientedBox(in box, in forward, maxDistanceFloat, in filter, out hit);
            }

            var ray = new Ray3(originVec, dirVec);
            var filter2 = new LayerFilter(layerMask, ignoredColliderIds);
            return _collision.Raycast(ray, maxDistanceFloat, in filter2, out hit);
        }

        private static bool ContainsCollider(int[] colliderIds, int count, ColliderId collider)
        {
            if (colliderIds == null || count <= 0) return false;
            var limit = count < colliderIds.Length ? count : colliderIds.Length;
            for (var i = 0; i < limit; i++)
            {
                if (colliderIds[i] == collider.Value) return true;
            }
            return false;
        }

    }
}
