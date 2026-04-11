using AbilityKit.Core.Common.MotionSystem.Core;
using AbilityKit.Core.Common.MotionSystem.Generic;
using AbilityKit.Core.Common.MotionSystem.Trajectory;
using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Examples
{
    public static class WaypointTrajectoryExample
    {
        public static void Example_WaypointsAsTrajectory(int id)
        {
            var points = new[]
            {
                new Vec3(0f, 0f, 0f),
                new Vec3(2f, 0f, 1f),
                new Vec3(5f, 0f, 1f),
                new Vec3(7f, 0f, 3f),
            };

            var traj = new WaypointTrajectory3D(points, speed: 4f);

            var pipeline = new MotionPipeline();
            pipeline.AddSource(new TrajectoryMotionSource(traj));

            var state = new MotionState(points[0]);
            var output = new MotionOutput();

            var runner = new FixedStepRunner(0.02f);
            var steps = runner.Accumulate(0.5f);
            for (int i = 0; i < steps; i++)
            {
                pipeline.Tick(id, ref state, runner.ConsumeOneStep(), ref output);
            }
        }
    }
}
