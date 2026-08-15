using System;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.MotionSystem.Collision
{
    public sealed class ConfigurableMotionSolver : IMotionSolver
    {
        public delegate MotionConstraints ConstraintsProvider(int moverId, in MotionState state, in MotionOutput input, float dt);

        private readonly IMotionCollisionWorld _world;
        private readonly ConstraintsProvider _constraints;

        public ConfigurableMotionSolver(IMotionCollisionWorld world, ConstraintsProvider constraints, IMotionSolverDiagnostics diagnostics = null)
        {
            _world = world;
            _constraints = constraints;
            Diagnostics = diagnostics;
        }

        public IMotionSolverDiagnostics Diagnostics { get; set; }

        public MotionSolveResult Solve(int id, in MotionState state, in MotionOutput input, float dt)
        {
            var constraints = ResolveConstraints(id, in state, in input, dt);
            var desiredDelta = input.DesiredDelta;

            if (constraints.Leash.Enable && constraints.Leash.Radius > 0f)
            {
                desiredDelta = ApplyLeash(in state.Position, in desiredDelta, constraints.Leash);
            }

            var collision = input.HasDominantCollisionPolicy ? input.DominantCollisionPolicy : constraints.Collision;
            if (!collision.Enable) return MotionSolveResult.NoHit(desiredDelta);
            if (_world == null) return MotionSolveResult.NoHit(desiredDelta);

            return Resolve(id, in state.Position, in desiredDelta, in collision);
        }

        /// <summary>
        /// 共享解析核：供管线 <see cref="Solve"/> 与瞬移类技能（blink）直接调用。
        /// AllowPassThrough=true 时跳过行进 sweep（穿墙），但仍执行终点 <see cref="ResolveEndOverlap"/>
        /// （终点落障碍物内则按 <see cref="MotionEndOverlapPolicy"/> 处理，如 ProjectAlongDirection）。
        /// </summary>
        public MotionSolveResult Resolve(int moverId, in Vec3 start, in Vec3 desiredDelta, in MotionCollisionConstraints constraints)
        {
            if (!constraints.Enable) return MotionSolveResult.NoHit(desiredDelta);
            if (_world == null) return MotionSolveResult.NoHit(desiredDelta);

            Vec3 applied;
            MotionHit hit;
            if (constraints.AllowPassThrough)
            {
                applied = desiredDelta;
                hit = MotionHit.None;
            }
            else
            {
                applied = ResolveMovementWithSlide(moverId, in start, in constraints, in desiredDelta, out hit);
            }

            var endState = new MotionState(start);
            return ResolveEndOverlap(moverId, in endState, in constraints, in applied, in hit);
        }

        /// <summary>
        /// 沿 desiredDelta 推进，遇墙时按 <see cref="MotionCollisionConstraints.SlideAlongWalls"/>
        /// 决定单次钳制（丢弃切向分量）或迭代切向滑动。返回累积已通过位移与（若有）最后一次撞击。
        /// SlideAlongWalls=false 时等价于原有单次 Sweep 钳制行为。
        /// </summary>
        private Vec3 ResolveMovementWithSlide(
            int id,
            in Vec3 start,
            in MotionCollisionConstraints constraints,
            in Vec3 desiredDelta,
            out MotionHit hit)
        {
            hit = MotionHit.None;
            var maxIterations = constraints.SlideAlongWalls ? constraints.MaxSlideIterations : 1;

            var currentPos = start;
            var remaining = desiredDelta;
            var totalApplied = Vec3.Zero;
            var collided = false;

            for (int iteration = 0; iteration < maxIterations && remaining.SqrMagnitude > 1e-8f; iteration++)
            {
                if (!_world.Sweep(
                        moverId: id,
                        start: in currentPos,
                        desiredDelta: in remaining,
                        radius: constraints.Radius,
                        obstacleMask: constraints.ObstacleMask,
                        ignoreMask: constraints.IgnoreMask,
                        hit: out var stepHit,
                        appliedDelta: out var stepApplied))
                {
                    totalApplied += remaining;
                    break;
                }

                collided = true;
                hit = stepHit;
                totalApplied += stepApplied;

                if (!constraints.SlideAlongWalls) break;

                remaining -= stepApplied;

                // 切向滑动：消除剩余位移里指向墙法向（XZ）的分量，保留沿墙分量。
                var horizontalLengthBeforeProjection =
                    DeterministicMathBridge.Sqrt(remaining.X * remaining.X + remaining.Z * remaining.Z);
                var normal = stepHit.Normal;
                var normalSqr = normal.X * normal.X + normal.Z * normal.Z;
                if (normalSqr <= 1e-8f) break;
                var inverseLength = 1f / DeterministicMathBridge.Sqrt(normalSqr);
                var nx = normal.X * inverseLength;
                var nz = normal.Z * inverseLength;
                var intoWall = remaining.X * nx + remaining.Z * nz;
                if (intoWall < 0f)
                {
                    remaining = new Vec3(remaining.X - nx * intoWall, remaining.Y, remaining.Z - nz * intoWall);

                    var projectedHorizontalLength =
                        DeterministicMathBridge.Sqrt(remaining.X * remaining.X + remaining.Z * remaining.Z);
                    if (constraints.WallSlideSpeedRecovery > 0f && projectedHorizontalLength > 1e-6f)
                    {
                        var targetHorizontalLength = projectedHorizontalLength +
                            (horizontalLengthBeforeProjection - projectedHorizontalLength) *
                            constraints.WallSlideSpeedRecovery;
                        var horizontalScale = targetHorizontalLength / projectedHorizontalLength;
                        remaining = new Vec3(
                            remaining.X * horizontalScale,
                            remaining.Y,
                            remaining.Z * horizontalScale);
                    }
                }

                currentPos = start + totalApplied;
            }

            if (!collided) hit = MotionHit.None;
            return totalApplied;
        }

        private MotionConstraints ResolveConstraints(int id, in MotionState state, in MotionOutput input, float dt)
        {
            if (_constraints == null) return MotionConstraints.Disabled;

            try
            {
                return _constraints.Invoke(id, in state, in input, dt);
            }
            catch (Exception ex)
            {
                Diagnostics?.OnConstraintsProviderException(id, in state, in input, dt, ex);
                return MotionConstraints.Disabled;
            }
        }

        private MotionSolveResult ResolveEndOverlap(int id, in MotionState state, in MotionCollisionConstraints constraints, in Vec3 candidateDelta, in MotionHit hit)
        {
            var end = state.Position + candidateDelta;
            if (!_world.Overlap(id, in end, constraints.Radius, constraints.ObstacleMask, constraints.IgnoreMask))
            {
                return new MotionSolveResult(candidateDelta, hit);
            }

            switch (constraints.EndOverlapPolicy)
            {
                case MotionEndOverlapPolicy.AllowInside:
                    Diagnostics?.OnEndOverlapResolved(id, in state, in constraints, constraints.EndOverlapPolicy, true);
                    return new MotionSolveResult(candidateDelta, hit);

                case MotionEndOverlapPolicy.ProjectToNearestFree:
                    if (_world.TryProjectToFree(id, in end, constraints.Radius, constraints.ObstacleMask, constraints.IgnoreMask, out var projected))
                    {
                        var projectedDelta = projected - state.Position;
                        Diagnostics?.OnEndOverlapResolved(id, in state, in constraints, constraints.EndOverlapPolicy, true);
                        return new MotionSolveResult(projectedDelta, hit);
                    }

                    Diagnostics?.OnEndOverlapResolved(id, in state, in constraints, constraints.EndOverlapPolicy, false);
                    return MotionSolveResult.NoHit(Vec3.Zero);

                case MotionEndOverlapPolicy.ProjectAlongDirection:
                    if (_world.TryProjectToFreeDirectional(id, in state.Position, in end, constraints.Radius, constraints.ObstacleMask, constraints.IgnoreMask, out var directionallyProjected))
                    {
                        var projectedDelta = directionallyProjected - state.Position;
                        Diagnostics?.OnEndOverlapResolved(id, in state, in constraints, constraints.EndOverlapPolicy, true);
                        return new MotionSolveResult(projectedDelta, hit);
                    }

                    Diagnostics?.OnEndOverlapResolved(id, in state, in constraints, constraints.EndOverlapPolicy, false);
                    return MotionSolveResult.NoHit(Vec3.Zero);

                case MotionEndOverlapPolicy.ClampToLastValid:
                    Diagnostics?.OnEndOverlapResolved(id, in state, in constraints, constraints.EndOverlapPolicy, true);
                    return new MotionSolveResult(candidateDelta, hit);

                case MotionEndOverlapPolicy.Reject:
                default:
                    Diagnostics?.OnEndOverlapResolved(id, in state, in constraints, constraints.EndOverlapPolicy, false);
                    return MotionSolveResult.NoHit(Vec3.Zero);
            }
        }

        private static Vec3 ApplyLeash(in Vec3 start, in Vec3 desiredDelta, in MotionLeashConstraints leash)
        {
            var endX = start.X + desiredDelta.X;
            var endZ = start.Z + desiredDelta.Z;

            var dx = endX - leash.Center.X;
            var dz = endZ - leash.Center.Z;

            var dist2 = dx * dx + dz * dz;
            var r = leash.Radius;
            var r2 = r * r;
            if (dist2 <= r2)
            {
                return desiredDelta;
            }

            switch (leash.Policy)
            {
                case MotionLeashPolicy.Reject:
                    return Vec3.Zero;
                case MotionLeashPolicy.ClampToRadius:
                default:
                    break;
            }

            var dist = DeterministicMathBridge.Sqrt(dist2);
            if (dist <= 1e-6f)
            {
                return Vec3.Zero;
            }

            var s = r / dist;
            var clampedEndX = leash.Center.X + dx * s;
            var clampedEndZ = leash.Center.Z + dz * s;

            return new Vec3(clampedEndX - start.X, desiredDelta.Y, clampedEndZ - start.Z);
        }
    }
}
