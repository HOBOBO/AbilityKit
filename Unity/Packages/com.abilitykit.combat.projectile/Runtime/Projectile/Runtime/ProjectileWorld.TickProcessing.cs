using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Combat.Projectile
{
    public sealed partial class ProjectileWorld
    {
        // 处理单个弹体的帧状态；活动列表遍历与对象池回收仍由 Tick 负责。
        private bool TickProjectile(int index, int frame, Fixed64 fixedDeltaSeconds, List<ProjectileHitEvent> hitEvents, List<ProjectileExitEvent> exitEvents, List<ProjectileTickEvent> tickEvents, List<ProjectileArrivalEvent> arrivalEvents)
        {
            var p = _active[index];
            if (p == null) return true;
            if (p.IsSuspended)
            {
                EmitTickIfDue(p, frame, tickEvents);
                return false;
            }

            if (p.LifetimeFramesLeft <= 0)
            {
                exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.Lifetime, frame, p.Position.ToVec3()));
                return true;
            }

            // 返回发射者逻辑（服务器权威）。MaxDistance 只约束出程，回程由 ReturnArrived/Lifetime 结束。
            if (!p.IsReturning && p.ReturnAfterFrames > 0 && frame - p.SpawnFrame >= p.ReturnAfterFrames)
            {
                p.IsReturning = true;
                p.DistanceLeft = Fixed64.Zero;
            }

            var returnDistance = Fixed64.Zero;
            var hasTrackingTarget = false;
            if (p.IsReturning)
            {
                if (_returnTargetProvider == null ||
                    !TryResolveReturnTarget(p.LauncherActorId, p.RootActorId, out var targetPos))
                {
                    exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.ReturnTargetLost, frame, p.Position.ToVec3()));
                    return true;
                }

                var returnTarget = targetPos.ToFixed();

                if (p.ReturnStopDistance >= Fixed64.Zero)
                {
                    var delta = returnTarget - p.Position;
                    var sqr = delta.SqrMagnitude;
                    var stopSqr = p.ReturnStopDistance * p.ReturnStopDistance;
                    if (sqr <= stopSqr)
                    {
                        exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.ReturnArrived, frame, p.Position.ToVec3()));
                        return true;
                    }
                }

                var to = returnTarget - p.Position;
                returnDistance = to.Magnitude;
                if (to.SqrMagnitude > Fixed64.Zero)
                {
                    p.Direction = to.Normalized;
                }
            }

            else if (p.TrackingTargetActorId > 0)
            {
                if (_trackingTargetProvider == null ||
                    !_trackingTargetProvider.TryGetTrackingTargetPosition(p.TrackingTargetActorId, out var targetPos))
                {
                    exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.TrackingTargetLost, frame, p.Position.ToVec3()));
                    return true;
                }

                var to = targetPos.ToFixed() - p.Position;
                hasTrackingTarget = true;
                to = new FixedVec3(to.X, Fixed64.Zero, to.Z);
                if (p.TrackingArcHeightPerUnit > Fixed64.Zero || p.TrackingArcBaseHeight > Fixed64.Zero)
                {
                    if (!p.TrackingArcDurationConfigured && p.TrackingArcDuration <= Fixed64.Zero && p.Speed > Fixed64.Zero)
                    {
                        p.TrackingArcStartY = p.Position.Y;
                        p.TrackingArcElapsed = Fixed64.Zero;
                        p.TrackingArcDuration = to.Magnitude / p.Speed;
                    }
                    p.TrackingArcEndY = targetPos.Y.ToFixed();
                    // Freeze one distance-scaled arc height for this segment. Target motion
                    // changes the endpoint, but does not make the arc height oscillate per tick.
                    if (p.TrackingArcElapsed <= Fixed64.Zero && p.TrackingArcHeightPerUnit > Fixed64.Zero)
                        p.TrackingArcHeight = p.TrackingArcBaseHeight + to.Magnitude * p.TrackingArcHeightPerUnit;
                }
                if (to.SqrMagnitude > Fixed64.Zero)
                {
                    p.Direction = to.Normalized;
                }
            }

            var speed = (p.IsReturning && p.ReturnSpeed > Fixed64.Zero) ? p.ReturnSpeed : p.Speed;
            var move = speed * fixedDeltaSeconds;
            var hasDistanceLimit = p.DistanceLeft > Fixed64.Zero;
            if (move <= Fixed64.Zero)
            {
                p.LifetimeFramesLeft--;
                return false;
            }

            var dir = p.Direction;
            var hasTrackingArc = hasTrackingTarget && p.TrackingArcHeight > Fixed64.Zero && p.TrackingArcDuration > Fixed64.Zero;
            var arcDeltaY = Fixed64.Zero;
            var arcProgressStart = Fixed64.Zero;
            var arcProgressEnd = Fixed64.Zero;
            if (hasTrackingArc)
            {
                arcProgressStart = p.TrackingArcElapsed / p.TrackingArcDuration;
                p.TrackingArcElapsed += fixedDeltaSeconds;
                var progress = p.TrackingArcElapsed / p.TrackingArcDuration;
                if (progress > Fixed64.One) progress = Fixed64.One;
                arcProgressEnd = progress;
                var currentArc = Fixed64.FromInt32(4) * p.TrackingArcHeight * progress * (Fixed64.One - progress);
                var currentY = p.TrackingArcStartY + (p.TrackingArcEndY - p.TrackingArcStartY) * progress + currentArc;
                arcDeltaY = currentY - p.Position.Y;
                var horizontalDirection = new FixedVec3(dir.X, Fixed64.Zero, dir.Z);
                if (horizontalDirection.SqrMagnitude > Fixed64.Zero)
                    dir = horizontalDirection.Normalized;
            }

            if (p.IsReturning && returnDistance > Fixed64.Zero && move > returnDistance) move = returnDistance;
            if (p.DistanceLeft > Fixed64.Zero && move > p.DistanceLeft)
            {
                move = p.DistanceLeft;
            }

            if (hasTrackingArc && move > Fixed64.Zero)
            {
                var horizontalStep = new FixedVec3(dir.X * move, Fixed64.Zero, dir.Z * move);
                var arcStep = new FixedVec3(horizontalStep.X, arcDeltaY, horizontalStep.Z);
                var arcDistance = arcStep.Magnitude;
                if (p.DistanceLeft > Fixed64.Zero && arcDistance > p.DistanceLeft)
                {
                    var budgetScale = p.DistanceLeft / arcDistance;
                    arcStep = arcStep * budgetScale;
                    arcDistance = p.DistanceLeft;
                }
                if (arcDistance > Fixed64.Zero)
                {
                    dir = arcStep / arcDistance;
                    move = arcDistance;
                    p.Direction = dir;
                }
            }

            var sweep = new ProjectileSweepState(
                hasTrackingArc && move > Fixed64.Zero,
                p.Position,
                p.Position + dir * move,
                arcProgressStart,
                arcProgressEnd,
                dir,
                move,
                p.Position);
            if (SweepProjectile(p, frame, fixedDeltaSeconds, hitEvents, exitEvents, ref sweep))
                return true;

            p.Position = sweep.Position;
            p.LifetimeFramesLeft--;
            p.ElapsedSeconds += fixedDeltaSeconds;

            // 移动后发送周期性 Tick 事件。
            EmitTickIfDue(p, frame, tickEvents);

            var travelled = (p.Position - sweep.ArcSweepStart).Magnitude;
            p.DistanceTravelled += travelled;
            if (hasDistanceLimit)
            {
                p.DistanceLeft -= travelled;
                if (p.DistanceLeft <= EpsilonAdvance)
                {
                    if (p.MaxDistanceResponse == ProjectileMaxDistanceResponse.ReturnToSource)
                    {
                        p.DistanceLeft = Fixed64.Zero;
                        p.ArrivedFrame = frame;
                        p.IsReturning = true;
                        return false;
                    }
                    if (p.MaxDistanceResponse == ProjectileMaxDistanceResponse.SuspendAtDestination)
                    {
                        p.DistanceLeft = Fixed64.Zero;
                        p.ArrivedFrame = frame;
                        p.IsSuspended = true;
                        arrivalEvents?.Add(new ProjectileArrivalEvent(p.Id, p.OwnerId, p.TemplateId,
                            p.LauncherActorId, p.RootActorId, frame, p.Position.ToVec3(), p.Direction.ToVec3()));
                        return false;
                    }
                    exitEvents?.Add(new ProjectileExitEvent(p.Id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, ProjectileExitReason.MaxDistance, frame, p.Position.ToVec3()));
                    return true;
                }
            }

                    return false;
        }
    }
}
