using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Combat.MotionSystem.Trajectory;
using AbilityKit.Core.Mathematics;
using NUnit.Framework;

namespace AbilityKit.Combat.MotionSystem.Tests
{
    public sealed class MotionSystemTests
    {
        [Test]
        public void Pipeline_AdditiveSources_ComposeDesiredAndAppliedDelta()
        {
            var pipeline = new MotionPipeline();
            var state = new MotionState(Vec3.Zero);
            var output = new MotionOutput();

            var locomotion = new LocomotionMotionSource(2f, MotionInputSpace.World);
            locomotion.SetInput(1f, 0f);
            pipeline.AddSource(locomotion);
            pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(0f, 0f, 3f), 1f, 0, MotionGroups.Ability, MotionStacking.Additive));

            var result = pipeline.Tick(1, ref state, 0.5f, ref output);

            Assert.That(result.AppliedDelta.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.AppliedDelta.Z, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(state.Position.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(state.Position.Z, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void Pipeline_DefaultPolicy_AbilitySuppressesLocomotion()
        {
            var pipeline = new MotionPipeline { Policy = MotionPipelinePolicy.CreateDefault() };
            var state = new MotionState(Vec3.Zero);
            var output = new MotionOutput();

            var locomotion = new LocomotionMotionSource(10f, MotionInputSpace.World);
            locomotion.SetInput(1f, 0f);
            pipeline.AddSource(locomotion);
            pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(0f, 0f, 1f), 1f, 20, MotionGroups.Ability, MotionStacking.OverrideLowerPriority));

            pipeline.Tick(1, ref state, 1f, ref output);

            Assert.That(output.AppliedDelta.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(output.AppliedDelta.Z, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Pipeline_DefaultPolicy_ControlSuppressesLocomotionAndAbility()
        {
            var pipeline = new MotionPipeline { Policy = MotionPipelinePolicy.CreateDefault() };
            var state = new MotionState(Vec3.Zero);
            var output = new MotionOutput();

            var locomotion = new LocomotionMotionSource(10f, MotionInputSpace.World);
            locomotion.SetInput(1f, 0f);
            pipeline.AddSource(locomotion);
            pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(10f, 0f, 0f), 1f, 20, MotionGroups.Ability, MotionStacking.OverrideLowerPriority));
            pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(0f, 0f, 1f), 1f, 100, MotionGroups.Control, MotionStacking.OverrideLowerPriority));

            pipeline.Tick(1, ref state, 1f, ref output);

            Assert.That(output.AppliedDelta.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(output.AppliedDelta.Z, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Pipeline_DefaultPolicy_ControlAllowsPassiveDisplacementToCompose()
        {
            var pipeline = new MotionPipeline { Policy = MotionPipelinePolicy.CreateDefault() };
            var state = new MotionState(Vec3.Zero);
            var output = new MotionOutput();

            pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(0f, 0f, 1f), 1f, 100, MotionGroups.Control, MotionStacking.OverrideLowerPriority));
            pipeline.AddSource(new FixedDeltaMotionSource(new Vec3(2f, 0f, 0f), 1f, 0, MotionGroups.PassiveDisplacement, MotionStacking.Additive));

            pipeline.Tick(1, ref state, 1f, ref output);

            Assert.That(output.AppliedDelta.X, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(output.AppliedDelta.Z, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void TrajectorySnapshot_RestoresProgress()
        {
            var trajectory = new LinearTrajectory3D(Vec3.Zero, new Vec3(10f, 0f, 0f), 10f);
            var source = new TrajectoryMotionSource(trajectory);
            var state = new MotionState(Vec3.Zero);
            var delta = Vec3.Zero;

            source.Tick(1, ref state, 3f, ref delta);
            Assert.That(source.ExportSnapshot(out var snapshot), Is.True);

            var restored = new TrajectoryMotionSource(trajectory);
            Assert.That(restored.ImportSnapshot(in snapshot), Is.True);
            delta = Vec3.Zero;
            restored.Tick(1, ref state, 2f, ref delta);

            Assert.That(delta.X, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void TrajectorySource_CanOverrideActorCollisionForVerticalMotion()
        {
            var trajectory = new LinearTrajectory3D(Vec3.Zero, Vec3.Up, 1f);
            var source = new TrajectoryMotionSource(
                trajectory,
                collisionPolicy: MotionCollisionConstraints.Disabled,
                hasCollisionPolicy: true);
            var pipeline = new MotionPipeline();
            var state = new MotionState(Vec3.Zero);
            var output = new MotionOutput();
            pipeline.AddSource(source);

            pipeline.Tick(1, ref state, 0.5f, ref output);

            Assert.That(output.HasDominantCollisionPolicy, Is.True);
            Assert.That(output.DominantCollisionPolicy.Enable, Is.False);
            Assert.That(state.Position.Y, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void PathFollower_CopiesInputPoints()
        {
            var points = new[] { Vec3.Zero, new Vec3(10f, 0f, 0f) };
            var source = new PathFollowerMotionSource(points, 10f);
            points[1] = new Vec3(100f, 0f, 0f);

            var state = new MotionState(Vec3.Zero);
            var delta = Vec3.Zero;
            source.Tick(1, ref state, 1f, ref delta);

            Assert.That(delta.X, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void WaypointTrajectory_CopiesInputPoints()
        {
            var points = new[] { Vec3.Zero, new Vec3(10f, 0f, 0f) };
            var trajectory = new WaypointTrajectory3D(points, 10f);
            points[1] = new Vec3(100f, 0f, 0f);

            var p = trajectory.SamplePosition(1f);

            Assert.That(p.X, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void Solver_ProjectToNearestFree_UsesProjection()
        {
            var world = new StubWorld
            {
                OverlapResult = true,
                ProjectResult = true,
                ProjectedPosition = new Vec3(2f, 0f, 0f),
            };
            var solver = new ConfigurableMotionSolver(world, (int moverId, in MotionState state, in MotionOutput input, float dt) =>
                new MotionConstraints(
                    new MotionCollisionConstraints(true, false, MotionEndOverlapPolicy.ProjectToNearestFree, 0.5f, 0f, 1, 0),
                    MotionLeashConstraints.Disabled));

            var state = new MotionState(Vec3.Zero);
            var output = new MotionOutput { DesiredDelta = new Vec3(5f, 0f, 0f) };

            var result = solver.Solve(1, in state, in output, 0.1f);

            Assert.That(result.AppliedDelta.X, Is.EqualTo(2f).Within(0.0001f));
        }

        private sealed class StubWorld : IMotionCollisionWorld
        {
            public bool SweepResult;
            public bool OverlapResult;
            public bool ProjectResult;
            public Vec3 ProjectedPosition;

            public bool Sweep(int moverId, in Vec3 start, in Vec3 desiredDelta, float radius, int obstacleMask, int ignoreMask, out MotionHit hit, out Vec3 appliedDelta)
            {
                hit = SweepResult ? new MotionHit(true, 2, new Vec3(-1f, 0f, 0f), 0.5f) : MotionHit.None;
                appliedDelta = desiredDelta;
                return SweepResult;
            }

            public bool Overlap(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask)
            {
                return OverlapResult;
            }

            public bool TryProjectToFree(int moverId, in Vec3 position, float radius, int obstacleMask, int ignoreMask, out Vec3 projectedPosition)
            {
                projectedPosition = ProjectedPosition;
                return ProjectResult;
            }

            public bool TryProjectToFreeDirectional(int moverId, in Vec3 from, in Vec3 to, float radius, int obstacleMask, int ignoreMask, out Vec3 projectedPosition)
            {
                projectedPosition = ProjectedPosition;
                return ProjectResult;
            }
        }
    }
}
