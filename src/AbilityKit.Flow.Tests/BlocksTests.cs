using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class BlocksTests
{
    // ---------- DoNode ----------

    [Fact]
    public void Do_without_tick_succeeds_immediately()
    {
        var node = new DoNode();
        var ctx = new FlowContext();
        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void Do_tick_running_propagates_and_delegates_fire()
    {
        var ctx = new FlowContext();
        var events = new List<string>();
        var node = new DoNode(
            onEnter: _ => events.Add("enter"),
            onTick: (_, _) => FlowStatus.Running,
            onExit: _ => events.Add("exit"),
            onInterrupt: _ => events.Add("interrupt"));

        node.Enter(ctx);
        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
        node.Interrupt(ctx);

        Assert.Equal(new[] { "enter", "interrupt" }, events);
    }

    // ---------- IfNode ----------

    [Fact]
    public void If_true_runs_then_branch()
    {
        var ctx = new FlowContext();
        var then = new RecordingNode(FlowStatus.Succeeded);
        var els = new RecordingNode(FlowStatus.Succeeded);
        var node = new IfNode(_ => true, then, els);

        node.Enter(ctx);
        var s = node.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Succeeded, s);
        Assert.Equal(1, then.EnterCount);
        Assert.Equal(0, els.EnterCount);
    }

    [Fact]
    public void If_false_without_else_succeeds_without_entering_children()
    {
        var ctx = new FlowContext();
        var then = new RecordingNode(FlowStatus.Succeeded);
        var node = new IfNode(_ => false, then);

        node.Enter(ctx);
        var s = node.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Succeeded, s);
        Assert.Equal(0, then.EnterCount);
    }

    [Fact]
    public void If_false_with_else_runs_else_branch()
    {
        var ctx = new FlowContext();
        var then = new RecordingNode(FlowStatus.Failed);
        var els = new RecordingNode(FlowStatus.Succeeded);
        var node = new IfNode(_ => false, then, els);

        node.Enter(ctx);
        var s = node.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Succeeded, s);
        Assert.Equal(0, then.EnterCount);
        Assert.Equal(1, els.EnterCount);
    }

    [Fact]
    public void If_branch_selected_at_enter_not_reevaluated_per_tick()
    {
        var ctx = new FlowContext();
        var flag = true;
        var then = new RecordingNode(FlowStatus.Running);
        var node = new IfNode(_ => flag, then);

        node.Enter(ctx);
        flag = false;

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
        Assert.Equal(1, then.EnterCount);
    }

    [Fact]
    public void If_child_status_propagates()
    {
        var ctx = new FlowContext();
        var then = new RecordingNode(FlowStatus.Failed);
        var node = new IfNode(_ => true, then);

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Failed, node.Tick(ctx, 0f));
    }

    [Fact]
    public void If_interrupt_forwards_to_active_child()
    {
        var ctx = new FlowContext();
        var running = new RecordingNode(FlowStatus.Running);
        var node = new IfNode(_ => true, running);

        node.Enter(ctx);
        node.Tick(ctx, 0f);
        node.Interrupt(ctx);

        Assert.Equal(1, running.InterruptCount);
        Assert.Equal(0, running.ExitCount);
    }

    [Fact]
    public void If_null_predicate_or_then_throws()
    {
        var child = new RecordingNode();
        Assert.Throws<ArgumentNullException>(() => new IfNode(null, child));
        Assert.Throws<ArgumentNullException>(() => new IfNode(_ => true, null));
    }

    // ---------- ParallelAllNode ----------

    [Fact]
    public void ParallelAll_enters_all_children_on_enter()
    {
        var ctx = new FlowContext();
        var a = new RecordingNode(FlowStatus.Running);
        var b = new RecordingNode(FlowStatus.Running);
        var node = new ParallelAllNode(a, b);

        node.Enter(ctx);

        Assert.Equal(1, a.EnterCount);
        Assert.Equal(1, b.EnterCount);
    }

    [Fact]
    public void ParallelAll_succeeds_when_all_children_succeed()
    {
        var ctx = new FlowContext();
        var node = new ParallelAllNode(
            new RecordingNode(FlowStatus.Succeeded),
            new RecordingNode(FlowStatus.Succeeded));

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void ParallelAll_waits_for_all_children_even_after_failure()
    {
        // 已知语义（钉住）：单个子节点失败不会短路，父节点保持 Running
        // 直到所有子节点进入终态，其余子节点继续被 Tick。
        var ctx = new FlowContext();
        var failing = new RecordingNode(FlowStatus.Failed);
        var slow = new RecordingNode(FlowStatus.Running);
        var node = new ParallelAllNode(failing, slow);

        node.Enter(ctx);
        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
        Assert.Equal(1, failing.ExitCount);

        // 失败发生后，慢节点仍继续被 Tick。
        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
        Assert.Equal(2, slow.TickCount);

        slow.Result = FlowStatus.Succeeded;
        // 2026-08-17 修复：终态判定看全部子节点最终状态，早先轮次的失败不再被遗忘。
        Assert.Equal(FlowStatus.Failed, node.Tick(ctx, 0f));
        Assert.Equal(3, slow.TickCount);
    }

    [Fact]
    public void ParallelAll_failure_in_final_pass_fails_whole_node()
    {
        // 失败与最后一个子节点完成发生在同一轮 Tick 时，anyFailed 生效，整体 Failed。
        var ctx = new FlowContext();
        var node = new ParallelAllNode(
            new RecordingNode(FlowStatus.Failed),
            new RecordingNode(FlowStatus.Succeeded));

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Failed, node.Tick(ctx, 0f));
    }

    [Fact]
    public void ParallelAll_running_when_any_child_still_running()
    {
        var ctx = new FlowContext();
        var node = new ParallelAllNode(
            new RecordingNode(FlowStatus.Succeeded),
            new RecordingNode(FlowStatus.Running));

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
    }

    [Fact]
    public void ParallelAll_interrupt_cancels_running_children()
    {
        var ctx = new FlowContext();
        var done = new RecordingNode(FlowStatus.Succeeded);
        var running = new RecordingNode(FlowStatus.Running);
        var node = new ParallelAllNode(done, running);

        node.Enter(ctx);
        node.Tick(ctx, 0f);
        node.Interrupt(ctx);

        Assert.Equal(1, running.InterruptCount);
        Assert.Equal(0, done.InterruptCount);
    }

    [Fact]
    public void ParallelAll_empty_succeeds_on_first_tick()
    {
        var ctx = new FlowContext();
        var node = new ParallelAllNode();

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void ParallelAll_null_child_throws_on_enter()
    {
        var ctx = new FlowContext();
        var node = new ParallelAllNode(new IFlowNode[] { null });

        Assert.Throws<InvalidOperationException>(() => node.Enter(ctx));
    }

    // ---------- RaceNode ----------

    [Fact]
    public void Race_first_finisher_wins_and_interrupts_the_rest()
    {
        var ctx = new FlowContext();
        var winner = new RecordingNode(FlowStatus.Succeeded);
        var loser = new RecordingNode(FlowStatus.Running);
        var node = new RaceNode(winner, loser);

        node.Enter(ctx);
        var s = node.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Succeeded, s);
        Assert.Equal(1, winner.ExitCount);
        Assert.Equal(1, loser.InterruptCount);
        Assert.Equal(0, loser.ExitCount);
    }

    [Fact]
    public void Race_failed_winner_fails_whole_race()
    {
        var ctx = new FlowContext();
        var node = new RaceNode(
            new RecordingNode(FlowStatus.Failed),
            new RecordingNode(FlowStatus.Running));

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Failed, node.Tick(ctx, 0f));
    }

    [Fact]
    public void Race_same_tick_winner_prevents_loser_from_ticking()
    {
        var ctx = new FlowContext();
        var winner = new RecordingNode(FlowStatus.Succeeded);
        var wouldAlsoFinish = new RecordingNode(FlowStatus.Succeeded);
        var node = new RaceNode(winner, wouldAlsoFinish);

        node.Enter(ctx);
        node.Tick(ctx, 0f);

        // 后位子节点在同一轮被中断，不再 Tick。
        Assert.Equal(1, winner.TickCount);
        Assert.Equal(0, wouldAlsoFinish.TickCount);
        Assert.Equal(1, wouldAlsoFinish.InterruptCount);
    }

    [Fact]
    public void Race_running_when_no_child_finished()
    {
        var ctx = new FlowContext();
        var node = new RaceNode(new RecordingNode(FlowStatus.Running), new RecordingNode(FlowStatus.Running));

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
    }

    [Fact]
    public void Race_empty_completes_immediately()
    {
        // 2026-08-17 修复：空 Race 与空 Sequence/ParallelAll 一致，立即 Succeeded。
        var ctx = new FlowContext();
        var node = new RaceNode();

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void Race_null_child_throws_on_enter()
    {
        var ctx = new FlowContext();
        var node = new RaceNode(new IFlowNode[] { null });

        Assert.Throws<InvalidOperationException>(() => node.Enter(ctx));
    }

    // ---------- TimeoutNode ----------

    [Fact]
    public void Timeout_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutNode(-1f, new RecordingNode()));
    }

    [Fact]
    public void Timeout_child_completing_before_deadline_succeeds()
    {
        var ctx = new FlowContext();
        var child = new RecordingNode(FlowStatus.Succeeded);
        var node = new TimeoutNode(1f, child);

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0.5f));
        Assert.Equal(1, child.ExitCount);
    }

    [Fact]
    public void Timeout_interrupts_child_and_fails_on_deadline()
    {
        var ctx = new FlowContext();
        var child = new RecordingNode(FlowStatus.Running);
        var node = new TimeoutNode(0.5f, child);

        node.Enter(ctx);
        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0.25f));
        // 钉住边界：判定使用严格大于（elapsed > seconds），恰好到 0.5 不算超时。
        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0.25f));
        Assert.Equal(FlowStatus.Failed, node.Tick(ctx, 0.25f));

        Assert.Equal(1, child.InterruptCount);
        Assert.Equal(0, child.ExitCount);
    }

    [Fact]
    public void Timeout_elapsed_accumulates_from_enter()
    {
        var ctx = new FlowContext();
        var node = new TimeoutNode(1f, new RecordingNode(FlowStatus.Running));

        node.Enter(ctx);
        node.Tick(ctx, 0.6f);

        node.Enter(ctx); // 重新 Enter 重置计时

        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0.6f));
    }

    // ---------- SwitchNode ----------

    [Fact]
    public void Switch_dispatches_to_matched_case()
    {
        var ctx = new FlowContext();
        var aCase = new RecordingNode(FlowStatus.Succeeded);
        var bCase = new RecordingNode(FlowStatus.Succeeded);
        var node = new SwitchNode<string>(_ => "b", new Dictionary<string, IFlowNode> { ["a"] = aCase, ["b"] = bCase });

        node.Enter(ctx);
        node.Tick(ctx, 0f);

        Assert.Equal(1, bCase.EnterCount);
        Assert.Equal(0, aCase.EnterCount);
    }

    [Fact]
    public void Switch_missing_key_falls_back_to_default()
    {
        var ctx = new FlowContext();
        var fallback = new RecordingNode(FlowStatus.Succeeded);
        var node = new SwitchNode<string>(
            _ => "missing",
            new Dictionary<string, IFlowNode>(),
            defaultNode: fallback);

        node.Enter(ctx);
        node.Tick(ctx, 0f);

        Assert.Equal(1, fallback.EnterCount);
    }

    [Fact]
    public void Switch_missing_key_without_default_succeeds()
    {
        var ctx = new FlowContext();
        var node = new SwitchNode<string>(_ => "missing", new Dictionary<string, IFlowNode>());

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void Switch_case_running_propagates_and_interrupt_forwards()
    {
        var ctx = new FlowContext();
        var running = new RecordingNode(FlowStatus.Running);
        var node = new SwitchNode<string>(_ => "k", new Dictionary<string, IFlowNode> { ["k"] = running });

        node.Enter(ctx);
        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0f));
        node.Interrupt(ctx);

        Assert.Equal(1, running.InterruptCount);
    }

    // ---------- TickWhileNode ----------

    [Fact]
    public void TickWhile_ticks_while_condition_holds()
    {
        var ctx = new FlowContext();
        var ticks = new List<float>();
        var hold = true;
        var node = new TickWhileNode(_ => hold, (_, dt) => ticks.Add(dt));

        node.Enter(ctx);
        Assert.Equal(FlowStatus.Running, node.Tick(ctx, 0.1f));

        hold = false;
        // 条件为假的这一次 Tick：先判条件再决定是否回调，onTick 不再触发。
        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0.2f));
        Assert.Equal(new[] { 0.1f }, ticks);
    }

    [Fact]
    public void TickWhile_false_from_start_succeeds_without_any_tick()
    {
        var ctx = new FlowContext();
        var tickCount = 0;
        var node = new TickWhileNode(_ => false, (_, _) => tickCount++);

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
        Assert.Equal(0, tickCount);
    }

    [Fact]
    public void TickWhile_null_condition_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TickWhileNode(null, null));
    }

    // ---------- FinallyNode ----------

    [Fact]
    public void Finally_runs_finally_after_try_success_and_returns_try_status()
    {
        var ctx = new FlowContext();
        var order = new List<string>();
        var @try = new RecordingNode(FlowStatus.Succeeded) { OnEnter = _ => order.Add("try"), OnExit = _ => order.Add("try.exit") };
        var @finally = new RecordingNode(FlowStatus.Succeeded) { OnEnter = _ => order.Add("finally"), OnExit = _ => order.Add("finally.exit") };
        var node = new FinallyNode(@try, @finally);

        node.Enter(ctx);
        var s = node.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Succeeded, s);
        Assert.Equal(new[] { "try", "try.exit", "finally", "finally.exit" }, order);
    }

    [Fact]
    public void Finally_try_failure_propagates_after_finally_runs()
    {
        var ctx = new FlowContext();
        var @try = new RecordingNode(FlowStatus.Failed);
        var @finally = new RecordingNode(FlowStatus.Succeeded);
        var node = new FinallyNode(@try, @finally);

        node.Enter(ctx);
        var s = node.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Failed, s);
        Assert.Equal(1, @finally.EnterCount);
        Assert.Equal(1, @finally.ExitCount);
    }

    [Fact]
    public void Finally_running_finally_keeps_parent_running()
    {
        var ctx = new FlowContext();
        var @finally = new RecordingNode(FlowStatus.Running);
        var node = new FinallyNode(new RecordingNode(FlowStatus.Succeeded), @finally);

        node.Enter(ctx);
        var first = node.Tick(ctx, 0f);
        Assert.Equal(FlowStatus.Running, first);

        @finally.Result = FlowStatus.Succeeded;
        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void Finally_failure_status_is_swallowed()
    {
        // 钉住实际行为（可疑，见报告）：finally 子节点自身返回 Failed 时，
        // 其状态被丢弃，父节点仍返回 try 的状态。
        var ctx = new FlowContext();
        var node = new FinallyNode(new RecordingNode(FlowStatus.Succeeded), new RecordingNode(FlowStatus.Failed));

        node.Enter(ctx);

        Assert.Equal(FlowStatus.Succeeded, node.Tick(ctx, 0f));
    }

    [Fact]
    public void Finally_interrupt_while_try_running_skips_finally()
    {
        // 钉住实际行为（可疑，见报告）：try 还在 Running 时被中断，
        // finally 分支不会被 Enter 或执行。
        var ctx = new FlowContext();
        var @try = new RecordingNode(FlowStatus.Running);
        var @finally = new RecordingNode(FlowStatus.Succeeded);
        var node = new FinallyNode(@try, @finally);

        node.Enter(ctx);
        node.Tick(ctx, 0f);
        node.Interrupt(ctx);

        Assert.Equal(1, @try.InterruptCount);
        Assert.Equal(0, @finally.EnterCount);
        Assert.Equal(0, @finally.TickCount);
    }

    [Fact]
    public void Finally_interrupt_while_finally_running_interrupts_finally()
    {
        var ctx = new FlowContext();
        var @finally = new RecordingNode(FlowStatus.Running);
        var node = new FinallyNode(new RecordingNode(FlowStatus.Succeeded), @finally);

        node.Enter(ctx);
        node.Tick(ctx, 0f); // try 完成，finally 进入 Running
        node.Interrupt(ctx);

        Assert.Equal(1, @finally.InterruptCount);
    }

    // ---------- 资源节点 ----------

    [Fact]
    public void CreateResource_sets_value_at_enter_then_succeeds()
    {
        var ctx = new FlowContext();
        var resource = new object();
        var node = new CreateResourceNode<object>(_ => resource);

        node.Enter(ctx);
        var s = node.Tick(ctx, 0f);

        Assert.Equal(FlowStatus.Succeeded, s);
        Assert.Same(resource, ctx.Get<object>());
    }

    [Fact]
    public void CreateResource_second_enter_recreates()
    {
        // 2026-08-17 修复：同一节点实例第二次运行（新 scope）会重新创建并写入。
        var ctx = new FlowContext();
        var first = new object();
        var second = new object();
        var current = first;
        var node = new CreateResourceNode<object>(_ => current);

        node.Enter(ctx);
        node.Exit(ctx);
        using (ctx.BeginScope())
        {
            current = second;
            node.Enter(ctx);

            Assert.Same(second, ctx.Get<object>());
        }
    }

    [Fact]
    public void DisposeResource_disposes_and_removes_on_exit()
    {
        var ctx = new FlowContext();
        var resource = new object();
        var disposed = new List<object>();
        ctx.Set(resource);
        var node = new DisposeResourceNode<object>(disposed.Add);

        node.Enter(ctx);
        node.Tick(ctx, 0f);
        node.Exit(ctx);

        Assert.Same(resource, disposed[0]);
        Assert.False(ctx.TryGet<object>(out _));
    }

    [Fact]
    public void DisposeResource_interrupt_also_disposes()
    {
        var ctx = new FlowContext();
        var resource = new object();
        var disposed = new List<object>();
        ctx.Set(resource);
        var node = new DisposeResourceNode<object>(disposed.Add);

        node.Interrupt(ctx);

        Assert.Single(disposed);
        Assert.False(ctx.TryGet<object>(out _));
    }

    [Fact]
    public void DisposeResource_missing_value_is_noop()
    {
        var ctx = new FlowContext();
        var disposed = 0;
        var node = new DisposeResourceNode<object>(_ => disposed++);

        node.Enter(ctx);
        node.Exit(ctx);

        Assert.Equal(0, disposed);
    }

    [Fact]
    public void UseResource_creates_on_enter_and_disposes_on_exit()
    {
        var ctx = new FlowContext();
        var resource = new object();
        var disposed = new List<object>();
        var node = new UseResourceNode<object>(_ => resource, disposed.Add);

        node.Enter(ctx);
        Assert.Same(resource, ctx.Get<object>());

        node.Tick(ctx, 0f);
        node.Exit(ctx);

        Assert.Single(disposed, resource);
        Assert.False(ctx.TryGet<object>(out _));
    }

    [Fact]
    public void UseResource_interrupt_disposes()
    {
        var ctx = new FlowContext();
        var resource = new object();
        var disposed = new List<object>();
        var node = new UseResourceNode<object>(_ => resource, disposed.Add);

        node.Enter(ctx);
        node.Interrupt(ctx);

        Assert.Single(disposed, resource);
    }

    [Fact]
    public void UsingResource_body_sees_resource_and_result_propagates()
    {
        var ctx = new FlowContext();
        var resource = new object();
        object seenInBody = null;
        var body = new RecordingNode(FlowStatus.Failed) { BeforeTick = (c, _) => seenInBody = c.Get<object>() };
        var node = new UsingResourceNode<object>(_ => resource, _ => { }, body);

        node.Enter(ctx);
        var s = node.Tick(ctx, 0f);
        node.Exit(ctx);

        Assert.Equal(FlowStatus.Failed, s);
        Assert.Same(resource, seenInBody);
        Assert.False(ctx.TryGet<object>(out _));
    }

    [Fact]
    public void UsingResource_disposes_after_long_running_body()
    {
        var ctx = new FlowContext();
        var resource = new object();
        var disposed = new List<object>();
        var body = new RecordingNode(FlowStatus.Running);
        var node = new UsingResourceNode<object>(_ => resource, disposed.Add, body);

        node.Enter(ctx);
        node.Tick(ctx, 0f);

        Assert.Empty(disposed); // body 还在运行，资源仍存活

        body.Result = FlowStatus.Succeeded;
        node.Tick(ctx, 0f);

        // body 完成本身不触发释放；资源在节点 Exit（由 Runner/父级在终态调用）时释放。
        Assert.Empty(disposed);
        node.Exit(ctx);
        Assert.Single(disposed, resource);
    }

    [Fact]
    public void UsingResource_interrupt_interrupts_body_then_disposes()
    {
        var ctx = new FlowContext();
        var resource = new object();
        var disposed = new List<object>();
        var body = new RecordingNode(FlowStatus.Running);
        var node = new UsingResourceNode<object>(_ => resource, disposed.Add, body);

        node.Enter(ctx);
        node.Tick(ctx, 0f);
        node.Interrupt(ctx);

        Assert.Equal(1, body.InterruptCount);
        Assert.Single(disposed, resource);
        Assert.False(ctx.TryGet<object>(out _));
    }

    // ---------- AwaitCompletionNode / RunUntilCompletionNode ----------

    [Fact]
    public void AwaitCompletion_pending_then_succeeded()
    {
        var ctx = new FlowContext();
        var completion = new FlowCompletion();
        var node = new AwaitCompletionNode(_ => completion);

        using var runner = new FlowRunner(ctx);
        runner.Start(node);

        Assert.Equal(FlowStatus.Running, runner.Status);

        completion.Complete(true);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
    }

    [Fact]
    public void AwaitCompletion_failed_completion_fails()
    {
        var ctx = new FlowContext();
        var completion = new FlowCompletion();
        var node = new AwaitCompletionNode(_ => completion);

        using var runner = new FlowRunner(ctx);
        runner.Start(node);
        completion.Complete(false);

        Assert.Equal(FlowStatus.Failed, runner.Status);
    }

    [Fact]
    public void AwaitCompletion_exit_detaches_wakeUp()
    {
        var ctx = new FlowContext();
        var completion = new FlowCompletion();
        var node = new AwaitCompletionNode(_ => completion);

        using var runner = new FlowRunner(ctx);
        runner.Start(node);
        runner.Stop(); // Exit/Interrupt 路径应 DetachWakeUp

        // Detach 后 Complete 不再持有 runner 的 Wake，不会推进（也不会抛错）。
        completion.Complete(true);

        Assert.Equal(FlowStatus.Canceled, runner.Status);
    }

    [Fact]
    public void RunUntilCompletion_ticks_body_until_done()
    {
        var ctx = new FlowContext();
        var completion = new FlowCompletion();
        var ticks = 0;
        var node = new RunUntilCompletionNode(_ => completion, (_, _) => ticks++);

        using var runner = new FlowRunner(ctx);
        runner.Start(node);
        var tickedBeforeComplete = runner.Diagnostics.Statistics.NodesTicked;

        completion.Complete(true);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.True(ticks >= 1);
        Assert.True(tickedBeforeComplete >= 1);
    }

    [Fact]
    public void RunUntilCompletion_failed_completion_fails()
    {
        var ctx = new FlowContext();
        var completion = new FlowCompletion();
        var node = new RunUntilCompletionNode(_ => completion, null);

        using var runner = new FlowRunner(ctx);
        runner.Start(node);
        completion.Complete(false);

        Assert.Equal(FlowStatus.Failed, runner.Status);
    }

    // ---------- FlowGraph 工厂 ----------

    [Fact]
    public void FlowGraph_factories_execute_with_expected_semantics()
    {
        Assert.Equal(FlowStatus.Succeeded, FlowGraph.Empty().Execute().Status);

        var order = new List<string>();
        var seq = FlowGraph.Sequence(
            FlowGraph.Do(onEnter: _ => order.Add("a")),
            FlowGraph.Do(onEnter: _ => order.Add("b")));
        Assert.Equal(FlowStatus.Succeeded, seq.Execute().Status);
        Assert.Equal(new[] { "a", "b" }, order);

        // If：条件为假且无 else 直接成功。
        Assert.Equal(FlowStatus.Succeeded, FlowGraph.If(_ => false, FlowGraph.Do()).Execute().Status);

        // WaitSeconds：DeltaTime 不足以完成时被 MaxSteps 截断为 Canceled。
        var wait = FlowGraph.WaitSeconds(1f);
        var result = wait.Execute(new FlowExecutionOptions { DeltaTime = 0.1f, MaxSteps = 3 });
        Assert.Equal(FlowStatus.Canceled, result.Status);

        // Timeout：超时返回 Failed。
        var timeout = FlowGraph.Timeout(0f, new RecordingNode(FlowStatus.Running));
        var timeoutResult = timeout.Execute(new FlowExecutionOptions { DeltaTime = 0.1f });
        Assert.Equal(FlowStatus.Failed, timeoutResult.Status);
    }

    [Fact]
    public void FlowGraph_ParallelAll_and_Race_execute()
    {
        var par = FlowGraph.ParallelAll(FlowGraph.Do(), FlowGraph.Do());
        Assert.Equal(FlowStatus.Succeeded, par.Execute().Status);

        var race = FlowGraph.Race(FlowGraph.Do(), FlowGraph.WaitSeconds(10f));
        Assert.Equal(FlowStatus.Succeeded, race.Execute().Status);
    }
}
