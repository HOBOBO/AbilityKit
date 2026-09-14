using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    internal static class MobaPlanActionOutput
    {
        public static bool IsConfigured(in BlackboardWriteTarget target)
        {
            return target.BoardId != 0 && target.KeyId != 0;
        }

        public static bool TryWrite<TCtx>(
            in ExecCtx<TCtx> ctx,
            in BlackboardWriteTarget target,
            double value,
            out string error)
        {
            if (!IsConfigured(in target))
            {
                error = null;
                return true;
            }
            return TriggerActionOutput.TryWrite(in ctx, in target, value, out error);
        }
    }
}
