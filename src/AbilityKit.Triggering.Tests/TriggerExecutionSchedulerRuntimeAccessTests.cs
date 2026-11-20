using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TriggerExecutionSchedulerRuntimeAccessTests
{
    [Fact]
    public void Progress_replays_next_due_once_in_order_and_restores_allocator()
    {
        var scheduler = new TriggerExecutionScheduler(retainFinishedEntries: true);
        var context = default(ExecCtx<object>);
        var calls = new List<string>();
        var first = scheduler.Schedule(new CallbackExecutable(() => calls.Add("first")), null!, in context,
            EScheduleMode.Periodic, 10f, 2, true);
        var interrupted = scheduler.Schedule(new CallbackExecutable(() => calls.Add("cancelled")), null!, in context,
            EScheduleMode.Timed, 10f, 1, true);
        var last = scheduler.Schedule(new CallbackExecutable(() => calls.Add("last")), null!, in context,
            EScheduleMode.Periodic, 10f, 2, true);
        var failing = scheduler.Schedule(new FailingExecutable(), null!, in context,
            EScheduleMode.Timed, 10f, 1, true);
        Assert.True(scheduler.Interrupt(in interrupted, "cancelled"));
        scheduler.Tick(10f);
        scheduler.Tick(4f);
        var checkpoint = scheduler.CaptureProgress();
        Assert.Equal(new[] { first.Id, interrupted.Id, last.Id, failing.Id }, checkpoint.Entries.Select(entry => entry.Id));
        Assert.Equal(TriggerScheduledExecutionState.Interrupted, checkpoint.Entries[1].State);
        Assert.Equal("cancelled", checkpoint.Entries[1].FailureReason);
        Assert.Equal(TriggerScheduledExecutionState.Failed, checkpoint.Entries[3].State);
        Assert.Equal("failed test execution", checkpoint.Entries[3].FailureReason);
        Assert.Equal(new[] { "first", "last" }, calls);
        calls.Clear();

        scheduler.Tick(6f);
        var expected = scheduler.CaptureProgress();
        Assert.Equal(TriggerScheduledExecutionState.Completed, expected.Entries[0].State);
        Assert.Equal(new[] { "first", "last" }, calls);
        var future = scheduler.Schedule(new CallbackExecutable(() => calls.Add("future")), null!, in context,
            EScheduleMode.Timed, 1f, 1, true);
        calls.Clear();

        scheduler.ValidateProgress(in checkpoint);
        scheduler.RestoreProgress(in checkpoint);
        Assert.Empty(calls);
        Assert.Equal(14d, checkpoint.Entries[0].ElapsedMs);
        Assert.Equal(10d, checkpoint.Entries[0].LastExecutionMs);
        Assert.Equal(1, checkpoint.Entries[0].ExecutionCount);
        Assert.Equal(2, scheduler.ActiveCount);
        Assert.False(scheduler.ContainsDefinition(future.Id));
        scheduler.Tick(5f);
        Assert.Empty(calls);
        scheduler.Tick(1f);
        Assert.Equal(new[] { "first", "last" }, calls);
        Assert.Equal(expected.Entries, scheduler.CaptureProgress().Entries);
        var reused = scheduler.Schedule(new CountingExecutable(), null!, in context, EScheduleMode.Timed, 1f, 1, true);
        Assert.Equal(future.Id, reused.Id);
    }

    [Fact]
    public void Invalid_progress_never_writes_or_removes_live_entries()
    {
        var scheduler = new TriggerExecutionScheduler(retainFinishedEntries: true);
        var context = default(ExecCtx<object>);
        var executable = new CountingExecutable();
        var handle = scheduler.Schedule(executable, null!, in context, EScheduleMode.Timed, 10f, 1, true);
        var later = scheduler.Schedule(executable, null!, in context, EScheduleMode.Timed, 20f, 1, true);
        scheduler.Tick(4f);
        var before = scheduler.CaptureProgress();
        var validFirst = new TriggerScheduledExecutionInfo(handle.Id, TriggerScheduledExecutionState.Completed, 10, 10, 1, "");
        var invalid = new[]
        {
            new TriggerExecutionProgress(0, before.Entries),
            new TriggerExecutionProgress(later.Id, before.Entries),
            new TriggerExecutionProgress(before.NextExecutionId, new[] { validFirst, validFirst }),
            new TriggerExecutionProgress(before.NextExecutionId + 1, new[] { validFirst,
                new TriggerScheduledExecutionInfo(before.NextExecutionId, TriggerScheduledExecutionState.Waiting, 0, 0, 0, "") }),
            new TriggerExecutionProgress(before.NextExecutionId, new[] { validFirst,
                new TriggerScheduledExecutionInfo(later.Id, TriggerScheduledExecutionState.Waiting, double.NaN, 0, 0, "") }),
        };

        foreach (var progress in invalid)
        {
            Assert.Throws<InvalidOperationException>(() => scheduler.ValidateProgress(in progress));
            Assert.Throws<InvalidOperationException>(() => scheduler.RestoreProgress(in progress));
            Assert.Equal(before.Entries, scheduler.GetExecutions());
            Assert.Equal(before.NextExecutionId, scheduler.NextExecutionId);
            Assert.Equal(2, scheduler.ActiveCount);
            Assert.Equal(0, executable.Count);
        }
    }

    [Fact]
    public void Removed_definition_cannot_be_revived_when_terminal_retention_is_disabled()
    {
        var scheduler = new TriggerExecutionScheduler();
        var context = default(ExecCtx<object>);
        var executable = new CountingExecutable();
        scheduler.Schedule(executable, null!, in context, EScheduleMode.Timed, 10f, 1, true);
        var checkpoint = scheduler.CaptureProgress();
        scheduler.Tick(10f);

        Assert.Throws<InvalidOperationException>(() => scheduler.RestoreProgress(in checkpoint));
        Assert.Empty(scheduler.GetExecutions());
        Assert.Equal(1, executable.Count);
    }

    [Theory]
    [InlineData(EScheduleMode.Timed)]
    [InlineData(EScheduleMode.External)]
    public void Callback_cannot_capture_or_restore_an_unsettled_execution(EScheduleMode mode)
    {
        var scheduler = new TriggerExecutionScheduler();
        var context = default(ExecCtx<object>);
        TriggerExecutionProgress checkpoint = default;
        var reached = false;
        var handle = scheduler.Schedule(new CallbackExecutable(() =>
        {
            Assert.Throws<InvalidOperationException>(() => scheduler.CaptureProgress());
            Assert.Throws<InvalidOperationException>(() => scheduler.RestoreProgress(in checkpoint));
            reached = true;
        }), null!, in context, mode, 5f, 1, true);
        checkpoint = scheduler.CaptureProgress();

        if (mode == EScheduleMode.External) Assert.True(scheduler.ExecuteExternal(in handle));
        else scheduler.Tick(5f);

        Assert.True(reached);
        Assert.Empty(scheduler.CaptureProgress().Entries);
    }

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

    private sealed class FailingExecutable : ITriggerPlanExecutable
    {
        public string Name => "failure";
        public ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Succeed;
        public float Weight => 1f;

        public TriggerPlanExecutionResult Execute<TCtx>(object args, in ExecCtx<TCtx> context)
            where TCtx : class => TriggerPlanExecutionResult.Failed("failed test execution");
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
