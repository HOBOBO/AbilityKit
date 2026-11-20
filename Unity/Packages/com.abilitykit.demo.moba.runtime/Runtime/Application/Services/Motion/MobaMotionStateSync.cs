using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    internal static class MobaMotionStateSync
    {
        internal static void SyncFromTransform(global::ActorEntity entity, in Transform3 transform)
        {
            if (entity == null || !entity.hasMotion) return;

            var motion = entity.motion;
            var state = motion.State;
            state.Position = transform.Position;
            state.Forward = transform.Forward;
            var output = motion.Output;
            output.NewForward = state.Forward;
            entity.ReplaceMotion(motion.Pipeline, state, output, motion.Solver, motion.Policy,
                motion.Events, motion.Initialized, motion.HitTriggerRuntime);
        }
    }
}
