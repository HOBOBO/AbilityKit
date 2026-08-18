using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class FlowCompletionTests
{
    [Fact]
    public void Initial_state_is_not_done()
    {
        var completion = new FlowCompletion();

        Assert.False(completion.IsDone);
        Assert.False(completion.Succeeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Complete_records_result(bool succeeded)
    {
        var completion = new FlowCompletion();

        completion.Complete(succeeded);

        Assert.True(completion.IsDone);
        Assert.Equal(succeeded, completion.Succeeded);
    }

    [Fact]
    public void Second_complete_is_ignored()
    {
        var completion = new FlowCompletion();
        completion.Complete(true);

        completion.Complete(false);

        Assert.True(completion.IsDone);
        Assert.True(completion.Succeeded);
    }

    [Fact]
    public void Reset_clears_result()
    {
        var completion = new FlowCompletion();
        completion.Complete(false);

        completion.Reset();

        Assert.False(completion.IsDone);
        Assert.False(completion.Succeeded);
    }

    [Fact]
    public void Reset_allows_completing_again()
    {
        var completion = new FlowCompletion();
        completion.Complete(true);
        completion.Reset();

        completion.Complete(false);

        Assert.True(completion.IsDone);
        Assert.False(completion.Succeeded);
    }

    [Fact]
    public void Attached_wakeUp_fires_once_on_complete()
    {
        // 通过 Runner + AwaitCompletionNode 观察：Complete → Wake → Pump 在回调内完成流程；
        // 第二次 Complete 被忽略（且 Exit 时已 Detach），不会再次推进或改变状态。
        var completion = new FlowCompletion();
        var node = new AwaitCompletionNode(_ => completion);
        using var runner = new FlowRunner(new FlowContext());
        runner.Start(node);

        completion.Complete(true);
        Assert.Equal(FlowStatus.Succeeded, runner.Status);

        completion.Complete(false);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.True(completion.Succeeded);
    }

    [Fact]
    public void Complete_without_attached_wakeUp_is_safe()
    {
        var completion = new FlowCompletion();

        completion.Complete(true);

        Assert.True(completion.IsDone);
    }

    [Fact]
    public void Detach_then_complete_does_not_throw()
    {
        var completion = new FlowCompletion();
        var node = new AwaitCompletionNode(_ => completion);
        using var runner = new FlowRunner(new FlowContext());
        runner.Start(node);

        node.Interrupt(runner.Context); // 显式中断路径：DetachWakeUp
        completion.Complete(true);

        Assert.Equal(FlowStatus.Running, runner.Status);
    }

    [Fact]
    public void Completion_wake_pump_completes_full_flow_from_event_thread()
    {
        // 端到端：Sequence(等 Completion, 再跑一个即时节点) ——
        // Complete 触发 Wake→Pump，无需任何 Step，流程在回调内直接跑到终态。
        var completion = new FlowCompletion();
        var finishedOrder = new List<FlowStatus>();
        var postNode = new RecordingNode(FlowStatus.Succeeded);
        var root = new AbilityKit.Ability.Flow.Nodes.SequenceNode(
            new AwaitCompletionNode(_ => completion),
            postNode);
        using var runner = new FlowRunner(new FlowContext());
        runner.Start(root, s => finishedOrder.Add(s), null);

        Assert.Equal(FlowStatus.Running, runner.Status);

        completion.Complete(true);

        Assert.Equal(FlowStatus.Succeeded, runner.Status);
        Assert.Equal(new[] { FlowStatus.Succeeded }, finishedOrder);
        Assert.Equal(1, postNode.TickCount);
    }
}
