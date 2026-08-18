using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using UnityHFSM;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class HfsmFlowRunnerTests
{
    private sealed class CountingState : State
    {
        public int EnterCount;
        public int LogicCount;
        public int ExitCount;

        public CountingState(Action<CountingState> onLogic = null)
            : base(onEnter: s => ((CountingState)s).EnterCount++,
                   onLogic: s => { ((CountingState)s).LogicCount++; onLogic?.Invoke((CountingState)s); },
                   onExit: s => ((CountingState)s).ExitCount++)
        {
        }
    }

    private static StateMachine<string, string, string> NewMachine(
        out CountingState a, out CountingState b)
    {
        a = new CountingState();
        b = new CountingState();
        var machine = new StateMachine<string, string, string>();
        machine.AddState("A", a);
        machine.AddState("B", b);
        machine.SetStartState("A");
        return machine;
    }

    [Fact]
    public void Ctor_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HfsmFlowRunner<string, string, string>(null, NewMachine(out _, out _), new FlowEventQueue<string>()));
        Assert.Throws<ArgumentNullException>(
            () => new HfsmFlowRunner<string, string, string>(new FlowContext(), null, new FlowEventQueue<string>()));
        Assert.Throws<ArgumentNullException>(
            () => new HfsmFlowRunner<string, string, string>(new FlowContext(), NewMachine(out _, out _), null));
    }

    [Fact]
    public void Exposes_context_machine_and_events()
    {
        var ctx = new FlowContext();
        var machine = NewMachine(out _, out _);
        var events = new FlowEventQueue<string>();
        using var runner = new HfsmFlowRunner<string, string, string>(ctx, machine, events);

        Assert.Same(ctx, runner.Context);
        Assert.Same(machine, runner.Machine);
        Assert.Same(events, runner.Events);
    }

    [Fact]
    public void Start_enters_start_state_only_once()
    {
        var machine = NewMachine(out var a, out _);
        using var runner = new HfsmFlowRunner<string, string, string>(new FlowContext(), machine, new FlowEventQueue<string>());

        runner.Start();
        runner.Start();

        Assert.Equal(1, a.EnterCount);
    }

    [Fact]
    public void Step_before_start_is_noop()
    {
        var machine = NewMachine(out var a, out _);
        using var runner = new HfsmFlowRunner<string, string, string>(new FlowContext(), machine, new FlowEventQueue<string>());

        runner.Step(0.1f);

        Assert.Equal(0, a.EnterCount);
        Assert.Equal(0, a.LogicCount);
    }

    [Fact]
    public void Step_runs_active_state_logic()
    {
        var machine = NewMachine(out var a, out _);
        using var runner = new HfsmFlowRunner<string, string, string>(new FlowContext(), machine, new FlowEventQueue<string>());

        runner.Start();
        runner.Step(0.1f);
        runner.Step(0.1f);

        Assert.Equal(1, a.EnterCount);
        Assert.Equal(2, a.LogicCount);
    }

    [Fact]
    public void Step_drains_all_queued_events_before_logic()
    {
        var machine = NewMachine(out var a, out var b);
        machine.AddTriggerTransition("go", new Transition<string>("A", "B"));
        machine.AddTriggerTransition("back", new Transition<string>("B", "A"));

        var events = new FlowEventQueue<string>();
        events.Enqueue("go");
        events.Enqueue("back");
        using var runner = new HfsmFlowRunner<string, string, string>(new FlowContext(), machine, events);

        runner.Start();
        runner.Step(0f); // 两个事件在同一 Step 内依次触发，最后跑当前状态 Logic

        Assert.Equal(0, events.Count);
        // A→B→A：a 两次进入，b 一次进入；最终 logic 跑在 A 上。
        Assert.Equal(2, a.EnterCount);
        Assert.Equal(1, b.EnterCount);
        Assert.Equal(1, a.LogicCount);
        Assert.Equal(0, b.LogicCount);
    }

    [Fact]
    public void Stop_exits_state_and_clears_pending_events()
    {
        var machine = NewMachine(out var a, out var b);
        machine.AddTriggerTransition("go", new Transition<string>("A", "B"));
        var events = new FlowEventQueue<string>();
        using var runner = new HfsmFlowRunner<string, string, string>(new FlowContext(), machine, events);

        runner.Start();
        events.Enqueue("go"); // 尚未 Step，事件仍在队列
        runner.Stop();
        runner.Stop(); // 幂等

        Assert.Equal(1, a.ExitCount);

        // 重新启动后，Stop 前积压的事件已被清空：Step 不应再触发 A→B。
        runner.Start();
        runner.Step(0f);

        Assert.Equal(0, b.EnterCount);
        Assert.Equal(1, a.LogicCount);
    }

    [Fact]
    public void Dispose_stops_machine()
    {
        var machine = NewMachine(out var a, out _);
        var runner = new HfsmFlowRunner<string, string, string>(new FlowContext(), machine, new FlowEventQueue<string>());

        runner.Start();
        runner.Dispose();

        Assert.Equal(1, a.ExitCount);
    }

    [Fact]
    public void Unknown_event_does_not_throw()
    {
        var machine = NewMachine(out var a, out _);
        var events = new FlowEventQueue<string>();
        using var runner = new HfsmFlowRunner<string, string, string>(new FlowContext(), machine, events);

        runner.Start();
        events.Enqueue("no-such-trigger");
        runner.Step(0f);

        Assert.Equal(1, a.LogicCount);
    }
}

public sealed class FlowEventQueueTests
{
    [Fact]
    public void Enqueue_TryDequeue_is_fifo()
    {
        var queue = new FlowEventQueue<string>();
        queue.Enqueue("a");
        queue.Enqueue("b");
        queue.Enqueue("c");

        Assert.Equal(3, queue.Count);
        Assert.True(queue.TryDequeue(out var first));
        Assert.Equal("a", first);
        Assert.True(queue.TryDequeue(out var second));
        Assert.Equal("b", second);
        Assert.True(queue.TryDequeue(out var third));
        Assert.Equal("c", third);
    }

    [Fact]
    public void TryDequeue_empty_returns_false_and_default()
    {
        var queue = new FlowEventQueue<int>();

        Assert.False(queue.TryDequeue(out var value));
        Assert.Equal(0, value);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Clear_drops_all_events()
    {
        var queue = new FlowEventQueue<string>();
        queue.Enqueue("a");
        queue.Enqueue("b");

        queue.Clear();

        Assert.Equal(0, queue.Count);
        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public void Count_tracks_enqueue_and_dequeue()
    {
        var queue = new FlowEventQueue<string>();

        Assert.Equal(0, queue.Count);
        queue.Enqueue("a");
        Assert.Equal(1, queue.Count);
        queue.TryDequeue(out _);
        Assert.Equal(0, queue.Count);
    }
}
