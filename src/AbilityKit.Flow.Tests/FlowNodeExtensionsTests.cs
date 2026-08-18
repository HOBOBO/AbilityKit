using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class FlowNodeExtensionsTests
{
    [Fact]
    public void Execute_null_root_throws()
    {
        IFlowNode nullNode = null;

        Assert.Throws<ArgumentNullException>(() => nullNode.Execute());
    }

    [Fact]
    public void Execute_instant_flow_returns_success_with_zero_loop_steps()
    {
        // Start 内部预热 Step(0) 已完成立即成功的节点，循环体内不再执行 Step。
        var result = new DoNode().Execute();

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Steps);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Execute_counts_steps_until_completion()
    {
        // 预热 tick（dt=0）不计时；之后每轮 dt=0.25，两轮后 WaitSeconds(0.5) 完成。
        var result = new WaitSecondsNode(0.5f).Execute(new FlowExecutionOptions { DeltaTime = 0.25f });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Steps);
    }

    [Fact]
    public void Execute_returns_failed_when_node_fails()
    {
        var result = new DoNode(onTick: (_, _) => FlowStatus.Failed).Execute();

        Assert.Equal(FlowStatus.Failed, result.Status);
        Assert.True(result.IsTerminalFailure);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Execute_returns_canceled_when_node_cancels()
    {
        var result = new DoNode(onTick: (_, _) => FlowStatus.Canceled).Execute();

        Assert.Equal(FlowStatus.Canceled, result.Status);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Execute_captures_tick_exception_and_fails()
    {
        var result = new DoNode(onTick: (_, _) => throw new InvalidOperationException("boom")).Execute();

        Assert.Equal(FlowStatus.Failed, result.Status);
        Assert.NotNull(result.Exception);
        Assert.Equal("boom", result.Exception.Message);
    }

    [Fact]
    public void Execute_RethrowExceptions_rethrows_captured_exception()
    {
        Assert.Throws<InvalidOperationException>(
            () => new DoNode(onTick: (_, _) => throw new InvalidOperationException("rethrow me"))
                .Execute(new FlowExecutionOptions { RethrowExceptions = true }));
    }

    [Fact]
    public void Execute_captures_enter_exception()
    {
        var result = new DoNode(onEnter: _ => throw new ArgumentException("enter boom")).Execute();

        Assert.Equal(FlowStatus.Failed, result.Status);
        Assert.IsType<ArgumentException>(result.Exception);
    }

    [Fact]
    public void Execute_max_steps_exceeded_cancels_and_captures_exception()
    {
        var result = new WaitUntilNode(_ => false).Execute(new FlowExecutionOptions { MaxSteps = 3 });

        Assert.Equal(FlowStatus.Canceled, result.Status);
        Assert.Equal(3, result.Steps);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains("step limit exceeded: limit=3", result.Exception.Message);
    }

    [Fact]
    public void Execute_max_steps_exceeded_with_rethrow_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => new WaitUntilNode(_ => false).Execute(
                new FlowExecutionOptions { MaxSteps = 2, RethrowExceptions = true }));
    }

    [Fact]
    public void Execute_max_steps_zero_means_unlimited()
    {
        var result = new WaitSecondsNode(0.3f).Execute(
            new FlowExecutionOptions { DeltaTime = 0.1f, MaxSteps = 0 });

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Steps);
    }

    [Fact]
    public void Execute_max_steps_negative_means_unlimited()
    {
        var result = new WaitSecondsNode(0.3f).Execute(
            new FlowExecutionOptions { DeltaTime = 0.1f, MaxSteps = -1 });

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Steps);
    }

    [Fact]
    public void Execute_default_options()
    {
        var options = FlowExecutionOptions.Default;

        Assert.Equal(0f, options.DeltaTime);
        Assert.Equal(1024, options.MaxSteps);
        Assert.False(options.RethrowExceptions);
        Assert.Null(options.ExceptionHandler);
        Assert.Null(options.Observer);
        Assert.Null(options.TraceRecorder);
    }

    [Fact]
    public void Execute_invokes_option_ExceptionHandler()
    {
        var seen = new List<Exception>();
        var result = new DoNode(onTick: (_, _) => throw new InvalidOperationException("notified"))
            .Execute(new FlowExecutionOptions { ExceptionHandler = seen.Add });

        Assert.Equal(FlowStatus.Failed, result.Status);
        Assert.Single(seen);
    }

    [Fact]
    public void Execute_keeps_only_first_exception()
    {
        // Sequence 中第二个节点也抛异常时，结果只保留第一个异常。
        var result = new SequenceNode(
            new DoNode(onTick: (_, _) => throw new InvalidOperationException("first")),
            new DoNode(onTick: (_, _) => throw new InvalidOperationException("second")))
            .Execute();

        Assert.Equal(FlowStatus.Failed, result.Status);
        Assert.Equal("first", result.Exception.Message);
    }

    [Fact]
    public void Execute_completes_multi_level_composition()
    {
        // 组合结构端到端：If + Sequence + ParallelAll + Timeout 全部成功。
        var flow = new IfNode(
            _ => true,
            new SequenceNode(
                new ParallelAllNode(new DoNode(), new WaitSecondsNode(0.1f)),
                new TimeoutNode(1f, new WaitUntilNode(_ => true))));

        var result = flow.Execute(new FlowExecutionOptions { DeltaTime = 0.1f });

        Assert.True(result.Succeeded);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Execute_propagates_timeout_failure_from_composition()
    {
        var flow = new TimeoutNode(0.2f, new WaitUntilNode(_ => false));
        var result = flow.Execute(new FlowExecutionOptions { DeltaTime = 0.1f });

        Assert.Equal(FlowStatus.Failed, result.Status);
        Assert.Null(result.Exception); // 超时失败是正常终态，不是异常
    }

    [Fact]
    public void Execute_race_empty_completes_immediately()
    {
        // 2026-08-17 修复：空 Race 立即 Succeeded，不再依赖 MaxSteps 兜底。
        var result = new RaceNode().Execute(new FlowExecutionOptions { MaxSteps = 5 });

        Assert.Equal(FlowStatus.Succeeded, result.Status);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void FlowExecutionResult_helpers()
    {
        var ok = new FlowExecutionResult(FlowStatus.Succeeded, 1, null);
        var fail = new FlowExecutionResult(FlowStatus.Failed, 2, new InvalidOperationException());
        var canceled = new FlowExecutionResult(FlowStatus.Canceled, 3, null);
        var running = new FlowExecutionResult(FlowStatus.Running, 4, null);

        Assert.True(ok.Succeeded);
        Assert.False(ok.IsTerminalFailure);

        Assert.False(fail.Succeeded);
        Assert.True(fail.IsTerminalFailure);

        Assert.True(canceled.IsTerminalFailure);
        Assert.False(running.IsTerminalFailure);
        Assert.False(running.Succeeded);
    }
}
