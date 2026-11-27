using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Combat.Collision;

namespace AbilityKit.Combat.Projectile
{
    public sealed partial class ProjectileWorld
    {
        private struct ProjectileSweepState
        {
            public readonly bool ArcSweepPending;
            public readonly FixedVec3 ArcSweepStart;
            public readonly FixedVec3 ArcSweepEnd;
            public readonly Fixed64 ArcProgressStart;
            public readonly Fixed64 ArcProgressEnd;
            public readonly Fixed64 Move;
            public readonly FixedVec3 Direction;
            public FixedVec3 Position;

            public ProjectileSweepState(bool arcSweepPending, in FixedVec3 arcSweepStart, in FixedVec3 arcSweepEnd,
                Fixed64 arcProgressStart, Fixed64 arcProgressEnd, in FixedVec3 direction, Fixed64 move, in FixedVec3 position)
            {
                ArcSweepPending = arcSweepPending;
                ArcSweepStart = arcSweepStart;
                ArcSweepEnd = arcSweepEnd;
                ArcProgressStart = arcProgressStart;
                ArcProgressEnd = arcProgressEnd;
                Direction = direction;
                Move = move;
                Position = position;
            }
        }

        // 按当前运动段执行碰撞扫描，并按原有顺序应用命中状态变化。
        private bool SweepProjectile(Projectile p, int frame, Fixed64 fixedDeltaSeconds,
            List<ProjectileHitEvent> hitEvents, List<ProjectileExitEvent> exitEvents, ref ProjectileSweepState state)
        {
            var arcSweepPending = state.ArcSweepPending;
            var arcSweepStart = state.ArcSweepStart;
            var arcSweepEnd = state.ArcSweepEnd;
            var arcProgressStart = state.ArcProgressStart;
            var arcProgressEnd = state.ArcProgressEnd;
            var dir = state.Direction;
            var move = state.Move;
            var remaining = move;

            // 单帧内允许多次命中（穿透），同时保持确定性上限。
            const int maxHitsPerStep = 8;
            var hitCount = 0;
            var origin = p.Position;

            // 防止同一帧内对同一个碰撞体重复触发命中回调。
            // 这样可保留“返回过程可跨帧多次命中同一目标”的行为，
            // 同时避免单帧内多段射线检测造成重复触发。
            var hitColliderIdsThisTick = p.HitColliderIdsThisTick;
            Array.Clear(hitColliderIdsThisTick, 0, hitColliderIdsThisTick.Length);
            var hitColliderCount = 0;
            var skippedDistanceThisTick = Fixed64.Zero;
            if (p.IgnoreCollider.Value != 0)
            {
                hitColliderIdsThisTick[hitColliderCount++] = p.IgnoreCollider.Value;
            }

            while (remaining > Fixed64.Zero)
            {
                RaycastHit hit;
                Fixed64 sweptDistance;
                if (arcSweepPending)
                {
                    arcSweepPending = false;
                    if (_collisionQueries.TrySweepTrackingArcSegments(
                            arcSweepStart,
                            arcSweepEnd,
                            arcProgressStart,
                            arcProgressEnd,
                            p.TrackingArcHeight,
                            p.TrackingArcStartY,
                            p.TrackingArcEndY,
                            p.CollisionLayerMask,
                            hitColliderIdsThisTick,
                            hitColliderCount,
                            p.CollisionHalfExtents,
                            out hit,
                            out sweptDistance,
                            out var segmentDirection))
                    {
                        dir = segmentDirection;
                    }
                    else
                    {
                        origin = arcSweepEnd;
                        remaining = Fixed64.Zero;
                        break;
                    }
                }
                else if (!_collisionQueries.TrySweepSkippingIgnored(origin, dir, remaining, p.CollisionLayerMask, hitColliderIdsThisTick, hitColliderCount, p.CollisionHalfExtents, out hit))
                {
                    // 剩余线段内没有命中。
                    origin = origin + dir * remaining;
                    remaining = Fixed64.Zero;
                    break;
                }
                else
                {
                    sweptDistance = Fixed64.FromSingle(hit.Distance);
                }

                var cumulativeHitDistance = p.DistanceTravelled + skippedDistanceThisTick + sweptDistance;
                var elapsedAtHit = p.ElapsedSeconds + ProjectileCollisionQueries.EstimateElapsedWithinTick(
                    fixedDeltaSeconds, skippedDistanceThisTick + sweptDistance, move);
                var hitEvt = new ProjectileHitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, hit.Collider, hit.Distance, hit.Point, hit.Normal, frame, hitCount: 0,
                    spawnPosition: p.SpawnPosition.ToVec3(), distanceTravelled: cumulativeHitDistance.ToSingle(), elapsedFrames: Math.Max(0, frame - p.SpawnFrame), spawnFrame: p.SpawnFrame, elapsedSeconds: elapsedAtHit.ToSingle());
                var collisionResponse = ResolveCollisionResponse(p, hit.Collider, frame);

                if (collisionResponse == ProjectileCollisionResponse.Block)
                {
                    exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                    return true;
                }

                // 命中过滤和按碰撞体冷却。
                if (collisionResponse == ProjectileCollisionResponse.Ignore)
                {
                    skippedDistanceThisTick += sweptDistance + EpsilonAdvance;
                    if (hitColliderCount < hitColliderIdsThisTick.Length)
                    {
                        hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                    }

                    origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                    remaining -= sweptDistance + EpsilonAdvance;
                    hitCount++;
                    if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                    {
                        remaining = Fixed64.Zero;
                        break;
                    }
                    continue;
                }

                if (p.HitCooldownFrames > 0 && hit.Collider.Equals(p.LastHitCollider) && frame < p.LastHitAllowedFrame)
                {
                    skippedDistanceThisTick += sweptDistance + EpsilonAdvance;
                    if (hitColliderCount < hitColliderIdsThisTick.Length)
                    {
                        hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                    }

                    origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                    remaining -= sweptDistance + EpsilonAdvance;
                    hitCount++;
                    if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                    {
                        remaining = Fixed64.Zero;
                        break;
                    }
                    continue;
                }

                // Bounce and opt-in identity policies exclude previously accepted actors.
                // A target can own multiple colliders, and the next tracking arc starts at
                // the previous hit point, so suppress every collider that resolves to an
                // actor already in the bounce history before applying hit policy again.
                var candidateActorId = 0;
                var identityProvider = p.HitIdentityPolicy != ProjectileHitIdentityPolicy.Collider
                    ? _actorIdentityProvider : p.HitPolicyKind == ProjectileHitPolicyKind.Bounce ? _bounceTargetProvider : null;
                var hasActorIdentity = identityProvider != null &&
                    identityProvider.TryResolveActorId(hit.Collider, out candidateActorId) && candidateActorId > 0;
                var acceptedActors = p.HitIdentityPolicy == ProjectileHitIdentityPolicy.OncePerActorPerLeg && p.IsReturning
                    ? p.ReturnHitActorIds : p.HitActorIds;
                if (hasActorIdentity && acceptedActors.Contains(candidateActorId))
                {
                    skippedDistanceThisTick += sweptDistance + EpsilonAdvance;
                    if (hitColliderCount < hitColliderIdsThisTick.Length)
                    {
                        hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                    }

                    origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                    remaining -= sweptDistance + EpsilonAdvance;
                    hitCount++;
                    if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                    {
                        remaining = Fixed64.Zero;
                        break;
                    }
                    continue;
                }

                var alreadyHitThisTick = false;
                for (int hc = 0; hc < hitColliderCount; hc++)
                {
                    if (hitColliderIdsThisTick[hc] == hit.Collider.Value)
                    {
                        alreadyHitThisTick = true;
                        break;
                    }
                }

                if (alreadyHitThisTick)
                {
                    skippedDistanceThisTick += sweptDistance + EpsilonAdvance;
                    origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                    remaining -= sweptDistance + EpsilonAdvance;
                    hitCount++;
                    if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                    {
                        remaining = Fixed64.Zero;
                        break;
                    }
                    continue;
                }

                if (hitColliderCount < hitColliderIdsThisTick.Length)
                {
                    hitColliderIdsThisTick[hitColliderCount++] = hit.Collider.Value;
                }

                if (hasActorIdentity) acceptedActors.Add(candidateActorId);
                p.TotalHitCount++;
                var hitDistance = p.DistanceTravelled + skippedDistanceThisTick + sweptDistance;
                var elapsedAtAcceptedHit = p.ElapsedSeconds + ProjectileCollisionQueries.EstimateElapsedWithinTick(
                    fixedDeltaSeconds, skippedDistanceThisTick + sweptDistance, move);
                hitEvents?.Add(new ProjectileHitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, hit.Collider, hit.Distance, hit.Point, hit.Normal, frame, p.TotalHitCount,
                    p.SpawnPosition.ToVec3(), hitDistance.ToSingle(), Math.Max(0, frame - p.SpawnFrame), p.SpawnFrame, elapsedAtAcceptedHit.ToSingle()));
                if (p.HitCooldownFrames > 0)
                {
                    p.LastHitCollider = hit.Collider;
                    p.LastHitAllowedFrame = frame + p.HitCooldownFrames;
                }

