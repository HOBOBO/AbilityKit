using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TriggerExecutionSchedulerRuntimeAccessTests
{
    private static readonly ActionId ApplyDeltaActionId = new(1);

    [Fact]
    public void Scheduled_plan_actions_replay_twice_with_explicit_host_state_restore()
    {
        var scheduler = new TriggerExecutionScheduler(retainFinishedEntries: true);
        var world = new ReplayState { Score = 10 };
        var context = CreateReplayContext(world, scheduler, totalTimeMs: 0f);
        ScheduleAction(new ReplayPayload(2), EScheduleMode.Timed, 1, in context);
        ScheduleAction(new ReplayPayload(1000), EScheduleMode.Timed, 1, in context);
        ScheduleAction(new ReplayPayload(3), EScheduleMode.Periodic, 2, in context);
        scheduler.Tick(4f);

        var checkpoint = scheduler.CaptureProgress();
        var checkpointScore = world.Score;
        var interrupted = new TriggerScheduledExecutionHandle(checkpoint.Entries[1].Id);
        Assert.Empty(world.Applied);

        void ReplayFrames()
        {
            Assert.True(scheduler.Interrupt(in interrupted, "cancelled by replay input"));
            scheduler.Tick(5f);
            Assert.Equal(10, world.Score);
            Assert.Empty(world.Applied);
            Assert.Equal(2, scheduler.ActiveCount);

            scheduler.Tick(1f);
            Assert.Equal(15, world.Score);
            Assert.Equal(new[] { (2, 0f), (3, 0f) }, world.Applied);
            Assert.Equal(1, scheduler.ActiveCount);

            scheduler.Tick(10f);
            Assert.Equal(18, world.Score);
            Assert.Equal(new[] { (2, 0f), (3, 0f), (3, 0f) }, world.Applied);
            Assert.Equal(0, scheduler.ActiveCount);
        }

        ReplayFrames();
        var expected = scheduler.CaptureProgress();
        Assert.Equal(TriggerScheduledExecutionState.Interrupted, expected.Entries[1].State);
        Assert.Equal("cancelled by replay input", expected.Entries[1].FailureReason);
        Assert.Equal(0, expected.Entries[1].ExecutionCount);

        for (var replayIndex = 0; replayIndex < 2; replayIndex++)
        {
            scheduler.ValidateProgress(in checkpoint);
            scheduler.RestoreProgress(in checkpoint);
            Assert.Equal(checkpoint.Entries, scheduler.GetExecutions());
            Assert.Equal(3, scheduler.ActiveCount);
            Assert.Equal(18, world.Score);
            Assert.Equal(new[] { (2, 0f), (3, 0f), (3, 0f) }, world.Applied);

            world.Score = checkpointScore;
            world.Applied.Clear();
            ReplayFrames();
            var actual = scheduler.CaptureProgress();
            Assert.Equal(expected.Entries, actual.Entries);
            Assert.Equal(expected.NextExecutionId, actual.NextExecutionId);
        }
    }

    [Fact]
    public void Scheduled_plan_copies_policy_values_but_keeps_payload_and_context_references()
    {
        var scheduler = new TriggerExecutionScheduler();
        var originalWorld = new ReplayState { Score = 1 };
        var replacementWorld = new ReplayState();
        var payload = new ReplayPayload(2);
        var context = CreateReplayContext(originalWorld, scheduler, totalTimeMs: 4f);
        ScheduleAction(payload, EScheduleMode.Timed, 1, in context);

        context = CreateReplayContext(replacementWorld, scheduler, totalTimeMs: 14f);
        payload.Delta = 7;
        originalWorld.Score = 9;
        scheduler.Tick(10f);

        Assert.Equal(new[] { (7, 4f) }, originalWorld.Applied);
        Assert.Equal(16, originalWorld.Score);
        Assert.Empty(context.Context.Applied);
        Assert.Equal(0, scheduler.ActiveCount);
    }

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
        Assert.Empty(calls);
        Assert.Equal(expected.Entries, scheduler.GetExecutions().Take(expected.Entries.Length));
        Assert.True(scheduler.ContainsDefinition(future.Id));
        Assert.Equal(future.Id + 1, scheduler.NextExecutionId);
        Assert.Equal(1, scheduler.ActiveCount);

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
            default(TriggerExecutionProgress),
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

    [Theory]
    [InlineData(255, 0d, 0d, 0)]
    [InlineData(0, -1d, 0d, 0)]
    [InlineData(0, double.PositiveInfinity, 0d, 0)]
    [InlineData(0, 0d, -1d, 0)]
    [InlineData(0, 0d, double.NaN, 0)]
    [InlineData(0, 0d, double.PositiveInfinity, 0)]
    [InlineData(0, 0d, 0d, -1)]
    public void Invalid_fields_are_rejected_by_snapshot_and_single_entry_access(
        int state, double elapsedMs, double lastExecutionMs, int count)
    {
        var scheduler = new TriggerExecutionScheduler(retainFinishedEntries: true);
        var context = default(ExecCtx<object>);
        var executable = new CountingExecutable();
        var handle = scheduler.Schedule(executable, null!, in context, EScheduleMode.Timed, 10f, 1, true);
        scheduler.Tick(4f);
        var before = scheduler.CaptureProgress();
        var invalid = new TriggerScheduledExecutionInfo(handle.Id, (TriggerScheduledExecutionState)state,
            elapsedMs, lastExecutionMs, count, "invalid");
        var checkpoint = new TriggerExecutionProgress(before.NextExecutionId, new[] { invalid });

        var validation = Assert.Throws<InvalidOperationException>(() => scheduler.ValidateProgress(in checkpoint));
        var restore = Assert.Throws<InvalidOperationException>(() => scheduler.RestoreProgress(in checkpoint));
        Assert.Equal(validation.Message, restore.Message);
        Assert.Contains("in-process definition", validation.Message);
        Assert.Equal("progress", Assert.Throws<ArgumentOutOfRangeException>(
            () => scheduler.SetExecutionProgress(in invalid)).ParamName);
        Assert.Equal(before.Entries, scheduler.GetExecutions());
        Assert.Equal(before.NextExecutionId, scheduler.NextExecutionId);
        Assert.Equal(1, scheduler.ActiveCount);
        Assert.Equal(0, executable.Count);
    }

    [Fact]
    public void Restore_revalidates_definitions_after_an_earlier_successful_preflight()
    {
        var scheduler = new TriggerExecutionScheduler(retainFinishedEntries: true);
        var context = default(ExecCtx<object>);
        var executable = new CountingExecutable();
        var first = scheduler.Schedule(executable, null!, in context, EScheduleMode.Timed, 10f, 1, true);
        var second = scheduler.Schedule(executable, null!, in context, EScheduleMode.Timed, 20f, 1, true);
        var checkpoint = new TriggerExecutionProgress(scheduler.NextExecutionId, new[]
        {
            new TriggerScheduledExecutionInfo(first.Id, TriggerScheduledExecutionState.Completed, 10, 10, 1, ""),
            new TriggerScheduledExecutionInfo(second.Id, TriggerScheduledExecutionState.Waiting, 4, 0, 0, ""),
        });

        scheduler.ValidateProgress(in checkpoint);
        Assert.True(scheduler.Remove(second.Id));
        var later = scheduler.Schedule(executable, null!, in context, EScheduleMode.Timed, 30f, 1, true);
        var beforeRestore = scheduler.CaptureProgress();

        Assert.Throws<InvalidOperationException>(() => scheduler.RestoreProgress(in checkpoint));

        var afterRestore = scheduler.CaptureProgress();
        Assert.Equal(beforeRestore.Entries, afterRestore.Entries);
        Assert.Equal(beforeRestore.NextExecutionId, afterRestore.NextExecutionId);
        Assert.True(scheduler.ContainsDefinition(later.Id));
        Assert.Equal(2, scheduler.ActiveCount);
        Assert.Equal(0, executable.Count);
    }

    [Fact]
    public void Restore_uses_live_definition_order_not_input_array_order()
    {
        var scheduler = new TriggerExecutionScheduler();
        var context = default(ExecCtx<object>);
        var calls = new List<string>();
        var first = scheduler.Schedule(new CallbackExecutable(() => calls.Add("first")), null!, in context,
            EScheduleMode.Timed, 10f, 1, true);
        var second = scheduler.Schedule(new CallbackExecutable(() => calls.Add("second")), null!, in context,
            EScheduleMode.Timed, 10f, 1, true);
        scheduler.Tick(4f);
        var checkpoint = scheduler.CaptureProgress();
        var reversed = new TriggerExecutionProgress(checkpoint.NextExecutionId, checkpoint.Entries.Reverse().ToArray());
        scheduler.Tick(2f);

        scheduler.RestoreProgress(in reversed);
        Assert.Empty(calls);
        Assert.Equal(new[] { first.Id, second.Id }, scheduler.GetExecutions().Select(entry => entry.Id));
        scheduler.Tick(5f);
        Assert.Empty(calls);
        scheduler.Tick(1f);
        Assert.Equal(new[] { "first", "second" }, calls);
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
            var capture = Assert.Throws<InvalidOperationException>(() => scheduler.CaptureProgress());
            var validation = Assert.Throws<InvalidOperationException>(() => scheduler.ValidateProgress(in checkpoint));
            var restore = Assert.Throws<InvalidOperationException>(() => scheduler.RestoreProgress(in checkpoint));
            Assert.Equal(capture.Message, validation.Message);
            Assert.Equal(capture.Message, restore.Message);
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

    [Theory]
    [InlineData(EScheduleMode.Timed)]
    [InlineData(EScheduleMode.Periodic)]
    public void Scheduled_node_without_host_scheduler_fails_without_executing_child(EScheduleMode mode)
    {
        var child = new CountingExecutable();
        var node = new ScheduledTriggerPlanExecutable(child, mode, intervalMs: 10f, maxExecutions: 1);
        var context = default(ExecCtx<ReplayState>);

        var result = node.Execute(new object(), in context);

        Assert.True(result.IsFailed);
        Assert.Contains(nameof(ITriggerExecutionScheduler), result.Reason);
        Assert.Equal(0, result.ExecutedCount);
        Assert.Equal(0, child.Count);
    }

    private static void ScheduleAction(ReplayPayload payload, EScheduleMode mode, int executions, in ExecCtx<ReplayState> context)
    {
        var action = new ActionCallTriggerPlanExecutable(new ActionCallPlan(ApplyDeltaActionId));
        var scheduled = new ScheduledTriggerPlanExecutable(action, mode, intervalMs: 10f, maxExecutions: executions);
        var result = scheduled.Execute(payload, in context);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExecutedCount);
    }

    private static ExecCtx<ReplayState> CreateReplayContext(ReplayState world, TriggerExecutionScheduler scheduler, float totalTimeMs)
    {
        var actions = new ActionRegistry();
        actions.Register<NamedAction0<object, object, ReplayState>>(ApplyDeltaActionId, static (args, _, context) =>
        {
            var delta = ((ReplayPayload)args).Delta;
            context.Context.Score += delta;
            context.Context.Applied.Add((delta, context.Policy.TotalTimeMs));
        }, isDeterministic: true);

        return new ExecCtx<ReplayState>(world, new EventBus(), new FunctionRegistry(), actions,
            blackboards: null!, payloads: null!, idNames: null!, numericDomains: null!, numericFunctions: null!,
            policy: new ExecPolicy(requireDeterministic: true, totalTimeMs: totalTimeMs), control: new ExecutionControl(),
            executionScheduler: scheduler);
    }

    private sealed class ReplayState
    {
        public int Score { get; set; }
        public List<(int Delta, float PolicyTotalTimeMs)> Applied { get; } = new();
    }

    private sealed class ReplayPayload(int delta)
    {
        public int Delta { get; set; } = delta;
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
