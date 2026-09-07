using System;
using System.Collections.Generic;
using AbilityKit.HFSM.Extension;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateMachine;

public sealed class HfsmCompositeBehaviourTests
{
    [Fact]
    public void Sequence_result_can_drive_transition_after_state_behaviour_succeeds()
    {
        var blackboard = new TestBlackboard();
        var time = new TestTimeSource { DeltaTime = 0.1f };
        var root = BehaviourSpec<string>.Sequence(
            BehaviourSpec<string>.Task("begin"),
            BehaviourSpec<string>.Task("delay"),
            BehaviourSpec<string>.Task("end"));
        var fsm = Build(
            time,
            blackboard,
            new[]
            {
                new NodeSpec<string>("work", root),
                HoldState("done", "done"),
            },
            new[]
            {
                new TransitionSpec(
                    "work",
                    "done",
                    string.Empty,
                    TransitionMode.OnSucceeded),
            });

        fsm.OnLogic();
        Assert.Equal("work", fsm.ActiveStateName);
        Assert.Equal(new[] { "begin" }, blackboard.Trace);

        fsm.OnLogic();
        Assert.Equal("work", fsm.ActiveStateName);
        Assert.Equal(new[] { "begin", "end" }, blackboard.Trace);

        fsm.OnLogic();
        Assert.Equal("done", fsm.ActiveStateName);
        Assert.Equal(new[] { "begin", "end", "done" }, blackboard.Trace);
    }

    [Fact]
    public void Failure_result_can_drive_a_different_transition()
    {
        var blackboard = new TestBlackboard();
        var time = new TestTimeSource { DeltaTime = 0.1f };
        var fsm = Build(
            time,
            blackboard,
            new[]
            {
                HoldState("work", "fail"),
                HoldState("fallback", "fallback"),
            },
            new[]
            {
                new TransitionSpec(
                    "work",
                    "fallback",
                    string.Empty,
                    TransitionMode.OnFailed),
            });

        fsm.OnLogic();
        Assert.Equal("work", fsm.ActiveStateName);
        fsm.OnLogic();

        Assert.Equal("fallback", fsm.ActiveStateName);
        Assert.Equal(new[] { "fallback" }, blackboard.Trace);
    }

    [Fact]
    public void Selector_and_parallel_policies_are_composable_from_runtime_spec()
    {
        var blackboard = new TestBlackboard();
        var time = new TestTimeSource { DeltaTime = 0.1f };
        var selector = BehaviourSpec<string>.Selector(
            BehaviourSpec<string>.Task("fail"),
            BehaviourSpec<string>.Task("selected"));
        var root = BehaviourSpec<string>.Parallel(
            new[]
            {
                selector,
                BehaviourSpec<string>.Task("probe"),
            },
            ParallelSuccessPolicy.Any,
            ParallelFailurePolicy.Any);
        var fsm = Build(
            time,
            blackboard,
            new[] { new NodeSpec<string>("work", root) },
            Array.Empty<TransitionSpec>());

        fsm.OnLogic();
        var state = Assert.IsAssignableFrom<CompositeActionState<string, string>>(fsm.GetState("work"));

        Assert.True(state.IsCompleted);
        Assert.Equal(ActionBehaviourStatus.Success, state.LastStatus);
        Assert.Equal(new[] { "selected" }, blackboard.Trace);
        Assert.NotNull(blackboard.Probe);
        Assert.Equal(1, blackboard.Probe.AbortCount);
    }

    [Fact]
    public void Normal_transition_waits_for_protected_state_behaviour_to_finish()
    {
        var blackboard = new TestBlackboard { Leave = true };
        var time = new TestTimeSource { DeltaTime = 0.1f };
        var fsm = Build(
            time,
            blackboard,
            new[]
            {
                new NodeSpec<string>(
                    "work",
                    BehaviourSpec<string>.Task("delay"),
                    needsExitTime: true),
                HoldState("done", "done"),
            },
            new[] { new TransitionSpec("work", "done", "leave") });

        fsm.OnLogic();
        Assert.Equal("work", fsm.ActiveStateName);

        fsm.OnLogic();
        Assert.Equal("done", fsm.ActiveStateName);
    }

    [Fact]
    public void Higher_priority_forced_transition_interrupts_running_behaviour()
    {
        var blackboard = new TestBlackboard();
        var time = new TestTimeSource { DeltaTime = 0.1f };
        var fsm = Build(
            time,
            blackboard,
            new[]
            {
                new NodeSpec<string>(
                    "work",
                    BehaviourSpec<string>.Task("probe"),
                    needsExitTime: true),
                HoldState("normal", "normal"),
                HoldState("urgent", "urgent"),
            },
            new[]
            {
                new TransitionSpec("work", "normal", "leave", priority: 10),
                new TransitionSpec(
                    "work",
                    "urgent",
                    "leave",
                    priority: 100,
                    forceInstantly: true),
            });

        fsm.OnLogic();
        Assert.Equal("work", fsm.ActiveStateName);
        Assert.NotNull(blackboard.Probe);

        blackboard.Leave = true;
        fsm.OnLogic();

        Assert.Equal("urgent", fsm.ActiveStateName);
        Assert.Equal(1, blackboard.Probe.AbortCount);
        Assert.Equal(new[] { "urgent" }, blackboard.Trace);
    }

