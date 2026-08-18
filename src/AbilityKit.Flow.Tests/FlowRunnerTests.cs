using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class FlowRunnerTests
{
    private static FlowRunner NewRunner() => new FlowRunner(new FlowContext());

    [Fact]
    public void Start_null_root_throws_ArgumentNullException()
    {
        using var runner = NewRunner();

        Assert.Throws<ArgumentNullException>(() => runner.Start(null));
    }

    [Fact]
    public void Step_before_Start_returns_NotStarted()
    {
        using var runner = NewRunner();

        Assert.Equal(FlowStatus.NotStarted, runner.Step(0.1f));
    }

    [Fact]
    public void Start_instant_flow_completes_during_prime_step()
    {
        // Start 内部会先 Step(0) 预热，立即成功的节点在 Start 返回时已进入终态。
        using var runner = NewRunner();
        var node = new RecordingNode(FlowStatus.Succeeded);
        FlowStatus? finished = null;

        runner.Start(node, s => finished = s, null);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.Equal(FlowStatus.Succeeded, finished);
        Assert.Equal(1, node.EnterCount);
        Assert.Equal(1, node.TickCount);
        Assert.Equal(1, node.ExitCount);
    }

    [Fact]
    public void Start_sets_Running_when_flow_incomplete()
    {
        using var runner = NewRunner();
        var node = new RecordingNode(FlowStatus.Running);

        runner.Start(node);

        Assert.Equal(FlowStatus.Running, runner.Status);
        Assert.Equal(1, node.EnterCount);
        Assert.Equal(1, node.TickCount);
        Assert.Equal(0, node.ExitCount);
    }

    [Fact]
    public void Step_passes_deltaTime_to_root()
    {
        using var runner = NewRunner();
        var node = new RecordingNode(FlowStatus.Running);
        runner.Start(node);

        runner.Step(0.25f);
        runner.Step(0.75f);

        // 首次 Tick 来自 Start 的预热（dt=0），后续两次使用调用方 dt。
        Assert.Equal(new[] { 0f, 0.25f, 0.75f }, node.TickDeltaTimes);
    }

    [Fact]
    public void Step_completes_flow_and_exits_root_once()
    {
        using var runner = NewRunner();
        var node = new RecordingNode(FlowStatus.Running);
        runner.Start(node);

        node.Result = FlowStatus.Succeeded;
        var final = runner.Step(0f);

        Assert.Equal(FlowStatus.Succeeded, final);
        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.Equal(1, node.ExitCount);

        // 终态后再 Step 不会重复 Tick/Exit。
        Assert.Equal(FlowStatus.Succeeded, runner.Step(0f));
        Assert.Equal(2, node.TickCount);
        Assert.Equal(1, node.ExitCount);
    }

    [Fact]
    public void Step_propagates_Failed_from_root()
    {
        using var runner = NewRunner();
        var node = new RecordingNode(FlowStatus.Running);
        FlowStatus? finished = null;
        runner.Start(node, s => finished = s, null);

        node.Result = FlowStatus.Failed;
        runner.Step(0f);

        Assert.Equal(FlowStatus.Failed, runner.Status);
        Assert.Equal(FlowStatus.Failed, finished);
        // 正常结束走 Exit 而不是 Interrupt。
        Assert.Equal(1, node.ExitCount);
        Assert.Equal(0, node.InterruptCount);
    }

    [Fact]
    public void Stop_mid_run_interrupts_root_and_cancels()
    {
        using var runner = NewRunner();
        var node = new RecordingNode(FlowStatus.Running);
        FlowStatus? finished = null;
        runner.Start(node, s => finished = s, null);

        runner.Stop();

        Assert.Equal(FlowStatus.Canceled, runner.Status);
        Assert.Equal(FlowStatus.Canceled, finished);
        Assert.Equal(1, node.InterruptCount);
        Assert.Equal(0, node.ExitCount);
    }

    [Fact]
    public void Stop_before_Start_is_noop()
    {
        using var runner = NewRunner();

        runner.Stop();

        Assert.Equal(FlowStatus.NotStarted, runner.Status);
    }

    [Fact]
    public void Stop_after_finish_keeps_terminal_status()
    {
        using var runner = NewRunner();
        runner.Start(new RecordingNode(FlowStatus.Succeeded));
        var statusAfterFinish = runner.Status;

        runner.Stop();

        Assert.Equal(FlowStatus.Succeeded, statusAfterFinish);
        Assert.Equal(FlowStatus.Succeeded, runner.Status);
    }

    [Fact]
    public void Restart_mid_run_cancels_previous_flow_first()
    {
        using var runner = NewRunner();
        var first = new RecordingNode(FlowStatus.Running);
        var second = new RecordingNode(FlowStatus.Running);
        var finished = new List<FlowStatus>();
        runner.Start(first, s => finished.Add(s), null);

        runner.Start(second);

        Assert.Equal(new[] { FlowStatus.Canceled }, finished);
        Assert.Equal(1, first.InterruptCount);
        Assert.Equal(FlowStatus.Running, runner.Status);
        Assert.Equal(1, second.EnterCount);
    }

    [Fact]
    public void Tick_exception_fails_flow_interrupts_root_and_notifies()
    {
        using var runner = NewRunner();
        var node = new RecordingNode(FlowStatus.Running)
        {
            BeforeTick = (_, _) => throw new InvalidOperationException("tick boom"),
        };
        FlowStatus? finished = null;
        Exception seen = null;
        runner.UnhandledException += ex => seen = ex;
        runner.Start(node, s => finished = s, null);

        runner.Step(0f);

        Assert.Equal(FlowStatus.Failed, runner.Status);
        Assert.Equal(FlowStatus.Failed, finished);
        Assert.Equal(1, node.InterruptCount);
        Assert.Equal(0, node.ExitCount);
        Assert.NotNull(seen);
        Assert.Equal("tick boom", seen.Message);
    }

    [Fact]
    public void Enter_exception_fails_flow()
    {
        using var runner = NewRunner();
        var node = new RecordingNode
        {
            OnEnter = _ => throw new InvalidOperationException("enter boom"),
        };
        FlowStatus? finished = null;
        runner.Start(node, s => finished = s, null);

        Assert.Equal(FlowStatus.Failed, runner.Status);
        Assert.Equal(FlowStatus.Failed, finished);
        // 即使 Enter 抛异常，Abort 仍会对根节点调用 Interrupt。
        Assert.Equal(1, node.InterruptCount);
        Assert.Equal(0, node.TickCount);
    }

    [Fact]
    public void ExceptionHandler_receives_exception_and_flow_fails()
    {
        using var runner = NewRunner();
        var seen = new List<Exception>();
        runner.ExceptionHandler = seen.Add;
        var node = new RecordingNode
        {
            BeforeTick = (_, _) => throw new InvalidOperationException("handled"),
        };
        runner.Start(node);

        runner.Step(0f);

        Assert.Single(seen);
        Assert.Equal("handled", seen[0].Message);
        Assert.Equal(FlowStatus.Failed, runner.Status);
    }

    [Fact]
    public void Throwing_ExceptionHandler_is_swallowed_and_flow_still_fails()
    {
        using var runner = NewRunner();
        runner.ExceptionHandler = _ => throw new InvalidOperationException("handler boom");
        var node = new RecordingNode
        {
            BeforeTick = (_, _) => throw new InvalidOperationException("tick boom"),
        };
        FlowStatus? finished = null;
        runner.Start(node, s => finished = s, null);

        var status = runner.Step(0f);

        Assert.Equal(FlowStatus.Failed, status);
        Assert.Equal(FlowStatus.Failed, finished);
    }

    [Fact]
    public void Throwing_UnhandledException_subscriber_is_swallowed()
    {
        using var runner = NewRunner();
        runner.UnhandledException += _ => throw new InvalidOperationException("subscriber boom");
        var node = new RecordingNode
        {
            BeforeTick = (_, _) => throw new InvalidOperationException("tick boom"),
        };

        runner.Start(node);

        Assert.Equal(FlowStatus.Failed, runner.Step(0f));
    }

    [Fact]
    public void onStatusChanged_receives_transition_sequence()
    {
        using var runner = NewRunner();
        var transitions = new List<(FlowStatus prev, FlowStatus next)>();
        var node = new RecordingNode(FlowStatus.Running);
        runner.Start(node, null, (prev, next) => transitions.Add((prev, next)));

        node.Result = FlowStatus.Succeeded;
        runner.Step(0f);

        Assert.Equal(
            new[] { (FlowStatus.NotStarted, FlowStatus.Running), (FlowStatus.Running, FlowStatus.Succeeded) },
            transitions);
    }

    [Fact]
    public void onFinished_fires_only_once_per_run()
    {
        using var runner = NewRunner();
        var count = 0;
        runner.Start(new RecordingNode(FlowStatus.Succeeded), _ => count++, null);

        runner.Step(0f);
        runner.Stop();

        Assert.Equal(1, count);
    }

    [Fact]
    public void Exit_exception_reports_secondary_but_completes_finish()
    {
        // 2026-08-17 修复：根节点 Exit 抛异常不改变已达成的终态，
        // 异常走二级通道上报，收尾照常完成（onFinished 触发、ctx/rootScope 清理）。
        using var runner = NewRunner();
        var node = new RecordingNode
        {
            OnExit = _ => throw new InvalidOperationException("exit boom"),
        };
        FlowStatus? finished = null;
        Exception seen = null;
        runner.UnhandledException += ex => seen = ex;
        runner.Start(node, s => finished = s, null);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.Equal(FlowStatus.Succeeded, finished);
        Assert.NotNull(seen);
        Assert.Equal("exit boom", seen.Message);
    }

    [Fact]
    public void Dispose_mid_run_cancels_and_releases_context()
    {
        var runner = NewRunner();
        var node = new RecordingNode(FlowStatus.Running);
        FlowStatus? finished = null;
        runner.Start(node, s => finished = s, null);

        runner.Dispose();
        runner.Dispose(); // 二次 Dispose 是 no-op

        Assert.Equal(FlowStatus.Canceled, finished);
        Assert.Equal(1, node.InterruptCount);
        Assert.Throws<ObjectDisposedException>(() => runner.Step(0f));
        Assert.Throws<ObjectDisposedException>(() => runner.Start(new RecordingNode()));
        Assert.Throws<ObjectDisposedException>(() => runner.Stop());
        // 钉住：Status 属性不做 disposed 检查，Dispose 后仍可读取终态。
        Assert.Equal(FlowStatus.Canceled, runner.Status);
    }

    [Fact]
    public void Context_returns_same_instance_passed_to_constructor()
    {
        var ctx = new FlowContext();
        using var runner = new FlowRunner(ctx);

        Assert.Same(ctx, runner.Context);
    }

    [Fact]
    public void Runner_provides_FlowWakeUp_and_diagnostics_to_root_scope()
    {
        using var runner = NewRunner();
        FlowWakeUp wakeUpInEnter = null;
        bool hasDiagnosticsInEnter = false;
        var node = new RecordingNode(FlowStatus.Running)
        {
            OnEnter = ctx =>
            {
                wakeUpInEnter = ctx.Get<FlowWakeUp>();
                hasDiagnosticsInEnter = ctx.TryGet<FlowRuntimeDiagnostics>(out _);
            },
        };
        runner.Start(node);

        Assert.NotNull(wakeUpInEnter);
        Assert.True(hasDiagnosticsInEnter);

        node.Result = FlowStatus.Succeeded;
        runner.Step(0f);

        // 流程结束后运行时对象已从 ctx 移除。
        Assert.False(runner.Context.TryGet<FlowWakeUp>(out _));
        Assert.False(runner.Context.TryGet<FlowRuntimeDiagnostics>(out _));
    }

    [Fact]
    public void Wake_from_event_callback_finishes_flow_without_step()
    {
        // Wake/Pump 驱动：事件回调里 Complete → Wake → Pump 在回调线程内推进流程。
        using var runner = NewRunner();
        Action<bool> pendingComplete = null;
        var node = new AwaitCallbackNode((ctx, complete) =>
        {
            pendingComplete = complete;
            return new DelegateDisposable();
        });
        FlowStatus? finished = null;
        runner.Start(node, s => finished = s, null);

        Assert.Equal(FlowStatus.Running, runner.Status);
        Assert.Null(finished);

        pendingComplete(true);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.Equal(FlowStatus.Succeeded, finished);
    }

    [Fact]
    public void Wake_after_finish_is_noop()
    {
        using var runner = NewRunner();
        FlowWakeUp wakeUp = null;
        var node = new RecordingNode(FlowStatus.Running)
        {
            OnEnter = ctx => wakeUp = ctx.Get<FlowWakeUp>(),
        };
        runner.Start(node);
        node.Result = FlowStatus.Succeeded;
        runner.Step(0f);

        wakeUp.Wake();

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
    }

    [Fact]
    public void Pump_iteration_limit_exceeded_fails_flow()
    {
        using var runner = NewRunner();
        runner.MaxPumpIterationsPerWake = 3;
        var node = new DoNode(onTick: (ctx, _) =>
        {
            ctx.Get<FlowWakeUp>().Wake();
            return FlowStatus.Running;
        });
        FlowStatus? finished = null;
        Exception seen = null;
        runner.UnhandledException += ex => seen = ex;
        runner.Start(node, s => finished = s, null);

        Assert.Equal(FlowStatus.Failed, runner.Status);
        Assert.Equal(FlowStatus.Failed, finished);
        Assert.IsType<InvalidOperationException>(seen);
        Assert.Contains("pump iteration limit exceeded", seen.Message);
    }

    [Fact]
    public void Pump_default_limit_is_128()
    {
        using var runner = NewRunner();

        Assert.Equal(128, runner.MaxPumpIterationsPerWake);
    }

    [Fact]
    public void Pump_finishes_normally_when_wake_stops()
    {
        // 每次 Wake 只推进有限步、随后不再 Wake 的流程应当正常完成而不是触发上限。
        using var runner = NewRunner();
        var ticks = 0;
        var counting = new DoNode(onTick: (ctx, _) =>
        {
            ticks++;
            if (ticks < 6)
            {
                ctx.Get<FlowWakeUp>().Wake();
                return FlowStatus.Running;
            }

            return FlowStatus.Succeeded;
        });

        FlowStatus? finished = null;
        runner.Start(counting, s => finished = s, null);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.Equal(FlowStatus.Succeeded, finished);
        Assert.Equal(6, ticks);
    }

    private sealed class DelegateDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
