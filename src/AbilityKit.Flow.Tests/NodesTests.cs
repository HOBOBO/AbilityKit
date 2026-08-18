using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Nodes;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class NodesTests
{
    // ---------- ActionNode ----------

    [Fact]
    public void ActionNode_without_tick_succeeds_on_first_tick()
    {
        var node = new ActionNode();
        var ctx = new FlowContext();

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void ActionNode_delegates_receive_context_and_deltaTime()
    {
        var ctx = new FlowContext();
        FlowContext enterCtx = null, exitCtx = null, interruptCtx = null;
        FlowContext tickCtx = null;
        float tickDt = -1f;

        var node = new ActionNode(
            onEnter: c => enterCtx = c,
            onTick: (c, dt) => { tickCtx = c; tickDt = dt; return FlowStatus.Running; },
            onExit: c => exitCtx = c,
            onInterrupt: c => interruptCtx = c);

        node.Enter(ctx);
        node.Tick(ctx, 0.5f);
        node.Exit(ctx);
        node.Interrupt(ctx);

        Assert.Same(ctx, enterCtx);
        Assert.Same(ctx, tickCtx);
        Assert.Equal(0.5f, tickDt);
        Assert.Same(ctx, exitCtx);
        Assert.Same(ctx, interruptCtx);
    }

    [Fact]
    public void ActionNode_tick_result_propagates()
    {
        var node = new ActionNode(onTick: (_, _) => FlowStatus.Failed);
        var ctx = new FlowContext();

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Failed, node.Tick(ctx, 0f));
    }

    // ---------- SequenceNode ----------

    [Fact]
    public void Sequence_runs_instant_children_in_one_tick()
    {
        var ctx = new FlowContext();
        var calls = new List<string>();
        var seq = new SequenceNode(
            new ActionNode(onEnter: _ => calls.Add("a.enter"), onExit: _ => calls.Add("a.exit")),
            new ActionNode(onEnter: _ => calls.Add("b.enter"), onExit: _ => calls.Add("b.exit")));

        seq.Enter(ctx);
        var s = seq.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Succeeded, s);
        Assert.Equal(new[] { "a.enter", "a.exit", "b.enter", "b.exit" }, calls);
    }

    [Fact]
    public void Sequence_running_child_pauses_progress()
    {
        var ctx = new FlowContext();
        var first = new RecordingNode(FlowStatus.Running);
        var second = new RecordingNode(FlowStatus.Succeeded);
        var seq = new SequenceNode(first, second);

        seq.Enter(ctx);
        Assert.Equal(FlowStatus.Running, seq.Tick(ctx, 0f));
        Assert.Equal(0, second.EnterCount);

        first.Result = FlowStatus.Succeeded;
        Assert.Equal(FlowStatus.Succeeded, seq.Tick(ctx, 0f));
        Assert.Equal(1, second.EnterCount);
    }

    [Fact]
    public void Sequence_failed_child_stops_and_propagates()
    {
        var ctx = new FlowContext();
        var first = new RecordingNode(FlowStatus.Failed);
        var second = new RecordingNode(FlowStatus.Succeeded);
        var seq = new SequenceNode(first, second);

        seq.Enter(ctx);
        var s = seq.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Failed, s);
        Assert.Equal(0, second.EnterCount);
        // 失败子节点以 Exit 收尾（非 Interrupt）。
        Assert.Equal(1, first.ExitCount);
    }

    [Fact]
    public void Sequence_empty_succeeds_immediately()
    {
        var ctx = new FlowContext();
        var seq = new SequenceNode();

        seq.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, seq.Tick(ctx, 0f));
    }

    [Fact]
    public void Sequence_null_child_throws_on_tick()
    {
        var ctx = new FlowContext();
        var seq = new SequenceNode(new IFlowNode[] { null });

        seq.Enter(ctx);

        Assert.Throws<InvalidOperationException>(() => seq.Tick(ctx, 0f));
    }

    [Fact]
    public void Sequence_interrupt_forwards_to_running_child_only()
    {
        var ctx = new FlowContext();
        var first = new RecordingNode(FlowStatus.Succeeded);
        var running = new RecordingNode(FlowStatus.Running);
        var third = new RecordingNode(FlowStatus.Succeeded);
        var seq = new SequenceNode(first, running, third);

        seq.Enter(ctx);
        seq.Tick(ctx, 0f); // first 完成，running 处于运行中

        seq.Interrupt(ctx);

        Assert.Equal(1, running.InterruptCount);
        Assert.Equal(0, first.InterruptCount);
        Assert.Equal(0, third.InterruptCount);
    }

    [Fact]
    public void Sequence_exit_forwards_to_running_child()
    {
        var ctx = new FlowContext();
        var running = new RecordingNode(FlowStatus.Running);
        var done = new RecordingNode(FlowStatus.Succeeded);
        var seq = new SequenceNode(done, running);

        seq.Enter(ctx);
        seq.Tick(ctx, 0f);
        seq.Exit(ctx);

        Assert.Equal(1, running.ExitCount);
        Assert.Equal(1, done.ExitCount); // 已完成的子节点在自己的终态时已 Exit 一次，不再重复
        Assert.Equal(0, running.InterruptCount);
    }

    [Fact]
    public void Sequence_reenter_resumes_from_start()
    {
        // Enter 重置索引：同一个 Sequence 实例可复用于第二次执行。
        var ctx = new FlowContext();
        var child = new RecordingNode(FlowStatus.Succeeded);
        var seq = new SequenceNode(child);

        seq.Enter(ctx);
        seq.Tick(ctx, 0f);

        seq.Enter(ctx);
        var s = seq.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Succeeded, s);
        Assert.Equal(2, child.EnterCount);
        Assert.Equal(2, child.TickCount);
    }

    // ---------- WaitSecondsNode ----------

    [Fact]
    public void WaitSeconds_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaitSecondsNode(-0.1f));
    }

    [Fact]
    public void WaitSeconds_zero_succeeds_on_first_tick()
    {
        var ctx = new FlowContext();
        var node = new WaitSecondsNode(0f);
        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void WaitSeconds_accumulates_deltaTime_until_threshold()
    {
        var ctx = new FlowContext();
        var node = new WaitSecondsNode(0.5f);
        node.Enter(ctx);

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0.25f));
        // >= 判定：恰好到达阈值即成功。
        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0.25f));
    }

    [Fact]
    public void WaitSeconds_enter_resets_elapsed()
    {
        var ctx = new FlowContext();
        var node = new WaitSecondsNode(0.5f);
        node.Enter(ctx);
        node.Tick(ctx, 0.75f);

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0.25f));
    }

    // ---------- WaitUntilNode ----------

    [Fact]
    public void WaitUntil_null_predicate_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WaitUntilNode(null));
    }

    [Fact]
    public void WaitUntil_returns_running_until_predicate_true()
    {
        var ctx = new FlowContext();
        var flag = false;
        var node = new WaitUntilNode(c => flag);
        node.Enter(ctx);

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
        flag = true;
        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    // ---------- RepeatUntilNode ----------

    [Fact]
    public void RepeatUntil_null_until_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RepeatUntilNode(null, null));
    }

    [Fact]
    public void RepeatUntil_invokes_onTick_then_checks_condition()
    {
        var ctx = new FlowContext();
        var ticks = new List<float>();
        var count = 0;
        var node = new RepeatUntilNode((c, dt) => { ticks.Add(dt); count++; }, c => count >= 2);
        node.Enter(ctx);

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0.1f));
        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0.2f));
        Assert.Equal(new[] { 0.1f, 0.2f }, ticks);
    }

    [Fact]
    public void RepeatUntil_without_onTick_only_checks_condition()
    {
        var ctx = new FlowContext();
        var node = new RepeatUntilNode(null, _ => true);
        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    // ---------- WaitSecondsEventNode ----------

    [Fact]
    public void WaitSecondsEvent_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaitSecondsEventNode(-1f));
    }

    [Fact]
    public void WaitSecondsEvent_completes_after_elapsed_wall_time()
    {
        var ctx = new FlowContext();
        var node = new WaitSecondsEventNode(0.02f);
        node.Enter(ctx);

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));

        var deadline = Environment.TickCount64 + 5000;
        while (node.Tick(ctx, 0f) == FlowStatus.Running && Environment.TickCount64 < deadline)
        {
            Thread.Sleep(5);
        }

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
        node.Exit(ctx);
    }

    [Fact]
    public void WaitSecondsEvent_exit_disposes_timer_without_firing()
    {
        var ctx = new FlowContext();
        var node = new WaitSecondsEventNode(0.05f);
        node.Enter(ctx);
        node.Exit(ctx);
        node.Exit(ctx); // 二次 Exit 安全

        Thread.Sleep(120);

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
    }

    // ---------- AwaitCallbackNode ----------

    [Fact]
    public void AwaitCallback_enter_without_wakeUp_throws()
    {
        var ctx = new FlowContext();
        var node = new AwaitCallbackNode((c, complete) => new NoopDisposable());

        Assert.Throws<InvalidOperationException>(() => node.Enter(ctx));
    }

    [Fact]
    public void AwaitCallback_subscription_disposed_on_exit()
    {
        var ctx = new FlowContext();
        var disposable = new CountingDisposable();
        var node = new AwaitCallbackNode((c, complete) => disposable);
        using (var runner = new FlowRunner(ctx))
        {
            runner.Start(node);
            runner.Stop();
        }

        Assert.Equal(1, disposable.DisposeCount);
    }

    [Fact]
    public void AwaitCallback_failed_completion_fails_flow()
    {
        var ctx = new FlowContext();
        Action<bool> pending = null;
        var node = new AwaitCallbackNode((c, complete) => { pending = complete; return new NoopDisposable(); });
        using var runner = new FlowRunner(ctx);
        runner.Start(node);

        pending(false);

        Assert.Equal(FlowStatus.Failed, runner.Status);
    }

    [Fact]
    public void WaitSecondsEvent_zero_seconds_completes_inside_Start_without_reentrant_errors()
    {
        // 2026-08-17 修复：Step 增加重入守卫——Enter 中的同步 Wake 只登记唤醒，
        // 由外层步进结束后统一推进，不再嵌套 Tick null 根、不再产生假 NRE。
        var ctx = new FlowContext();
        var node = new WaitSecondsEventNode(0f);
        var unhandled = new List<Exception>();
        FlowStatus? finished = null;
        using var runner = new FlowRunner(ctx);
        runner.UnhandledException += unhandled.Add;
        runner.Start(node, s => finished = s, null);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.Equal(FlowStatus.Succeeded, finished);
        Assert.Empty(unhandled);
    }

    [Fact]
    public void AwaitCallback_synchronous_complete_during_subscribe_completes_once()
    {
        // 2026-08-17 修复：重入守卫使订阅委托内的同步 complete 不再触发第二次 Enter，
        // 首次完成不丢失，订阅只发生一次。
        var subscribeCount = 0;
        Action<bool> latest = null;
        var node = new AwaitCallbackNode((c, complete) =>
        {
            subscribeCount++;
            latest = complete;
            if (subscribeCount == 1) complete(true);
            return new NoopDisposable();
        });
        using var runner = new FlowRunner(new FlowContext());
        runner.Start(node);

        Assert.Equal(1, subscribeCount); // 不再有重入导致的第二次订阅
        Assert.Equal(FlowStatus.Succeeded, runner.Status); // 首次同步完成直接生效
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount;

        public void Dispose() => DisposeCount++;
    }
}
