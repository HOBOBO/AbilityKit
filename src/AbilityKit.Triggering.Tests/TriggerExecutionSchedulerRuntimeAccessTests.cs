using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TriggerExecutionSchedulerRuntimeAccessTests
{
    [Fact]
    public void Finished_entries_are_removed_by_default()
    {
        var scheduler = new TriggerExecutionScheduler();
        var context = default(ExecCtx<object>);
        scheduler.Schedule(new CountingExecutable(), null!, in context,
            EScheduleMode.Timed, 10f, 1, true);

        scheduler.Tick(10f);

        Assert.Equal(0, scheduler.ActiveCount);
        Assert.Empty(scheduler.GetExecutions());
        Assert.Equal(0, scheduler.DefinitionCount);
    }

    [Fact]
    public void Retained_entry_can_be_reactivated_through_progress_access()
    {
        var scheduler = new TriggerExecutionScheduler(retainFinishedEntries: true);
        var executable = new CountingExecutable();
        var context = default(ExecCtx<object>);
        scheduler.Schedule(executable, null!, in context,
            EScheduleMode.Periodic, 10f, 2, true);
        scheduler.Tick(10f);
        var progress = scheduler.GetExecutions()[0];
        scheduler.Tick(10f);
        Assert.Equal(0, scheduler.ActiveCount);

        scheduler.SetExecutionProgress(in progress);
        Assert.Equal(1, scheduler.ActiveCount);
        scheduler.Tick(10f);

        Assert.Equal(3, executable.Count);
        Assert.Equal(0, scheduler.ActiveCount);
        Assert.Single(scheduler.GetExecutions());
    }

    [Fact]
    public void Interrupt_during_tick_does_not_skip_later_execution()
    {
        var scheduler = new TriggerExecutionScheduler();
        var context = default(ExecCtx<object>);
        TriggerScheduledExecutionHandle first = default;
        first = scheduler.Schedule(new CallbackExecutable(() => scheduler.Interrupt(in first)),
            null!, in context, EScheduleMode.Timed, 10f, 1, true);
        var later = new CountingExecutable();
        scheduler.Schedule(later, null!, in context, EScheduleMode.Timed, 10f, 1, true);

        scheduler.Tick(10f);

        Assert.Equal(1, later.Count);
        Assert.Equal(0, scheduler.ActiveCount);
        Assert.Empty(scheduler.GetExecutions());
    }

    private sealed class CountingExecutable : ITriggerPlanExecutable
    {
        public int Count { get; private set; }
        public string Name => "counting";
        public ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Succeed;
        public float Weight => 1f;

        public TriggerPlanExecutionResult Execute<TCtx>(object args, in ExecCtx<TCtx> context)
            where TCtx : class
        {
            Count++;
            return TriggerPlanExecutionResult.Success();
        }
    }

    private sealed class CallbackExecutable : ITriggerPlanExecutable
    {
        private readonly Action _callback;
        public CallbackExecutable(Action callback) => _callback = callback;
        public string Name => "callback";
        public ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Succeed;
        public float Weight => 1f;

        public TriggerPlanExecutionResult Execute<TCtx>(object args, in ExecCtx<TCtx> context)
            where TCtx : class
        {
            _callback();
            return TriggerPlanExecutionResult.Success();
        }
    }
}