                var hitsRemaining = p.HitsRemaining;
                var shouldExit = collisionResponse == ProjectileCollisionResponse.HitAndStop ||
                    collisionResponse != ProjectileCollisionResponse.HitAndContinue &&
                    (p.HitPolicy ?? ExitOnHitPolicy.Instance).ShouldExitOnHit(in hitEvt, ref hitsRemaining);
                p.HitsRemaining = hitsRemaining;

                if (shouldExit)
                {
                    exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                    return true;
                }

                // 命中后继续推进到命中点之后。
                if (p.HitPolicyKind == ProjectileHitPolicyKind.Bounce)
                {
                    if (_bounceTargetProvider == null ||
                        !_bounceTargetProvider.TryResolveActorId(hit.Collider, out var hitActorId) || hitActorId <= 0)
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                        return true;
                    }

                    if (!p.HitActorIds.Contains(hitActorId)) p.HitActorIds.Add(hitActorId);
                    if (!_bounceTargetProvider.TrySelectNextTarget(p.OwnerId, hitActorId, hit.Point, p.HitActorIds,
                            p.BounceSearchRadius.ToSingle(),
                            out var nextActorId, out var nextTargetPosition) ||
                        nextActorId <= 0 || p.HitActorIds.Contains(nextActorId))
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                        return true;
                    }

