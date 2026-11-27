using AbilityKit.Core.Pooling;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Plan;
using System.Reflection;
using Xunit;
using RuntimeActionScheduler = AbilityKit.Triggering.Runtime.ActionScheduler.ActionScheduler;

namespace AbilityKit.Triggering.ActionScheduling.Tests;

public sealed class ActionSchedulerManagerTests
{
    [Fact]
    public void Adapter_exports_only_the_host_manager_and_pool_owner()
    {
        var exported = typeof(ActionSchedulerManager).Assembly.GetExportedTypes();
        Assert.Equal(new[] { typeof(ActionSchedulerManager), typeof(ActionSchedulerPools) }, exported.OrderBy(type => type.Name));
    }

    [Fact]
    public void Host_member_signatures_do_not_expose_scheduler_internals()
    {
        Assert.Equal(new[]
        {
            "Int32 SchedulerCount", "Int32 TotalActionCount", "Void Clear()",
            "Boolean RemoveScheduler(Int32)", "Void Update(Single, AbilityKit.Triggering.Runtime.Dispatcher.ITriggerDispatcherContext)",
            "Void .ctor(AbilityKit.Triggering.Runtime.ActionScheduler.ActionSchedulerPools)"
        }.OrderBy(signature => signature, StringComparer.Ordinal), PublicSignatures(typeof(ActionSchedulerManager)));
        Assert.Equal(new[] { "Void .ctor(System.String)", "Void Dispose()", "Void Prewarm()" }, PublicSignatures(typeof(ActionSchedulerPools)));
    }

    private static string[] PublicSignatures(Type type) => type
        .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(member => member is not MethodInfo { IsSpecialName: true })
        .Select(member => member.ToString()!)
        .OrderBy(signature => signature, StringComparer.Ordinal)
        .ToArray();

    private static readonly ActionCallPlan Delayed = new(
        default, 0, default, default, null, EActionScheduleMode.Delayed, 100f, 1, true, EActionExecutionPolicy.Immediate);

