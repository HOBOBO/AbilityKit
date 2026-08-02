using AbilityKit.Core.Logging;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public sealed class ActionCallTriggerPlanExecutable : TriggerPlanExecutableBase
    {
        private readonly ActionCallPlan _action;
        private readonly TriggerPlan<object> _plan;
        private readonly int _originalActionIndex;

        public ActionCallPlan Action => _action;

        public override string Name => "ActionCall";
        public override ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Action;

        public ActionCallTriggerPlanExecutable(ActionCallPlan action, int originalActionIndex = -1, ITriggerPlanCondition condition = null, float weight = 1f)
            : base(condition, weight)
        {
            _action = action;
            _originalActionIndex = originalActionIndex;
            _plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: 0, actions: new[] { _action });
        }

        protected override TriggerPlanExecutionResult ExecuteCore<TCtx>(object args, in ExecCtx<TCtx> ctx)
            where TCtx : class
        {
            var executor = new PlannedTriggerActionExecutor<object, TCtx>(in _plan);
            executor.Resolve(in ctx);
            executor.ExecuteWithScopeIndex(args, in ctx, 0, _originalActionIndex >= 0 ? _originalActionIndex : 0);
            if (ctx.Control != null && ctx.Control.IsActionRejected)
            {
                return TriggerPlanExecutionResult.Failed(
                    ctx.Control.ActionRejectReason ?? $"Action {_action.Id.Value} rejected");
            }

            return TriggerPlanExecutionResult.Success(_plan.Actions?.Length ?? 0);
        }
    }
}
