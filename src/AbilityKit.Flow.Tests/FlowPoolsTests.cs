using System;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Pooling;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class FlowPoolsTests
{
    [Fact]
    public void RentRunner_returns_runner_in_NotStarted_with_clean_context()
    {
        var runner = FlowPools.RentRunner();
        try
        {
            Assert.Equal(FlowStatus.NotStarted, runner.Status);
            Assert.NotNull(runner.Context);
            Assert.Equal(128, runner.MaxPumpIterationsPerWake);
            Assert.False(runner.Context.TryGet<FlowWakeUp>(out _));
        }
        finally
        {
            FlowPools.ReleaseRunner(runner);
        }
    }

    [Fact]
    public void RentRunner_clears_leftover_context_values_from_previous_use()
    {
        var runner = FlowPools.RentRunner();
        runner.Context.Set(new object());
        FlowPools.ReleaseRunner(runner);

        var rented = FlowPools.RentRunner();
        try
        {
            // 无论拿到哪个实例（onGet 都会 Clear），租到的上下文都是干净的。
            Assert.False(rented.Context.TryGet<object>(out _));
        }
        finally
        {
            FlowPools.ReleaseRunner(rented);
        }
    }

    [Fact]
    public void ReleaseRunner_cancels_active_flow_and_notifies()
    {
        var runner = FlowPools.RentRunner();
        FlowStatus? finished = null;
        var node = new RecordingNode(FlowStatus.Running);
        runner.Start(node, s => finished = s, null);

        FlowPools.ReleaseRunner(runner);

        Assert.Equal(FlowStatus.Canceled, finished);
        Assert.Equal(1, node.InterruptCount);
        Assert.Throws<ObjectDisposedException>(() => runner.Step(0f));
        // Status 不做 disposed 检查；ResetForRelease 会把状态复位为 NotStarted。
        Assert.Equal(FlowStatus.NotStarted, runner.Status);
    }

    [Fact]
    public void Rent_after_release_yields_runner_without_stale_callbacks()
    {
        var runner = FlowPools.RentRunner();
        var staleFinishedCount = 0;
        runner.Start(new RecordingNode(FlowStatus.Running), _ => staleFinishedCount++, null);
        FlowPools.ReleaseRunner(runner);
        Assert.Equal(1, staleFinishedCount); // 释放时因取消触发一次

        var rented = FlowPools.RentRunner();
        try
        {
            rented.Start(new RecordingNode(FlowStatus.Succeeded));
            rented.Start(new RecordingNode(FlowStatus.Failed));

            // 旧回调不应被再次触发（计数保持释放时的 1 次）。
            Assert.Equal(1, staleFinishedCount);
            Assert.Equal(FlowStatus.Failed, rented.Status);
        }
        finally
        {
            FlowPools.ReleaseRunner(rented);
        }
    }

    [Fact]
    public void Rent_after_release_resets_MaxPumpIterationsPerWake()
    {
        var runner = FlowPools.RentRunner();
        runner.MaxPumpIterationsPerWake = 7;
        FlowPools.ReleaseRunner(runner);

        var rented = FlowPools.RentRunner();
        try
        {
            Assert.Equal(128, rented.MaxPumpIterationsPerWake);
        }
        finally
        {
            FlowPools.ReleaseRunner(rented);
        }
    }

    [Fact]
    public void RentRunner_and_ReleaseRunner_roundtrip_reuses_instance()
    {
        // 池是栈式实现：释放后立即租借应拿回同一实例（在关闭并行的本测试程序集内确定性成立）。
        var runner = FlowPools.RentRunner();
        FlowPools.ReleaseRunner(runner);

        var rented = FlowPools.RentRunner();
        try
        {
            Assert.Same(runner, rented);
            Assert.Equal(FlowStatus.NotStarted, rented.Status);
        }
        finally
        {
            FlowPools.ReleaseRunner(rented);
        }
    }

    [Fact]
    public void Double_release_of_same_runner_throws()
    {
        // collectionCheck=true：重复释放会被对象池拒绝。
        var runner = FlowPools.RentRunner();
        FlowPools.ReleaseRunner(runner);

        Assert.Throws<InvalidOperationException>(() => FlowPools.ReleaseRunner(runner));
    }

    [Fact]
    public void RentContext_returns_cleared_context()
    {
        var ctx = FlowPools.RentContext();
        ctx.Set(new object());
        FlowPools.ReleaseContext(ctx);

        var rented = FlowPools.RentContext();
        try
        {
            Assert.False(rented.TryGet<object>(out _));
        }
        finally
        {
            FlowPools.ReleaseContext(rented);
        }
    }

    [Fact]
    public void RentSession_binds_runner_and_context()
    {
        var session = FlowPools.RentSession();
        try
        {
            Assert.Equal(FlowStatus.NotStarted, session.Status);
            Assert.NotNull(session.Context);
        }
        finally
        {
            FlowPools.ReleaseSession(session);
        }

        Assert.Throws<ObjectDisposedException>(() => _ = session.Status);
    }

    [Fact]
    public void RentSession_after_release_session_is_clean()
    {
        var session = FlowPools.RentSession();
        FlowPools.ReleaseSession(session);

        var rented = FlowPools.RentSession();
        try
        {
            rented.Start(new RecordingNode(FlowStatus.Succeeded));
            Assert.Equal(FlowStatus.Succeeded, rented.Status);
        }
        finally
        {
            FlowPools.ReleaseSession(rented);
        }
    }

    /// <summary>
    /// 测试专属 TArgs：FlowHost 池按具体 TArgs 类型区分，
    /// 独占类型保证本测试对 FlowHost 池的断言不受其他测试租借顺序影响。
    /// </summary>
    private sealed class PooledHostArgs { }

    [Fact]
    public void RentHost_binds_provider_per_rent()
    {
        Assert.Throws<ArgumentNullException>(() => FlowPools.RentHost<PooledHostArgs>(null));

        // 第一次租借：provider 正常绑定，Start 使用其根节点。
        var first = new DelegateRootProvider<PooledHostArgs>(_ => new RecordingNode(FlowStatus.Running));
        var host1 = FlowPools.RentHost(first);
        try
        {
            host1.Start(new PooledHostArgs());
            Assert.Equal(FlowStatus.Running, host1.Status);
            Assert.Equal(1, first.CreateCount);
        }
        finally
        {
            FlowPools.ReleaseHost(host1);
        }

        Assert.Throws<ObjectDisposedException>(() => _ = host1.Status);

        // 2026-08-17 修复：provider 不再进池闭包，每次 RentHost 绑定当次传入的 provider。
        var second = new DelegateRootProvider<PooledHostArgs>(_ => new RecordingNode(FlowStatus.Running));
        var host2 = FlowPools.RentHost(second);
        try
        {
            host2.Start(new PooledHostArgs());

            Assert.Equal(FlowStatus.Running, host2.Status);
            Assert.Equal(1, first.CreateCount); // 旧 provider 不再被调用
            Assert.Equal(1, second.CreateCount); // 新 provider 生效
        }
        finally
        {
            FlowPools.ReleaseHost(host2);
        }
    }

    [Fact]
    public void RentCompletion_resets_previous_state()
    {
        var completion = FlowPools.RentCompletion();
        completion.Complete(true);
        FlowPools.ReleaseCompletion(completion);

        var rented = FlowPools.RentCompletion();
        try
        {
            Assert.False(rented.IsDone);
            Assert.False(rented.Succeeded);
        }
        finally
        {
            FlowPools.ReleaseCompletion(rented);
        }
    }

    [Fact]
    public void Double_release_of_completion_throws()
    {
        var completion = FlowPools.RentCompletion();
        FlowPools.ReleaseCompletion(completion);

        Assert.Throws<InvalidOperationException>(() => FlowPools.ReleaseCompletion(completion));
    }

    [Fact]
    public void RentEventQueue_roundtrip_clears_entries()
    {
        var queue = FlowPools.RentEventQueue<string>();
        queue.Enqueue("a");
        FlowPools.ReleaseEventQueue(queue);

        var rented = FlowPools.RentEventQueue<string>();
        try
        {
            Assert.Equal(0, rented.Count);
        }
        finally
        {
            FlowPools.ReleaseEventQueue(rented);
        }
    }

    [Fact]
    public void RentStageNodeList_roundtrip_clears_entries()
    {
        var list = FlowPools.RentStageNodeList();
        list.Add(new RecordingNode());
        FlowPools.ReleaseStageNodeList(list);

        var rented = FlowPools.RentStageNodeList();
        try
        {
            Assert.Empty(rented);
        }
        finally
        {
            FlowPools.ReleaseStageNodeList(rented);
        }
    }

    [Fact]
    public void Release_null_arguments_are_noops()
    {
        FlowPools.ReleaseRunner(null);
        FlowPools.ReleaseContext(null);
        FlowPools.ReleaseSession(null);
        FlowPools.ReleaseHost<object>(null);
        FlowPools.ReleaseCompletion(null);
        FlowPools.ReleaseEventQueue<string>(null);
        FlowPools.ReleaseStageNodeList(null);
    }

    [Fact]
    public void Context_scope_maps_roundtrip_through_pool()
    {
        // BeginScope/Dispose 租还 scope 字典；大量往返后作用域语义保持正确。
        var ctx = new FlowContext();
        for (int i = 0; i < 32; i++)
        {
            var scope = ctx.BeginScope();
            ctx.Set(i);
            Assert.True(ctx.TryGet(out int v));
            Assert.Equal(i, v);
            scope.Dispose();
            Assert.False(ctx.TryGet(out int _));
        }
    }
}
