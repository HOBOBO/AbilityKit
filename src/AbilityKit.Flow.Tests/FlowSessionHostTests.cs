using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Pooling;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class FlowSessionHostTests
{
    // ---------- FlowSession ----------

    [Fact]
    public void Session_start_runs_flow_and_raises_events()
    {
        using var session = new FlowSession();
        var statuses = new List<FlowStatus>();
        session.StatusChanged += (_, next) => statuses.Add(next);
        session.Start(new RecordingNode(FlowStatus.Succeeded));

        Assert.Equal(FlowStatus.Succeeded, session.Status);
        Assert.Equal(new[] { FlowStatus.Running, FlowStatus.Succeeded }, statuses);
    }

    [Fact]
    public void Session_started_event_fires_after_runner_start()
    {
        using var session = new FlowSession();
        var started = false;
        session.Started += () => started = true;

        session.Start(new RecordingNode(FlowStatus.Running));

        Assert.True(started);
    }

    [Fact]
    public void Session_instant_flow_started_fires_before_finished_event()
    {
        // 2026-08-17 修复：Started 先于 runner.Start 触发，立即完成的流程事件顺序不再倒置。
        using var session = new FlowSession();
        var order = new List<string>();
        session.Started += () => order.Add("started");
        session.Finished += s => order.Add($"finished:{s}");

        session.Start(new RecordingNode(FlowStatus.Succeeded));

        Assert.Equal(new[] { "started", "finished:Succeeded" }, order);
    }

    [Fact]
    public void Session_step_advances_flow()
    {
        using var session = new FlowSession();
        var node = new RecordingNode(FlowStatus.Running);
        session.Start(node);

        node.Result = FlowStatus.Succeeded;
        var final = session.Step(0f);

        Assert.Equal(FlowStatus.Succeeded, final);
    }

    [Fact]
    public void Session_stop_cancels_and_raises_finished()
    {
        using var session = new FlowSession();
        FlowStatus? finished = null;
        session.Finished += s => finished = s;
        session.Start(new RecordingNode(FlowStatus.Running));

        session.Stop();

        Assert.Equal(FlowStatus.Canceled, session.Status);
        Assert.Equal(FlowStatus.Canceled, finished);
    }

    [Fact]
    public void Session_relays_unhandled_exception()
    {
        using var session = new FlowSession();
        var seen = new List<Exception>();
        session.UnhandledException += seen.Add;
        var node = new RecordingNode
        {
            BeforeTick = (_, _) => throw new InvalidOperationException("session boom"),
        };
        session.Start(node);
        session.Step(0f);

        Assert.Equal(FlowStatus.Failed, session.Status);
        Assert.Single(seen);
        Assert.Equal("session boom", seen[0].Message);
    }

    [Fact]
    public void Session_dispose_releases_runner_and_throws_after()
    {
        var session = new FlowSession();
        session.Start(new RecordingNode(FlowStatus.Running));
        FlowStatus? finished = null;
        session.Finished += s => finished = s;

        session.Dispose();
        session.Dispose(); // 二次 Dispose 无副作用

        // 钉住实际行为（可疑，见报告）：Dispose 先清空事件再释放 Runner，
        // 因此运行中 Dispose 不会触发 Finished(Canceled) 通知。
        Assert.Null(finished);
        Assert.Throws<ObjectDisposedException>(() => session.Step(0f));
        Assert.Throws<ObjectDisposedException>(() => _ = session.Status);
        Assert.Throws<ObjectDisposedException>(() => _ = session.Context);
    }

    [Fact]
    public void Session_after_use_can_start_new_flow()
    {
        using var session = new FlowSession();
        session.Start(new RecordingNode(FlowStatus.Succeeded));
        session.Start(new RecordingNode(FlowStatus.Running));

        Assert.Equal(FlowStatus.Running, session.Status);
    }

    // ---------- FlowHost ----------

    [Fact]
    public void Host_ctor_requires_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new FlowHost<object>(null));
    }

    [Fact]
    public void Host_start_uses_provider_root_and_raises_events()
    {
        var provider = new DelegateRootProvider<string>(_ => new RecordingNode(FlowStatus.Running));
        using var host = new FlowHost<string>(provider);

        var started = false;
        FlowStatus? finished = null;
        host.Started += () => started = true;
        host.Finished += s => finished = s;
        host.Start("args");

        Assert.True(started);
        Assert.Null(finished);
        Assert.Equal(FlowStatus.Running, host.Status);
        Assert.Equal(1, provider.CreateCount);
    }

    [Fact]
    public void Host_stop_cancels_and_finished_raises()
    {
        using var host = new FlowHost<object>(new DelegateRootProvider<object>(_ => new RecordingNode(FlowStatus.Running)));
        FlowStatus? finished = null;
        host.Finished += s => finished = s;
        host.Start(0);

        host.Stop();

        Assert.Equal(FlowStatus.Canceled, host.Status);
        Assert.Equal(FlowStatus.Canceled, finished);
    }

    [Fact]
    public void Host_dispose_throws_after_and_is_idempotent()
    {
        var host = new FlowHost<object>(new DelegateRootProvider<object>(_ => new RecordingNode(FlowStatus.Running)));
        host.Start(0);

        host.Dispose();
        host.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = host.Status);
        Assert.Throws<ObjectDisposedException>(() => host.Start(0));
    }

    [Fact]
    public void Host_relays_unhandled_exception()
    {
        // FlowHost 没有公开 Step；让根节点在 Start 的预热 Step 内抛异常，
        // 验证 Session → Host 的 UnhandledException 事件转发。
        using var host = new FlowHost<object>(new DelegateRootProvider<object>(
            _ => new RecordingNode { OnEnter = _ => throw new InvalidOperationException("host boom") }));
        var seen = new List<Exception>();
        host.UnhandledException += seen.Add;

        host.Start(0);

        Assert.Equal(FlowStatus.Failed, host.Status);
        Assert.Single(seen);
        Assert.Equal("host boom", seen[0].Message);
    }

    [Fact]
    public void Host_pooled_roundtrip_via_FlowPools()
    {
        var provider = new DelegateRootProvider<object>(_ => new RecordingNode(FlowStatus.Running));
        var host = FlowPools.RentHost(provider);
        host.Start(new object());
        FlowPools.ReleaseHost(host);

        var rented = FlowPools.RentHost(provider);
        try
        {
            rented.Start(new object());
            Assert.Equal(FlowStatus.Running, rented.Status);
        }
        finally
        {
            FlowPools.ReleaseHost(rented);
        }
    }

    [Fact]
    public void Host_restart_cancels_previous_flow()
    {
        var first = new RecordingNode(FlowStatus.Running);
        var second = new RecordingNode(FlowStatus.Running);
        var provider = new DelegateRootProvider<object>(a => ReferenceEquals(a, "first") ? first : second);
        using var host = new FlowHost<object>(provider);
        var finished = new List<FlowStatus>();
        host.Finished += s => finished.Add(s);

        host.Start("first");
        host.Start("second");

        Assert.Equal(new[] { FlowStatus.Canceled }, finished);
        Assert.Equal(1, first.InterruptCount);
        Assert.Equal(FlowStatus.Running, host.Status);
    }
}
