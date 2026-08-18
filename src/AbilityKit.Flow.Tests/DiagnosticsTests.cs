using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class DiagnosticsTests
{
    [Fact]
    public void Observer_receives_full_lifecycle_for_instant_flow_in_order()
    {
        var observer = new RecordingObserver();
        using var runner = new FlowRunner(new FlowContext()) { Observer = observer };

        runner.Start(new SequenceNode(new DoNode(), new DoNode()));

        Assert.Equal(
            new[]
            {
                // Start 内先 SetStatus(Running) 再广播 RunStarted。
                "StatusChanged:NotStarted->Running",
                "RunStarted",
                "Enter:SequenceNode",
                "Enter:DoNode",
                "Tick:DoNode:Succeeded",
                "Exit:DoNode:Succeeded",
                "Enter:DoNode",
                "Tick:DoNode:Succeeded",
                "Exit:DoNode:Succeeded",
                "Tick:SequenceNode:Succeeded",
                "StatusChanged:Running->Succeeded",
                "Exit:SequenceNode:Succeeded",
                "RunFinished:Succeeded"
            },
            observer.Calls);
    }

    [Fact]
    public void Observer_receives_interrupt_on_cancel()
    {
        var observer = new RecordingObserver();
        using var runner = new FlowRunner(new FlowContext()) { Observer = observer };
        runner.Start(new RecordingNode(FlowStatus.Running));

        runner.Stop();

        Assert.Contains("Interrupt:RecordingNode:Canceled", observer.Calls);
        Assert.Equal("RunFinished:Canceled", observer.Calls.Last());
    }

    [Fact]
    public void Observer_receives_exception_on_tick_failure()
    {
        var observer = new RecordingObserver();
        using var runner = new FlowRunner(new FlowContext()) { Observer = observer };
        runner.Start(new RecordingNode { BeforeTick = (_, _) => throw new InvalidOperationException("obs boom") });

        Assert.Contains("Unhandled:InvalidOperationException:obs boom", observer.Calls);
        Assert.Equal("RunFinished:Failed", observer.Calls.Last());
    }

    [Fact]
    public void RunId_increments_across_runs()
    {
        var observer = new RecordingObserver();
        using var runner = new FlowRunner(new FlowContext()) { Observer = observer };
        runner.Start(new DoNode());
        runner.Start(new DoNode());

        Assert.Equal(2, observer.RunStartedIds.Distinct().Count());
    }

    [Fact]
    public void Statistics_counters_track_run_lifecycle()
    {
        using var runner = new FlowRunner(new FlowContext());
        runner.Start(new SequenceNode(new DoNode(), new RecordingNode(FlowStatus.Running)));
        runner.Step(0f); // 第二个子节点进入 Running

        runner.Start(new DoNode()); // 重启：第一个流程被取消，Diagnostics 是新实例

        var after = runner.Diagnostics.Statistics;
        Assert.Equal(1, after.RunsStarted);
        Assert.Equal(1, after.RunsFinished);
        Assert.Equal(1, after.NodesEntered);
        Assert.Equal(1, after.NodesTicked);
        Assert.Equal(1, after.NodesExited);
        Assert.Equal(FlowStatus.Succeeded, after.LastStatus);
    }

    [Fact]
    public void Statistics_count_interrupted_nodes_on_cancel()
    {
        using var runner = new FlowRunner(new FlowContext());
        var child = new RecordingNode(FlowStatus.Running);
        runner.Start(new SequenceNode(child));
        runner.Stop();

        var stats = runner.Diagnostics.Statistics;

        // 根 SequenceNode 与其运行中子节点各计一次 Interrupt。
        Assert.Equal(2, stats.NodesInterrupted);
        Assert.Equal(FlowStatus.Canceled, stats.LastStatus);
        Assert.Equal(1, stats.RunsStarted);
        Assert.Equal(1, stats.RunsFinished);
    }

    [Fact]
    public void Statistics_count_unhandled_exceptions()
    {
        using var runner = new FlowRunner(new FlowContext());
        runner.Start(new RecordingNode { BeforeTick = (_, _) => throw new InvalidOperationException() });
        runner.Step(0f);

        Assert.Equal(1, runner.Diagnostics.Statistics.UnhandledExceptions);
    }

    [Fact]
    public void TraceRecorder_receives_event_stream()
    {
        var recorder = new InMemoryFlowTraceRecorder();
        using var runner = new FlowRunner(new FlowContext()) { TraceRecorder = recorder };

        runner.Start(new DoNode());

        var types = recorder.GetSnapshot().Select(r => r.Type).ToArray();
        Assert.Equal(
            new[]
            {
                // SetStatus(Running) 的 StatusChanged 先于 RunStarted 记录。
                FlowTraceEventType.StatusChanged,
                FlowTraceEventType.RunStarted,
                FlowTraceEventType.NodeEnter,
                FlowTraceEventType.NodeTick,
                FlowTraceEventType.StatusChanged,
                FlowTraceEventType.NodeExit,
                FlowTraceEventType.RunFinished
            },
            types);
    }

    [Fact]
    public void TraceRecorder_records_exception_and_pump_limit_events()
    {
        var recorder = new InMemoryFlowTraceRecorder();
        using var runner = new FlowRunner(new FlowContext())
        {
            TraceRecorder = recorder,
            MaxPumpIterationsPerWake = 2,
        };
        runner.Start(new DoNode(onTick: (ctx, _) =>
        {
            ctx.Get<FlowWakeUp>().Wake();
            return FlowStatus.Running;
        }));

        Assert.Contains(recorder.GetSnapshot(), r => r.Type == FlowTraceEventType.PumpLimitExceeded);
        Assert.Contains(recorder.GetSnapshot(), r => r.Type == FlowTraceEventType.UnhandledException);
    }

    [Fact]
    public void TraceRecorder_evicts_oldest_when_capacity_exceeded()
    {
        var recorder = new InMemoryFlowTraceRecorder(capacity: 2);

        recorder.Record(new FlowTraceData(0, 1, FlowTraceEventType.RunStarted, "A", FlowStatus.Running, 0f, 0, 0, null, null));
        recorder.Record(new FlowTraceData(1, 1, FlowTraceEventType.NodeEnter, "A", FlowStatus.Running, 0f, 0, 0, null, null));
        recorder.Record(new FlowTraceData(2, 1, FlowTraceEventType.NodeTick, "A", FlowStatus.Running, 0f, 0, 0, null, null));

        var snapshot = recorder.GetSnapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(FlowTraceEventType.NodeEnter, snapshot[0].Type);
        Assert.Equal(FlowTraceEventType.NodeTick, snapshot[1].Type);
    }

    [Fact]
    public void TraceRecorder_clear_drops_all_records()
    {
        var recorder = new InMemoryFlowTraceRecorder();
        recorder.Record(new FlowTraceData(0, 1, FlowTraceEventType.RunStarted, null, FlowStatus.Running, 0f, 0, 0, null, null));

        recorder.Clear();

        Assert.Empty(recorder.GetSnapshot());
    }

    [Fact]
    public void TraceRecorder_non_positive_capacity_falls_back_to_1024()
    {
        var recorder = new InMemoryFlowTraceRecorder(capacity: 0);

        for (int i = 0; i < 1100; i++)
        {
            recorder.Record(new FlowTraceData(i, 1, FlowTraceEventType.NodeTick, null, FlowStatus.Running, 0f, 0, 0, null, null));
        }

        Assert.Equal(1024, recorder.GetSnapshot().Count);
    }

    [Fact]
    public void TraceRecorder_is_enabled_and_nulls_are_normalized()
    {
        var recorder = new InMemoryFlowTraceRecorder();
        Assert.True(recorder.IsEnabled);

        recorder.Record(new FlowTraceData(5, 1, FlowTraceEventType.RunStarted, null, FlowStatus.Running, 0f, 0, 0, null, null));

        var record = recorder.GetSnapshot().Single();
        Assert.Equal(string.Empty, record.NodeName);
        Assert.Equal(string.Empty, record.Message);
    }

    [Fact]
    public void TraceRecorder_records_node_name_and_deltaTime()
    {
        var recorder = new InMemoryFlowTraceRecorder();
        using var runner = new FlowRunner(new FlowContext()) { TraceRecorder = recorder };

        var node = new RecordingNode(FlowStatus.Running);
        runner.Start(node);
        runner.Step(0.5f);

        var tickRecord = recorder.GetSnapshot().First(r => r.Type == FlowTraceEventType.NodeTick && r.DeltaTime > 0f);
        Assert.Equal("RecordingNode", tickRecord.NodeName);
        Assert.Equal(0.5f, tickRecord.DeltaTime);
    }

    [Fact]
    public void NullFlowObserver_is_singleton_noop()
    {
        Assert.Same(NullFlowObserver.Instance, NullFlowObserver.Instance);
        NullFlowObserver.Instance.OnRunStarted(0, null, null);
        NullFlowObserver.Instance.OnRunFinished(0, FlowStatus.Succeeded, null);
        NullFlowObserver.Instance.OnStatusChanged(0, FlowStatus.NotStarted, FlowStatus.Running, null);
        NullFlowObserver.Instance.OnNodeEnter(0, null, null);
        NullFlowObserver.Instance.OnNodeTick(0, null, null, 0f, FlowStatus.Running, 0);
        NullFlowObserver.Instance.OnNodeExit(0, null, null, FlowStatus.Succeeded);
        NullFlowObserver.Instance.OnNodeInterrupt(0, null, null, FlowStatus.Canceled);
        NullFlowObserver.Instance.OnUnhandledException(0, new Exception(), null);
    }

    [Fact]
    public void Statistics_snapshot_is_independent_copy()
    {
        using var runner = new FlowRunner(new FlowContext());
        runner.Start(new DoNode());

        var snapshot = runner.Diagnostics.Statistics.Snapshot();
        runner.Start(new DoNode());

        Assert.Equal(1, snapshot.RunsStarted);
        Assert.Equal(1, snapshot.RunsFinished);
    }

    [Fact]
    public void Statistics_average_tick_ticks_computes()
    {
        using var runner = new FlowRunner(new FlowContext());
        runner.Start(new SequenceNode(new DoNode(), new DoNode()));

        var stats = runner.Diagnostics.Statistics;
        Assert.Equal(3, stats.NodesTicked); // 两个 Do + 一次 Sequence
        Assert.True(stats.AverageNodeTickTicks >= 0);
    }
}
