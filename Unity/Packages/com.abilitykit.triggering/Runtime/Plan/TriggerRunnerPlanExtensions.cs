using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime
{
    public static class TriggerRunnerPlanExtensions
    {
        public static IDisposable RegisterPlan<TArgs, TCtx>(this TriggerRunner<TCtx> runner, EventKey<TArgs> key, in TriggerPlan<TArgs> plan)
            where TArgs : class
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            var trigger = new PlannedTrigger<TArgs, TCtx>(plan);
            return runner.Register(key, trigger, plan.Phase, plan.Priority);
        }
    }
}