    [Fact]
    public void Prewarming_a_scheduler_pool_does_not_touch_another_scope_with_the_same_name()
    {
        using var first = new ActionSchedulerPools("test-battle", ActionSchedulerPoolProfile.Small);
        using var second = new ActionSchedulerPools("test-battle");
        var manager = new ActionSchedulerManager(second);
        var scheduler = manager.GetOrCreateScheduler(10);
        var calls = 0;
        scheduler.RegisterPlannedOrReplace(0, in Delayed, (_, _) => calls++, null!, null!);

        first.Prewarm();
        var firstInstances = first.Scope.GetPool(new PoolKey("triggering.action-instance"), () => new ActionInstance());
        var config = first.Profile.ActionInstanceConfig;
        Assert.Equal(config.DefaultCapacity + config.PrewarmCount, firstInstances.InactiveCount);
        Assert.Equal(0, firstInstances.ActiveCount);
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(first.Prewarm);

        Assert.Equal(1, manager.TotalActionCount);
        manager.Update(100f, null!);
        manager.Clear();
        Assert.Equal(1, calls);
        Assert.Equal(0, manager.TotalActionCount);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Queued_plan_blocks_same_executor_reentry_with_or_without_pools(bool pooled)
    {
        using var pools = pooled ? new ActionSchedulerPools() : null;
        var manager = new ActionSchedulerManager(pools!);
        var scheduler = manager.GetOrCreateScheduler(10);
        var plan = new ActionCallPlan(default, 0, default, default, null!, EActionScheduleMode.Delayed,
            100f, 1, true, EActionExecutionPolicy.Queued);
        QueuedActionExecutor executor = null!;
        var calls = 0;
        bool? reentryExecuted = null;
        var instance = scheduler.RegisterPlannedOrReplace(0, in plan, (_, _) =>
        {
            if (++calls == 1)
                reentryExecuted = executor.TryExecute(default, out _);
        }, null!, null!);
        executor = Assert.IsType<QueuedActionExecutor>(instance.Executor);

        manager.Update(100f, null!);

        Assert.Equal(1, calls);
        Assert.Equal(false, reentryExecuted);
        Assert.False(executor.IsQueued);
        Assert.Equal(EActionInstanceState.Completed, instance.State);
    }

    [Fact]
    public void Total_counts_across_schedulers_without_waiting_for_update()
    {
        var manager = new ActionSchedulerManager();
        var first = manager.GetOrCreateScheduler(10);
        var second = manager.GetOrCreateScheduler(20);
        Assert.Equal(0, manager.TotalActionCount);

        var calls = 0;
        first.Register(Delayed, (_, _) => calls++, null, null, null);
        second.RegisterOrReplace(0, Delayed, (_, _) => calls++, null, null, null);
        Assert.Equal(2, manager.TotalActionCount);

        second.RegisterOrReplace(0, Delayed, (_, _) => calls++, null, null, null);
        Assert.Equal(2, manager.TotalActionCount);

        manager.Update(1f, null);
        Assert.Equal(2, manager.TotalActionCount);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Total_includes_terminal_action_until_cleanup(bool interrupt)
    {
        var manager = new ActionSchedulerManager();
        var scheduler = manager.GetOrCreateScheduler(10);
        var plan = new ActionCallPlan(default);
        var instance = scheduler.Register(plan, (_, _) => { }, null, null, null);

        if (interrupt)
            instance.RequestInterrupt("测试中断");
        else
            manager.Update(1f, null);

        Assert.Equal(interrupt ? EActionInstanceState.Interrupted : EActionInstanceState.Completed, instance.State);
        Assert.Equal(1, manager.TotalActionCount);

        manager.Update(1f, null);

        Assert.Equal(0, manager.TotalActionCount);
        Assert.Null(manager.GetScheduler(10));
    }

    [Fact]
    public void Total_reflects_remove_and_clear_without_waiting_for_update()
    {
        var manager = new ActionSchedulerManager();
        manager.GetOrCreateScheduler(10).Register(Delayed, (_, _) => { }, null, null, null);
        manager.GetOrCreateScheduler(20).Register(Delayed, (_, _) => { }, null, null, null);
        manager.Update(1f, null);

        Assert.True(manager.RemoveScheduler(10));
        Assert.Equal(1, manager.TotalActionCount);

        manager.Clear();
        Assert.Equal(0, manager.TotalActionCount);
    }

    [Fact]
    public void Manager_rejects_reentry_and_group_changes_without_stopping_other_actions()
    {
        var manager = new ActionSchedulerManager();
        var first = manager.GetOrCreateScheduler(10);
        var second = manager.GetOrCreateScheduler(20);
        var plan = new ActionCallPlan(default);
        var order = new List<int>();
        var nested = new List<Exception?>();
        RuntimeActionScheduler? queried = null;

        first.Register(plan, (_, _) => order.Add(1), null!, null!, null!);
        second.Register(plan, (_, _) => order.Add(2), null!, null!, null!);
        var rejected = second.Register(plan, (_, _) =>
        {
            if (order.Contains(3)) return;
            order.Add(3);
            queried = manager.GetOrCreateScheduler(20);
            nested.Add(Record.Exception(() => manager.Update(500f, null!)));
            nested.Add(Record.Exception(() => { manager.GetOrCreateScheduler(30); }));
            nested.Add(Record.Exception(() => { manager.RemoveScheduler(10); }));
            manager.Clear();
        }, null!, null!, null!);

        manager.Update(1f, null!);

        Assert.Same(second, queried);
        Assert.Equal(3, nested.Count);
        Assert.All(nested, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Equal(new[] { 3, 2, 1 }, order);
        Assert.Equal(EActionInstanceState.Failed, rejected.State);
        Assert.Equal(1f, first.ElapsedMs);
        Assert.Equal(1f, second.ElapsedMs);
        Assert.Null(manager.GetScheduler(30));
        Assert.Equal(3, manager.TotalActionCount);

        manager.Update(1f, null!);
        Assert.Equal(0, manager.TotalActionCount);
        Assert.Equal(0, manager.SchedulerCount);

        manager.GetOrCreateScheduler(30).Register(plan, (_, _) => order.Add(4), null!, null!, null!);
        manager.Update(1f, null!);
        Assert.Equal(new[] { 3, 2, 1, 4 }, order);
        manager.Clear();
        Assert.Equal(0, manager.SchedulerCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Callback_cannot_replace_its_own_slot_before_the_old_execution_returns(bool pooled)
    {
        using var pools = pooled ? new ActionSchedulerPools() : null;
        var manager = new ActionSchedulerManager(pools!);
        var scheduler = manager.GetOrCreateScheduler(10);
        var plan = new ActionCallPlan(default);
        Exception? rejection = null;
        var calls = 0;
        var original = scheduler.RegisterOrReplace(0, plan, (_, _) =>
        {
            rejection = Record.Exception(() =>
            {
                scheduler.RegisterOrReplace(0, plan, (_, _) => calls++, null!, null!, null!);
            });
        }, null!, null!, null!);
        var originalId = original.InstanceId;

        manager.Update(1f, null!);

        Assert.IsType<InvalidOperationException>(rejection);
        Assert.Equal(EActionInstanceState.Completed, original.State);
        Assert.Null(original.InterruptReason);
        Assert.Equal(1, scheduler.ActionCount);

        var replacement = scheduler.RegisterOrReplace(0, plan, (_, _) => calls++, null!, null!, null!);
        Assert.Equal(originalId + 1, replacement.InstanceId);
        Assert.Equal(1, scheduler.ActionCount);

        manager.Update(1f, null!);
        Assert.Equal(1, calls);
        Assert.Equal(1, manager.TotalActionCount);
        manager.Update(1f, null!);
        Assert.Equal(0, manager.TotalActionCount);
    }

    [Fact]
    public void Direct_group_update_rejects_changes_before_registration_pipeline_runs()
    {
        using var pools = new ActionSchedulerPools();
        var manager = new ActionSchedulerManager(pools);
        var first = manager.GetOrCreateScheduler(10);
        var scheduler = manager.GetOrCreateScheduler(20);
        var last = manager.GetOrCreateScheduler(30);
        var plan = new ActionCallPlan(default);
        var nested = new List<Exception?>();
        var entered = false;
        Action<object, ITriggerDispatcherContext> noop = (_, _) => { };
        first.Register(plan, noop, null!, null!, null!);
        last.Register(plan, noop, null!, null!, null!);
        var instance = scheduler.Register(plan, (_, _) =>
        {
            if (entered) return;
            entered = true;
            nested.Add(Record.Exception(() => scheduler.Update(500f, default)));
            nested.Add(Record.Exception(() => { scheduler.Register(plan, noop, null!, null!, null!); }));
            nested.Add(Record.Exception(() => ((IFlatActionScheduleGroup)scheduler).RegisterOrReplace(
                1, in plan, noop, null!, 0)));
            nested.Add(Record.Exception(() => { manager.RemoveScheduler(20); }));
            nested.Add(Record.Exception(() => manager.Clear()));
            nested.Add(Record.Exception(() => manager.Update(500f, null!)));
            nested.Add(Record.Exception(() => scheduler.Dispose()));
        }, null!, null!, null!);

        scheduler.Update(1f, default);

        Assert.Equal(7, nested.Count);
        Assert.All(nested, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Equal(EActionInstanceState.Completed, instance.State);
        Assert.Equal(1f, scheduler.ElapsedMs);
        Assert.Equal(0f, first.ElapsedMs);
        Assert.Equal(0f, last.ElapsedMs);
        Assert.Equal(3, manager.SchedulerCount);
        Assert.Equal(3, manager.TotalActionCount);
        Assert.True(manager.RemoveScheduler(20));
        manager.Clear();
    }

    [Fact]
    public void Manager_leaves_update_scope_when_host_context_throws()
    {
        var manager = new ActionSchedulerManager();
        var scheduler = manager.GetOrCreateScheduler(10);
        var context = new FailingDispatcherContext();

        Assert.Same(context.Failure, Record.Exception(() => manager.Update(1f, context)));
        Assert.Equal(0f, scheduler.ElapsedMs);
        Assert.True(manager.RemoveScheduler(10));
        manager.Update(1f, null!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_replacement_keeps_old_delay_and_slot(bool pooled)
    {
        using var pools = pooled ? new ActionSchedulerPools() : null;
        var scheduler = new RuntimeActionScheduler(10, pools!);
        var calls = 0;
        var original = scheduler.RegisterPlannedOrReplace(0, in Delayed, (_, _) => calls++, null!, null!);
        scheduler.Update(40f, default);

        var group = (IFlatActionScheduleGroup)scheduler;
        Assert.Throws<ArgumentNullException>(() => group.RegisterOrReplace(0, in Delayed, null!, null!, null!));
        Assert.Equal(1, scheduler.ActionCount);
        Assert.Equal(EActionInstanceState.WaitingDelay, original.State);
        Assert.Null(original.InterruptReason);
        scheduler.Update(59f, default);
        Assert.Equal(0, calls);
        scheduler.Update(1f, default);
        Assert.Equal(1, calls);
        scheduler.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Planned_replacement_returns_rentals_and_keeps_old_instance_on_pool_failure(bool failOnActionInstance)
    {
        using var pools = new ActionSchedulerPools();
        var failure = new InvalidOperationException("测试池租借失败");
        var fail = false;
        var instances = pools.Scope.GetPool(new PoolKey("triggering.action-instance"), () => new ActionInstance(),
            onGet: _ => { if (fail && failOnActionInstance) throw failure; });
        var defaults = pools.Scope.GetPool(new PoolKey("triggering.action-executor.default"), () => new DefaultActionExecutor());
        var retries = pools.Scope.GetPool(new PoolKey("triggering.action-executor.retry"), () => new RetryActionExecutor(),
            onGet: _ => { if (fail && !failOnActionInstance) throw failure; });
        var scheduler = new RuntimeActionScheduler(10, pools);
        var calls = 0;
        var original = scheduler.RegisterPlannedOrReplace(0, in Delayed, (_, _) => calls++, null!, null!);
        var retry = new ActionCallPlan(default, 0, default, default, null!, EActionScheduleMode.Delayed, 100f, 1, true, EActionExecutionPolicy.WithRetry);
        fail = true;

        var thrown = Record.Exception(() => ((IFlatActionScheduleGroup)scheduler).RegisterOrReplace(
            0, in retry, (_, _) => { }, null!, 0));

        Assert.Same(failure, thrown);
        Assert.Null(original.InterruptReason);
        Assert.Equal(1, scheduler.ActionCount);
        Assert.Equal(1, defaults.ActiveCount);
        Assert.Equal(0, retries.ActiveCount);
        Assert.Equal(1, instances.ActiveCount);

        scheduler.Update(100f, default);
        Assert.Equal(1, calls);
        scheduler.Update(1f, default);
        Assert.Equal(0, defaults.ActiveCount);
        Assert.Equal(0, instances.ActiveCount);
    }

    [Fact]
    public void Plan_slot_replacement_failure_does_not_undo_prior_slots()
    {
        using var pools = new ActionSchedulerPools();
        var scheduler = new RuntimeActionScheduler(10, pools);
        var group = (IFlatActionScheduleGroup)scheduler;
        var calls = new List<string>();
        group.RegisterOrReplace(0, in Delayed, (_, _) => calls.Add("旧槽 0"), null!, null!);
        group.RegisterOrReplace(1, in Delayed, (_, _) => calls.Add("旧槽 1"), null!, null!);

        group.RegisterOrReplace(0, in Delayed, (_, _) => calls.Add("新槽 0"), null!, null!);
        Assert.Throws<ArgumentNullException>(() => group.RegisterOrReplace(1, in Delayed, null!, null!, null!));

        Assert.Equal(2, scheduler.ActionCount);
        scheduler.Update(100f, default);
        Assert.Equal(new[] { "新槽 0", "旧槽 1" }, calls);
        scheduler.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Old_cancel_failure_does_not_remove_committed_replacement(bool pooled)
    {
        using var pools = pooled ? new ActionSchedulerPools() : null;
        var manager = new ActionSchedulerManager(pools!);
        var scheduler = manager.GetOrCreateScheduler(10);
        var failure = new InvalidOperationException("测试旧任务取消失败");
        Exception? reentrantClear = null;
        var heldDuringCancel = -1;
        var previous = new CallbackExecutor(() =>
        {
            heldDuringCancel = manager.TotalActionCount;
            reentrantClear = Record.Exception(manager.Clear);
            throw failure;
        });
        scheduler.RegisterOrReplace(0, Delayed, null!, null!, null!, previous);
        var calls = 0;

        var group = (IFlatActionScheduleGroup)scheduler;
        var thrown = Record.Exception(() => group.RegisterOrReplace(0, in Delayed, (_, _) => calls++, null!, null!));

        Assert.Same(failure, thrown);
        Assert.IsType<InvalidOperationException>(reentrantClear);
        Assert.Equal(1, heldDuringCancel);
        Assert.Equal(1, manager.TotalActionCount);
        manager.Update(100f, null!);
        Assert.Equal(1, calls);
        manager.Clear();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Update_failure_keeps_order_and_cleanup_timing_even_when_cancel_throws(bool pooled, bool cleanupThrows)
    {
        using var pools = pooled ? new ActionSchedulerPools() : null;
        var manager = new ActionSchedulerManager(pools!);
        var earlierGroup = manager.GetOrCreateScheduler(10);
        var laterGroup = manager.GetOrCreateScheduler(20);
        var plan = new ActionCallPlan(default);
        var executionError = new InvalidOperationException("测试动作执行失败");
        var cleanupError = new InvalidOperationException("测试失败清理异常");
        var calls = new List<string>();
        var executor = new CallbackExecutor(
            onCancel: () =>
            {
                calls.Add("失败清理");
                if (cleanupThrows) throw cleanupError;
            },
            onExecute: () =>
            {
                calls.Add("失败动作");
                throw executionError;
            });

        earlierGroup.Register(plan, (_, _) => calls.Add("其他分组"), null!, null!, null!);
        laterGroup.Register(plan, (_, _) => calls.Add("同组动作"), null!, null!, null!);
        var failedAction = laterGroup.Register(plan, null!, null!, null!, executor);

        manager.Update(1f, null!);

        var expectedOrder = new[] { "失败动作", "失败清理", "同组动作", "其他分组" };
        Assert.Equal(expectedOrder, calls);
        Assert.Equal(EActionInstanceState.Failed, failedAction.State);
        if (cleanupThrows)
        {
            Assert.StartsWith(executionError.Message, failedAction.InterruptReason);
            Assert.Contains(cleanupError.Message, failedAction.InterruptReason);
        }
        else
        {
            Assert.Equal(executionError.Message, failedAction.InterruptReason);
        }

        Assert.Equal(3, manager.TotalActionCount);

        manager.Update(1f, null!);

        Assert.Equal(expectedOrder, calls);
        Assert.Equal(0, manager.TotalActionCount);
        Assert.Equal(0, manager.SchedulerCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispose_cancels_active_interruptible_actions_and_cleans_up_after_error(bool pooled)
    {
        using var pools = pooled ? new ActionSchedulerPools() : null;
        var instancePool = pools?.Scope.GetPool(new PoolKey("triggering.action-instance"), () => new ActionInstance());
        var scheduler = new RuntimeActionScheduler(10, pools!);
        var calls = new List<string>();
        var failure = new InvalidOperationException("测试销毁取消失败");
        var nonInterruptible = new ActionCallPlan(default, 0, default, default, null!,
            EActionScheduleMode.Delayed, 100f, 1, false, EActionExecutionPolicy.Immediate);
        scheduler.RegisterOrReplace(0, Delayed, null!, null!, null!,
            new CallbackExecutor(() => calls.Add("活跃")));
        scheduler.RegisterOrReplace(1, Delayed, null!, null!, null!, new CallbackExecutor(() =>
        {
            calls.Add("取消失败");
            throw failure;
        }));
        scheduler.Register(nonInterruptible, null!, null!, null!, new CallbackExecutor(() => calls.Add("不可中断")));
        scheduler.Register(new ActionCallPlan(default), null!, null!, null!, new CallbackExecutor(() => calls.Add("已完成")));
        scheduler.Update(1f, default);
        var interrupted = scheduler.Register(Delayed, null!, null!, null!, new CallbackExecutor(() => calls.Add("显式中断")));
        interrupted.RequestInterrupt("测试中断");

        Assert.Same(failure, Record.Exception(scheduler.Dispose));

        var expected = new[] { "显式中断", "取消失败", "活跃" };
        Assert.Equal(expected, calls);
        Assert.Equal(0, scheduler.ActionCount);
        Assert.Throws<InvalidOperationException>(() => ((IFlatActionScheduleGroup)scheduler).RegisterOrReplace(
            0, in Delayed, (_, _) => { }, null!, null!));
        if (instancePool != null) Assert.Equal(0, instancePool.ActiveCount);
        scheduler.Dispose();
        Assert.Equal(expected, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Clear_cleans_all_groups_before_reporting_multiple_failures(bool pooled)
    {
        using var pools = pooled ? new ActionSchedulerPools() : null;
        var manager = new ActionSchedulerManager(pools!);
        var failureA = new InvalidOperationException("测试分组 A 清理失败");
        var failureB = new InvalidOperationException("测试分组 B 清理失败");
        var calls = new List<int>();
        Exception? reentrantCreate = null;
        var first = manager.GetOrCreateScheduler(10);
        var second = manager.GetOrCreateScheduler(20);
        var third = manager.GetOrCreateScheduler(30);
        first.Register(Delayed, null!, null!, null!, new CallbackExecutor(() =>
        {
            calls.Add(10);
            reentrantCreate = Record.Exception(() => { manager.GetOrCreateScheduler(40); });
            throw failureA;
        }));
        second.Register(Delayed, null!, null!, null!, new CallbackExecutor(() =>
        {
            calls.Add(20);
            throw failureB;
        }));
        third.Register(Delayed, null!, null!, null!, new CallbackExecutor(() => calls.Add(30)));
        manager.Update(1f, null!);

        var thrown = Assert.Throws<AggregateException>(manager.Clear);

        Assert.Collection(thrown.InnerExceptions, error => Assert.Same(failureA, error), error => Assert.Same(failureB, error));
        Assert.IsType<InvalidOperationException>(reentrantCreate);
        Assert.Equal(new[] { 10, 20, 30 }, calls);
        Assert.Equal(0, first.ActionCount);
        Assert.Equal(0, second.ActionCount);
        Assert.Equal(0, third.ActionCount);
        Assert.Equal(0, manager.SchedulerCount);
        Assert.Equal(0, manager.TotalActionCount);
        Assert.NotSame(first, manager.GetOrCreateScheduler(10));
        manager.Clear();
    }

    [Fact]
    public void Remove_scheduler_detaches_failed_group_and_keeps_other_groups()
    {
        var manager = new ActionSchedulerManager();
        var removed = manager.GetOrCreateScheduler(10);
        var survivor = manager.GetOrCreateScheduler(20);
        var failure = new InvalidOperationException("测试移除分组失败");
        removed.Register(Delayed, null!, null!, null!, new CallbackExecutor(() => throw failure));
        int calls = 0;
        survivor.Register(new ActionCallPlan(default), (_, _) => calls++, null!, null!, null!);

        Assert.Same(failure, Record.Exception(() => { manager.RemoveScheduler(10); }));

        Assert.Null(manager.GetScheduler(10));
        Assert.Equal(0, removed.ActionCount);
        Assert.False(manager.RemoveScheduler(10));
        Assert.Same(survivor, manager.GetScheduler(20));
        manager.Update(1f, null!);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Returning_action_instance_to_pool_does_not_invoke_executor()
    {
        using var pools = new ActionSchedulerPools();
        var calls = 0;
        var executor = new CallbackExecutor(() => calls++);
        var instance = pools.RentActionInstance(1, 10, in Delayed, executor, null!, 0f, ownsExecutor: false);

        pools.ReleaseActionInstance(instance);

        Assert.Equal(0, calls);
        Assert.Null(instance.Executor);
    }

    private sealed class CallbackExecutor(Action onCancel, Action? onExecute = null) : IActionExecutor
    {
        public bool TryExecute(ActionExecutionContext ctx, out ExecutionResult result)
        {
            onExecute?.Invoke();
            result = ExecutionResult.Success();
            return true;
        }

        public void Cancel(string reason) => onCancel();
    }

    private sealed class FailingDispatcherContext : ITriggerDispatcherContext
    {
        public Exception Failure { get; } = new InvalidOperationException("测试宿主上下文读取失败");
        public object Context => throw Failure;
        public T GetService<T>() where T : class => null!;
        public float CurrentTimeMs => 0f;
    }
}
