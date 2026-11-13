using System;
using System.Reflection;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime
{
    public static class TriggerRunnerPlanExtensions
    {
        private static readonly MethodInfo RegisterPlanAsMethod = typeof(TriggerRunnerPlanExtensions).GetMethod(nameof(RegisterPlanAs), BindingFlags.NonPublic | BindingFlags.Static);

        public static IDisposable RegisterPlan<TArgs, TCtx>(this TriggerRunner<TCtx> runner, EventKey<TArgs> key, in TriggerPlan<TArgs> plan)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            var trigger = new PlannedTrigger<TArgs, TCtx>(plan);
            return runner.Register(key, trigger, plan.Phase, plan.Priority);
        }

        public static IDisposable RegisterPlan<TCtx>(this TriggerRunner<TCtx> runner, int eventId, Type argsType, in TriggerPlan<object> plan)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (argsType == null) throw new ArgumentNullException(nameof(argsType));
            if (eventId == 0) throw new ArgumentException("Event id must be non-zero.", nameof(eventId));
            if (RegisterPlanAsMethod == null) throw new MissingMethodException(nameof(TriggerRunnerPlanExtensions), nameof(RegisterPlanAs));

            try
            {
                var mi = RegisterPlanAsMethod.MakeGenericMethod(argsType, typeof(TCtx));
                return (IDisposable)mi.Invoke(null, new object[] { runner, eventId, plan });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static IDisposable RegisterPlanAs<TArgs, TCtx>(TriggerRunner<TCtx> runner, int eventId, TriggerPlan<object> plan)
        {
            var typedPlan = plan.AsArgs<TArgs>();
            var key = new EventKey<TArgs>(eventId);
            return runner.RegisterPlan<TArgs, TCtx>(key, typedPlan);
        }
    }
}
