using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public abstract class TriggerPlanExecutableBase : ITriggerPlanExecutable
    {
        private readonly ITriggerPlanCondition _condition;

        public abstract string Name { get; }
        public abstract ETriggerPlanExecutableKind Kind { get; }
        public float Weight { get; }

        protected TriggerPlanExecutableBase(ITriggerPlanCondition condition = null, float weight = 1f)
        {
            _condition = condition;
            Weight = weight > 0f ? weight : 1f;
        }

        public TriggerPlanExecutionResult Execute<TCtx>(object args, in ExecCtx<TCtx> ctx)
            where TCtx : class
        {
            if (!CanExecute(args, in ctx))
                return TriggerPlanExecutionResult.Skipped($"{Name} condition not met");

            if (ShouldStop(in ctx))
                return TriggerPlanExecutionResult.Skipped($"{Name} cancelled");

            return ExecuteCore(args, in ctx);
        }

        protected abstract TriggerPlanExecutionResult ExecuteCore<TCtx>(object args, in ExecCtx<TCtx> ctx)
            where TCtx : class;

        protected static bool ShouldStop<TCtx>(in ExecCtx<TCtx> ctx)
            where TCtx : class
        {
            return ctx.Control != null && ctx.Control.IsPlanStopped;
        }

        private bool CanExecute<TCtx>(object args, in ExecCtx<TCtx> ctx)
            where TCtx : class
        {
            return _condition == null || _condition.Evaluate(args, in ctx);
        }
    }
}
