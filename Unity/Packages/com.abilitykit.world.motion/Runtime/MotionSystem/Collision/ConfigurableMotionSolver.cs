using System;
using AbilityKit.Ability.Share.Common.MotionSystem.Constraints;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Collision
{
    public sealed class ConfigurableMotionSolver : IMotionSolver
    {
        public delegate MotionConstraints ConstraintsProvider(int moverId, in MotionState state, in MotionOutput input, float dt);

        private readonly IMotionCollisionWorld _world;
        private readonly ConstraintsProvider _constraints;

        public ConfigurableMotionSolver(IMotionCollisionWorld world, ConstraintsProvider constraints)
        {
            _world = world;
            _constraints = constraints;
        }

        public MotionSolveResult Solve(int id, in MotionState state, in MotionOutput input, float dt)
        {
            var constraints = MotionConstraints.Disabled;
            try
            {
                if (_constraints != null)
                {
                    constraints = _constraints.Invoke(id, in state, in input, dt);
                }
            }
            catch
            {
                constraints = MotionConstraints.Disabled;
            }

            var desiredDelta = input.DesiredDelta;

            if (constraints.Leash.Enable && constraints.Leash.Radius > 0f)
            {
                desiredDelta = ApplyLeash(in state.Position, in desiredDelta, constraints.Leash);
            }

            if (!constraints.Collision.Enable) return MotionSolveResult.NoHit(desiredDelta);

            if (_world == null) return MotionSolveResult.NoHit(desiredDelta);

            if (constraints.Collision.AllowPassThrough)
            {
                return MotionSolveResult.NoHit(desiredDelta);
            }

            var start = state.Position;
            var desired = desiredDelta;

            if (_world.Sweep(
                    moverId: id,
                    start: in start,
                    desiredDelta: in desired,
                    radius: constraints.Collision.Radius,
                    obstacleMask: constraints.Collision.ObstacleMask,
                    ignoreMask: constraints.Collision.IgnoreMask,
                    hit: out var hit,
                    appliedDelta: out var applied))
            {
                return new MotionSolveResult(applied, hit);
            }

            return MotionSolveResult.NoHit(desired);
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

            var dist = (float)global::System.Math.Sqrt(dist2);
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
