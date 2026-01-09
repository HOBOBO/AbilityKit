using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Common.MotionSystem.Generic;
using AbilityKit.Ability.Share.Common.MotionSystem.Trajectory;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Examples
{
    public static class BlendPathLocomotionExample
    {
        public static void Example_PathWithInputSteering(int id)
        {
            var points = new[]
            {
                new Vec3(0f, 0f, 0f),
                new Vec3(5f, 0f, 0f),
                new Vec3(10f, 0f, 5f),
            };

            var traj = new WaypointTrajectory3D(points, speed: 4f);

            var pipeline = new MotionPipeline();

            // Path contributes as Ability group but additive, so it can be blended with locomotion.
            pipeline.AddSource(new TrajectoryMotionSource(traj, priority: 10, groupId: MotionGroups.Path, stacking: MotionStacking.Additive));

            var locomotion = new LocomotionMotionSource(speed: 4.5f, space: MotionInputSpace.Local, priority: 0);
            locomotion.SetInput(1f, 0f);
            // Input steering weight: 30%.
            pipeline.AddSource(new ScaledMotionSource(locomotion, scale: 0.3f));

            var state = new MotionState(points[0]);
            state.Forward = new Vec3(0f, 0f, 1f);
            var output = new MotionOutput();

            pipeline.Tick(id, ref state, 0.1f, ref output);
        }
    }
}
