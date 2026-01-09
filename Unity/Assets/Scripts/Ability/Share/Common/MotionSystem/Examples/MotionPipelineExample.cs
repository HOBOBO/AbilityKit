using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Common.MotionSystem.Trajectory;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Examples
{
    public static class MotionPipelineExample
    {
        public static void Example_LinearTrajectoryFixedStep(int id)
        {
            var start = new Vec3(0f, 0f, 0f);
            var end = new Vec3(10f, 0f, 0f);

            var traj = new LinearTrajectory3D(start, end, duration: 1.0f);

            var pipeline = new MotionPipeline();
            var locomotion = new LocomotionMotionSource(speed: 4.5f, space: MotionInputSpace.Local, priority: 0);
            locomotion.SetInput(0f, 1f);

            pipeline.AddSource(locomotion);
            pipeline.AddSource(new TrajectoryMotionSource(traj));

            var state = new MotionState(start);
            state.Forward = new Vec3(0f, 0f, 1f);
            var output = new MotionOutput();

            var runner = new FixedStepRunner(0.02f);
            var steps = runner.Accumulate(0.33f);
            for (int i = 0; i < steps; i++)
            {
                pipeline.Tick(id, ref state, runner.ConsumeOneStep(), ref output);
            }
        }
    }
}
