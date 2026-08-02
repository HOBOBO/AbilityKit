using System;
using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    public readonly struct MobaMotionHitTriggerRuntime
    {
        public MobaMotionHitTriggerRuntime(
            int triggerId,
            int sourceActorId,
            int sourceConfigId,
            MobaEffectTraceScopeSnapshot traceScope,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            TriggerId = triggerId;
            SourceActorId = sourceActorId;
            SourceConfigId = sourceConfigId;
            TraceScope = traceScope;
            SkillRuntimeHandle = skillRuntimeHandle;
        }

        public int TriggerId { get; }
        public int SourceActorId { get; }
        public int SourceConfigId { get; }
        public MobaEffectTraceScopeSnapshot TraceScope { get; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }

        public bool IsValid => TriggerId > 0 && SourceActorId > 0 && TraceScope.EffectContextId != 0;

        public MobaMotionHitTriggerRuntime WithSourceActor(int sourceActorId)
        {
            return new MobaMotionHitTriggerRuntime(
                TriggerId,
                sourceActorId,
                SourceConfigId,
                TraceScope,
                SkillRuntimeHandle);
        }
    }

    public sealed class MobaMotionHitArgs : MobaTriggerInvocationContextBase, IMobaActorContextProvider, IMobaTriggerExecutionSnapshotProvider
    {
        public override EffectContextKind Kind => EffectContextKind.Trigger;
        public int SourceConfigId { get; set; }
        public int Frame { get; set; }
        public int MotionTargetId { get; set; }
        public ColliderId HitCollider { get; set; }
        public Vec3 Point { get; set; }
        public Vec3 Normal { get; set; }
        public MobaMotionHitTriggerRuntime Runtime { get; set; }

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = SourceActorId > 0 ? SourceActorId : Runtime.SourceActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public override bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            if (Runtime.IsValid)
            {
                lineageContext = new MobaTriggerLineageContext(
                    EffectContextKind.Trigger,
                    MobaTraceKind.EffectExecution,
                    SourceActorId > 0 ? SourceActorId : Runtime.SourceActorId,
                    TargetActorId,
                    Runtime.TraceScope.EffectContextId,
                    Runtime.TraceScope.EffectContextId,
                    Runtime.TraceScope.EffectContextId,
                    SourceConfigId != 0 ? SourceConfigId : Runtime.SourceConfigId);
                return true;
            }

            lineageContext = default;
            return false;
        }

        public override bool TryGetTraceContext(out MobaTriggerTraceContext traceContext)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                traceContext = lineageContext.ToTraceContext();
                return true;
            }

            traceContext = default;
            return false;
        }

        public override bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                origin = MobaGameplayOrigin.FromLineageContext(in lineageContext, Runtime.SkillRuntimeHandle);
                return origin.IsValid;
            }

            origin = default;
            return false;
        }

        public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
        {
            if (!TryGetLineageContext(out var lineageContext))
            {
                snapshot = default;
                return false;
            }

            snapshot = new MobaTriggerExecutionSnapshot(
                lineageContext.ContextKind,
                lineageContext.SourceActorId,
                lineageContext.TargetActorId,
                lineageContext.SourceContextId,
                lineageContext.RootContextId,
                lineageContext.OwnerContextId,
                TriggerId,
                lineageContext.SourceConfigId,
                Frame,
                Runtime.SkillRuntimeHandle);
            return snapshot.IsValid;
        }
    }

    public sealed class MobaMotionCollisionWorldAdapter : IMotionCollisionWorld
    {
        private const int ProjectionDirectionCount = 64;
        private const int ProjectionBinarySearchSteps = 16;
        private const float ProjectionSearchStep = 0.25f;
        private const float ProjectionMaxDistance = 64f;

        private readonly ICollisionWorld _world;
        private readonly MobaActorRegistry _actors;
        private readonly List<ColliderId> _candidates = new List<ColliderId>(16);
        private readonly List<ColliderId> _sampleOverlaps = new List<ColliderId>(16);

        public MobaMotionCollisionWorldAdapter(ICollisionWorld world, MobaActorRegistry actors)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _actors = actors;
        }

        public bool Sweep(
            int moverId,
            in Vec3 start,
            in Vec3 desiredDelta,
            float radius,
            int obstacleMask,
            int ignoreMask,
            out MotionHit hit,
            out Vec3 appliedDelta)
        {
            appliedDelta = desiredDelta;
            var distance = desiredDelta.Magnitude;
            if (distance <= MathUtil.Epsilon)
            {
                hit = MotionHit.None;
                return false;
            }

            var ignoredCollider = ResolveIgnoredCollider(moverId);
            var ignoredColliders = ignoredCollider.Value > 0
                ? new[] { ignoredCollider.Value }
                : null;
            var layerFilter = new LayerFilter(ResolveMask(obstacleMask), ignoredColliders);
            var sweepRadius = MathUtil.Max(radius, 0.01f);
            var direction = desiredDelta / distance;

            // 球形移动体扫掠：把移动体当作半径 sweepRadius 的球，球心射线对障碍按半径做
            // Minkowski 膨胀后检测——对 OBB 精确（保留旋转）。MOBA 角色为圆形，优先走此路径。
            if (_world is ISphereSweepCollisionWorld sphereWorld)
            {
                if (sphereWorld.SweepSphere(in start, in direction, distance, sweepRadius, in layerFilter, out var sphereHit)
                    && !ShouldIgnore(sphereHit.Collider, ignoredCollider, ignoreMask))
                {
                    var appliedDistance = MathUtil.Clamp(sphereHit.Distance, 0f, distance);
                    var normal = sphereHit.Normal.SqrMagnitude > MathUtil.Epsilon ? sphereHit.Normal : -direction;
                    appliedDelta = direction * appliedDistance;
                    hit = new MotionHit(true, sphereHit.Collider.Value, normal, distance > MathUtil.Epsilon ? MathUtil.Clamp01(appliedDistance / distance) : 0f);
                    return true;
                }

                hit = MotionHit.None;
                return false;
            }

            if (_world is IOrientedBoxSweepCollisionWorld sweepWorld)
            {
                var halfExtents = new Vec3(sweepRadius, sweepRadius, sweepRadius);
                var box = new OrientedBoxSweep(
                    start,
                    new Vec3(1f, 0f, 0f),
                    new Vec3(0f, 1f, 0f),
                    new Vec3(0f, 0f, 1f),
                    halfExtents);

                if (sweepWorld.SweepOrientedBox(
                        in box,
                        in direction,
                        distance,
                        in layerFilter,
                        out var sweepHit) &&
                    !ShouldIgnore(sweepHit.Collider, ignoredCollider, ignoreMask))
                {
                    var appliedDistance = MathUtil.Clamp(sweepHit.Distance, 0f, distance);
                    var time01 = distance > MathUtil.Epsilon
                        ? MathUtil.Clamp01(appliedDistance / distance)
                        : 0f;
                    var normal = sweepHit.Normal.SqrMagnitude > MathUtil.Epsilon
                        ? sweepHit.Normal
                        : -direction;
                    appliedDelta = direction * appliedDistance;
                    hit = new MotionHit(true, sweepHit.Collider.Value, normal, time01);
                    return true;
                }

                hit = MotionHit.None;
                return false;
            }

            var center = start + desiredDelta * 0.5f;
            var queryRadius = distance * 0.5f + sweepRadius;

            _candidates.Clear();
            _world.OverlapSphere(new Sphere(center, queryRadius), in layerFilter, _candidates);

            var bestTime = float.PositiveInfinity;
            var bestCollider = default(ColliderId);
            var bestNormal = Vec3.Zero;

            for (var i = 0; i < _candidates.Count; i++)
            {
                var collider = _candidates[i];
                if (ShouldIgnore(collider, ignoredCollider, ignoreMask)) continue;

                if (!TryResolveHitTime(start, direction, distance, sweepRadius, collider, in layerFilter, out var time01, out var normal)) continue;
                if (time01 < bestTime)
                {
                    bestTime = time01;
                    bestCollider = collider;
                    bestNormal = normal;
                }
            }

            _candidates.Clear();

            if (bestCollider.Value <= 0)
            {
                hit = MotionHit.None;
                return false;
            }

            var clampedTime = MathUtil.Clamp01(bestTime);
            appliedDelta = desiredDelta * clampedTime;
            hit = new MotionHit(true, bestCollider.Value, bestNormal, clampedTime);
            return true;
        }

        public bool Overlap(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask)
        {
            var layerFilter = new LayerFilter(ResolveMask(obstacleMask));
            var ignoredCollider = ResolveIgnoredCollider(moverId);

            _sampleOverlaps.Clear();
            _world.OverlapSphere(new Sphere(position, MathUtil.Max(radius, 0.01f)), in layerFilter, _sampleOverlaps);

            for (var i = 0; i < _sampleOverlaps.Count; i++)
            {
                var collider = _sampleOverlaps[i];
                if (ShouldIgnore(collider, ignoredCollider, ignoreMask)) continue;

                _sampleOverlaps.Clear();
                return true;
            }

            _sampleOverlaps.Clear();
            return false;
        }

        public bool TryProjectToFree(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask, out Vec3 projectedPosition)
        {
            projectedPosition = position;
            if (!Overlap(moverId, in position, radius, obstacleMask, ignoreMask))
            {
                return true;
            }

            var found = false;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < ProjectionDirectionCount; i++)
            {
                var angle = (float)(Math.PI * 2.0 * i / ProjectionDirectionCount);
                var direction = new Vec3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle));
                if (!TryFindFreeAlongDirection(
                        moverId,
                        in position,
                        in direction,
                        radius,
                        obstacleMask,
                        ignoreMask,
                        out var candidate,
                        out var distance))
                {
                    continue;
                }

                if (found && distance >= bestDistance) continue;
                found = true;
                bestDistance = distance;
                projectedPosition = candidate;
            }

            return found;
        }

        public bool TryProjectToFreeDirectional(int moverId, in Vec3 from, in Vec3 to, float radius, int obstacleMask, int ignoreMask, out Vec3 projectedPosition)
        {
            // 终点本就空闲：无需投影。
            if (!Overlap(moverId, in to, radius, obstacleMask, ignoreMask))
            {
                projectedPosition = to;
                return true;
            }

            // 起点必须在墙外，作为出墙方向锚点；否则无法沿方向投影。
            if (Overlap(moverId, in from, radius, obstacleMask, ignoreMask))
            {
                projectedPosition = to;
                return false;
            }

            // 沿 to→from 二分（固定 16 步，确定性），找最近出墙点。lo=墙内侧(to)、hi=墙外侧(from)。
            const int steps = 16;
            var lo = 0f;
            var hi = 1f;
            for (var i = 0; i < steps; i++)
            {
                var mid = (lo + hi) * 0.5f;
                var p = new Vec3(
                    to.X + (from.X - to.X) * mid,
                    to.Y + (from.Y - to.Y) * mid,
                    to.Z + (from.Z - to.Z) * mid);
                if (Overlap(moverId, in p, radius, obstacleMask, ignoreMask))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            projectedPosition = new Vec3(
                to.X + (from.X - to.X) * hi,
                to.Y + (from.Y - to.Y) * hi,
                to.Z + (from.Z - to.Z) * hi);
            return true;
        }

        private bool TryFindFreeAlongDirection(
            int moverId,
            in Vec3 origin,
            in Vec3 direction,
            float radius,
            int obstacleMask,
            int ignoreMask,
            out Vec3 projectedPosition,
            out float projectedDistance)
        {
            projectedPosition = origin;
            projectedDistance = 0f;

            var insideDistance = 0f;
            var outsideDistance = ProjectionSearchStep;
            while (outsideDistance <= ProjectionMaxDistance)
            {
                var sample = origin + direction * outsideDistance;
                if (!Overlap(moverId, in sample, radius, obstacleMask, ignoreMask))
                {
                    for (var i = 0; i < ProjectionBinarySearchSteps; i++)
                    {
                        var middleDistance = (insideDistance + outsideDistance) * 0.5f;
                        var middle = origin + direction * middleDistance;
                        if (Overlap(moverId, in middle, radius, obstacleMask, ignoreMask))
                        {
                            insideDistance = middleDistance;
                        }
                        else
                        {
                            outsideDistance = middleDistance;
                        }
                    }

                    projectedDistance = outsideDistance;
                    projectedPosition = origin + direction * outsideDistance;
                    return true;
                }

                insideDistance = outsideDistance;
                outsideDistance += ProjectionSearchStep;
            }

            return false;
        }

        private bool TryResolveHitTime(
            in Vec3 start,
            in Vec3 direction,
            float distance,
            float moverRadius,
            ColliderId collider,
            in LayerFilter layerFilter,
            out float time01,
            out Vec3 normal)
        {
            const int samples = 10;
            time01 = 0f;
            normal = Vec3.Zero;

            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var point = start + direction * (distance * t);
                _sampleOverlaps.Clear();
                _world.OverlapSphere(new Sphere(point, moverRadius), in layerFilter, _sampleOverlaps);

                for (var j = 0; j < _sampleOverlaps.Count; j++)
                {
                    if (!_sampleOverlaps[j].Equals(collider)) continue;

                    time01 = t;
                    normal = -direction;
                    _sampleOverlaps.Clear();
                    return true;
                }
            }

            var ray = new Ray3(start, direction);
            if (_world.Raycast(ray, distance + moverRadius, in layerFilter, out var rayHit) && rayHit.Collider.Equals(collider))
            {
                time01 = distance > MathUtil.Epsilon ? MathUtil.Clamp01(rayHit.Distance / distance) : 0f;
                normal = rayHit.Normal.SqrMagnitude > MathUtil.Epsilon ? rayHit.Normal : -direction;
                return true;
            }

            return false;
        }

        private ColliderId ResolveIgnoredCollider(int moverId)
        {
            if (_actors == null) return default;
            if (!_actors.TryGet(moverId, out var entity) || entity == null) return default;
            return entity.hasCollisionId ? entity.collisionId.Value : default;
        }

        private static int ResolveMask(int mask)
        {
            return mask != 0 ? mask : -1;
        }

        private static bool ShouldIgnore(ColliderId collider, ColliderId moverCollider, int ignoreMask)
        {
            if (collider.Value <= 0) return true;
            if (moverCollider.Value > 0 && collider.Equals(moverCollider)) return true;
            return ignoreMask != 0 && (collider.Value & ignoreMask) != 0;
        }
    }
}
