using AbilityKit.Core.Common.MotionSystem.Core;
using AbilityKit.Core.Common.MotionSystem.Generic;
using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Examples
{
    public static class GroupSuppressionExample
    {
        public static void Example_ControlSuppressLocomotion(int id)
        {
            var pipeline = new MotionPipeline
            {
                Policy = MotionPipelinePolicy.CreateDefault(),
            };

            var locomotion = new LocomotionMotionSource(speed: 4.5f, space: MotionInputSpace.Local, priority: 0);
            locomotion.SetInput(0f, 1f);
            pipeline.AddSource(locomotion);

            // Control source: for 0.3s, push backward and suppress locomotion by policy.
            var knockback = new FixedDeltaMotionSource(
                deltaPerSecond: new Vec3(0f, 0f, -6f),
                duration: 0.3f,
                priority: 100,
                groupId: MotionGroups.Control,
                stacking: MotionStacking.OverrideLowerPriority);

            pipeline.AddSource(knockback);

            var state = new MotionState(new Vec3(0f, 0f, 0f))
            {
                Forward = new Vec3(0f, 0f, 1f),
            };

            var output = new MotionOutput();

            // During knockback active window, locomotion will be suppressed.
            pipeline.Tick(id, ref state, 0.1f, ref output);
        }
    }
}
