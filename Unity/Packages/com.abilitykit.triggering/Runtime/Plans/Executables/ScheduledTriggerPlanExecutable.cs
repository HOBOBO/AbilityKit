using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public sealed class ScheduledTriggerPlanExecutable : TriggerPlanExecutableBase
    {
        public ScheduledTriggerPlanExecutable(
            ITriggerPlanExecutable child,
            EScheduleMode scheduleMode,
            float intervalMs = 0f,
            int maxExecutions = -1,
            bool canBeInterrupted = true,
            ITriggerPlanCondition condition = null,
            float weight = 1f)
            : base(condition, weight)
        {
            Child = child;
            ScheduleMode = scheduleMode;
            IntervalMs = intervalMs;
            MaxExecutions = maxExecutions;
            CanBeInterrupted = canBeInterrupted;
        }

        public override string Name => $"Scheduled({ScheduleMode})";
        public override ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Scheduled;

        public ITriggerPlanExecutable Child { get; }
        public EScheduleMode ScheduleMode { get; }
        public float IntervalMs { get; }
        public int MaxExecutions { get; }
        public bool CanBeInterrupted { get; }

        protected override TriggerPlanExecutionResult ExecuteCore<TCtx>(object args, in ExecCtx<TCtx> ctx)
        {
            if (Child == null)
            {
                return TriggerPlanExecutionResult.Failed("Scheduled child is null");
            }

            if (ScheduleMode == EScheduleMode.Transient)
                return Child.Execute(args, in ctx);

            if (ctx.ExecutionScheduler == null)
                return TriggerPlanExecutionResult.Failed("Scheduled execution requires an injected ITriggerExecutionScheduler.");

            var handle = ctx.ExecutionScheduler.Schedule(
                Child,
                args,
                in ctx,
                ScheduleMode,
                IntervalMs,
                MaxExecutions,
                CanBeInterrupted);
            return handle.IsValid
                ? TriggerPlanExecutionResult.None
                : TriggerPlanExecutionResult.Failed("Scheduled execution registration failed.");
        }
    }
}