    [Fact]
    public void Exiting_machine_cancels_pending_composite_transition()
    {
        var probe = new ProbeBehaviour();
        var targetEntered = false;
        var work = new CompositeActionState<string>(needsExitTime: true).SetRoot(probe);
        var fsm = new AbilityKit.HFSM.StateMachine<string>();
        fsm.AddState("work", work);
        fsm.AddState("target", new AbilityKit.HFSM.State<string>(onEnter: _ => targetEntered = true));
        fsm.AddTransition(new AbilityKit.HFSM.Transition<string>("work", "target"));
        fsm.SetStartState("work");
        fsm.OnEnter();

        fsm.OnLogic();
        Assert.True(fsm.HasPendingTransition);

        fsm.OnExit();

        Assert.False(fsm.IsActive);
        Assert.False(targetEntered);
        Assert.Equal(1, probe.AbortCount);
    }

    [Fact]
    public void Parallel_snapshot_preserves_completed_children()
    {
        var count = 0;
        var parallel = new ParallelBehaviour()
            .Add(new CountingSuccessBehaviour(() => count++))
            .Add(new DelayBehaviour(0.2f));
        var context = new ActionBehaviourContext(0.1f, 0.1f, 1f, 1f);

        parallel.Reset();
        Assert.Equal(ActionBehaviourStatus.Running, parallel.Tick(in context));
        var snapshot = parallel.CaptureSnapshot();
        Assert.Equal(1, count);

        Assert.Equal(ActionBehaviourStatus.Success, parallel.Tick(in context));
        parallel.RestoreSnapshot(snapshot);
        Assert.Equal(ActionBehaviourStatus.Success, parallel.Tick(in context));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Repeat_and_timeout_decorators_have_deterministic_terminal_results()
    {
        var count = 0;
        var repeat = new RepeatBehaviour(new CallbackBehaviour(() => count++), count: 2);
        var context = new ActionBehaviourContext(0.1f, 0.1f, 1f, 1f);
        repeat.Reset();

        Assert.Equal(ActionBehaviourStatus.Running, repeat.Tick(in context));
        Assert.Equal(ActionBehaviourStatus.Success, repeat.Tick(in context));
        Assert.Equal(2, count);

        var probe = new ProbeBehaviour();
        var timeout = new TimeoutBehaviour(probe, duration: 0.2f);
        timeout.Reset();
        Assert.Equal(ActionBehaviourStatus.Running, timeout.Tick(in context));
        Assert.Equal(ActionBehaviourStatus.Failure, timeout.Tick(in context));
        Assert.Equal(1, probe.AbortCount);
    }

    private static AbilityKit.HFSM.StateMachine<string> Build(
        TestTimeSource time,
        TestBlackboard blackboard,
        IReadOnlyList<NodeSpec<string>> states,
        IReadOnlyList<TransitionSpec> transitions)
    {
        var builder = new HierarchicalProfileBuilder<TestBlackboard, string>(
            CreateAction,
            (bb, condition) => condition == "leave" && bb.Leave);
        return builder.Build(
            time,
            blackboard,
            new HierarchicalProfile<string>("test", states[0].Id, states, transitions));
    }

    private static NodeSpec<string> HoldState(string id, string action)
    {
        return new NodeSpec<string>(
            id,
            BehaviourSpec<string>.Task(action),
            ActionStateCompletionPolicy.Hold);
    }

    private static IActionBehaviour CreateAction(TestBlackboard blackboard, string action)
    {
        return action switch
        {
            "delay" => new DelayBehaviour(0.2f),
            "fail" => new FixedStatusBehaviour(ActionBehaviourStatus.Failure),
            "probe" => blackboard.Probe = new ProbeBehaviour(),
            _ => new CallbackBehaviour(() => blackboard.Trace.Add(action)),
        };
    }

    private sealed class TestBlackboard
    {
        public readonly List<string> Trace = new();
        public bool Leave;
        public ProbeBehaviour Probe;
    }

    private sealed class TestTimeSource : IActionTimeSource
    {
        public float DeltaTime { get; set; }
        public float UnscaledDeltaTime => DeltaTime;
    }

    private sealed class FixedStatusBehaviour : IRollbackActionBehaviour
    {
        private readonly ActionBehaviourStatus _status;

        public FixedStatusBehaviour(ActionBehaviourStatus status)
        {
            _status = status;
        }

        public void Reset()
        {
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx) => _status;

        public ActionBehaviourSnapshot CaptureSnapshot() =>
            new(nameof(FixedStatusBehaviour));

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
        }
    }

    private sealed class ProbeBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        public int TickCount { get; private set; }
        public int AbortCount { get; private set; }

        public void Reset()
        {
            TickCount = 0;
            AbortCount = 0;
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            TickCount++;
            return ActionBehaviourStatus.Running;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            AbortCount++;
        }

        public ActionBehaviourSnapshot CaptureSnapshot() =>
            new(nameof(ProbeBehaviour), TickCount, booleanValue: AbortCount > 0);

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            TickCount = snapshot.IntegerValue;
            AbortCount = snapshot.BooleanValue ? 1 : 0;
        }
    }

    private sealed class CountingSuccessBehaviour : IRollbackActionBehaviour
    {
        private readonly Action _onTick;

        public CountingSuccessBehaviour(Action onTick)
        {
            _onTick = onTick;
        }

        public void Reset()
        {
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            _onTick();
            return ActionBehaviourStatus.Success;
        }

        public ActionBehaviourSnapshot CaptureSnapshot() =>
            new(nameof(CountingSuccessBehaviour));

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
        }
    }
}