                    var nextDirection = nextTargetPosition.ToFixed() - hit.Point.ToFixed();
                    if (nextDirection.SqrMagnitude <= Fixed64.Zero)
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Hit, frame, hit.Point));
                        return true;
                    }

                    var fixedArcDuration = p.TrackingArcDuration;
                    var arcDurationConfigured = p.TrackingArcDurationConfigured;
                    var nextPlanar = new FixedVec3(
                        nextTargetPosition.X.ToFixed() - hit.Point.ToFixed().X,
                        Fixed64.Zero,
                        nextTargetPosition.Z.ToFixed() - hit.Point.ToFixed().Z);
                    p.TrackingTargetActorId = nextActorId;
                    p.TrackingArcDurationConfigured = arcDurationConfigured;
                    if (arcDurationConfigured)
                    {
                        p.TrackingArcDuration = fixedArcDuration;
                    }
                    else if (p.Speed > Fixed64.Zero)
                    {
                        p.TrackingArcDuration = nextPlanar.Magnitude / p.Speed;
                    }
                    else
                    {
                        p.TrackingArcDuration = Fixed64.Zero;
                    }
                    p.TrackingArcElapsed = Fixed64.Zero;
                    p.TrackingArcStartY = hit.Point.ToFixed().Y;
                    p.TrackingArcEndY = nextTargetPosition.Y.ToFixed();
                    p.TrackingArcHeight = p.TrackingArcBaseHeight + nextPlanar.Magnitude * p.TrackingArcHeightPerUnit;
                    p.Direction = nextDirection.Normalized;
                    dir = p.Direction;
                    arcSweepPending = false;
                    if (p.TrackingArcHeight > Fixed64.Zero)
                    {
                        origin = hit.Point.ToFixed();
                        remaining = Fixed64.Zero;
                        break;
                    }
                }

                origin = hit.Point.ToFixed() + dir * EpsilonAdvance;
                remaining -= sweptDistance + EpsilonAdvance;
                hitCount++;
                if (hitCount >= maxHitsPerStep || remaining <= Fixed64.Zero)
                {
                    // 避免无限循环，本帧停止处理。
                    remaining = Fixed64.Zero;
                    break;
                }
            }

            state.Position = origin;
            return false;
        }
    }
}
