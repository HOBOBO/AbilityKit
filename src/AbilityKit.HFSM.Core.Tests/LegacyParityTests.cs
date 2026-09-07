using AbilityKit.Deterministic;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

/// <summary>
/// Executable characterization of the concepts retained from AbilityKit.HFSM. These tests compare
/// observable behavior rather than implementation details so the legacy runtime can be retired.
/// </summary>
public sealed class LegacyParityTests
{
    [Fact]
    public void TransitionLifecycleAndNewStateTickHaveParity()
    {
        var legacy = RunLegacyLifecycle();
        var next = RunNextLifecycle();

        Assert.Equal(legacy, next);
        Assert.Equal(
            new[] { "enter:a", "before:a-b", "exit:a", "enter:b", "after:a-b", "tick:b" },
            next);
    }

    [Fact]
    public void FromAnyTransitionPrecedesLocalTransitionInBothRuntimes()
    {
        var legacy = new StateMachine();
        legacy.AddState("a", new State());
        legacy.AddState("local", new State());
        legacy.AddState("global", new State());
        legacy.AddTransition(new Transition("a", "local"));
        legacy.AddTransitionFromAny(new Transition(string.Empty, "global"));
        legacy.Init();
        legacy.OnLogic();

        var definition = Fixtures.Flat(
            Fixtures.State("a"),
            Fixtures.State("local"),
            Fixtures.State("global"));
        definition.Machines[0].Transitions.Add(
            Fixtures.Transition("local", "a", "local"));
        definition.Machines[0].Transitions.Add(
            Fixtures.Transition("global", string.Empty, "global", fromAny: true));
        var next = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());
        next.Initialize(0, Fixed64.Zero);
        next.Tick(1, Fixed64.One);

        Assert.Equal("global", legacy.ActiveStateName);
        Assert.Equal(new[] { "root/global" }, next.GetActivePath());
    }

    [Fact]
    public void TriggerPropagationIsRootFirstInBothHierarchies()
    {
        var legacyChild = new StateMachine();
        legacyChild.AddState("idle", new State());
        legacyChild.AddState("attack", new State());
        legacyChild.AddTriggerTransition("go", new Transition("idle", "attack"));

        var legacyRoot = new StateMachine();
        legacyRoot.AddState("active", legacyChild);
        legacyRoot.AddState("done", new State());
        legacyRoot.AddTriggerTransition("go", new Transition("active", "done"));
        legacyRoot.Init();
        legacyRoot.Trigger("go");

        var definition = CreateTriggerHierarchy();
        var next = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());
        next.Initialize(0, Fixed64.Zero);
        Assert.True(next.Trigger("go"));

        Assert.Equal("done", legacyRoot.ActiveStateName);
        Assert.Equal(new[] { "root/done" }, next.GetActivePath());
    }

    [Fact]
    public void RememberLastStateRestoresNestedMachineInBothRuntimes()
    {
        var legacyChild = new StateMachine(rememberLastState: true);
        legacyChild.AddState("idle", new State());
        legacyChild.AddState("attack", new State());
        legacyChild.AddTriggerTransition("attack", new Transition("idle", "attack"));
        var legacyRoot = new StateMachine();
        legacyRoot.AddState("active", legacyChild);
        legacyRoot.AddState("outside", new State());
        legacyRoot.AddTriggerTransition("leave", new Transition("active", "outside"));
        legacyRoot.AddTriggerTransition("return", new Transition("outside", "active"));
        legacyRoot.Init();
        legacyRoot.Trigger("attack");
        legacyRoot.Trigger("leave");
        legacyRoot.Trigger("return");

        var definition = CreateRememberHierarchy();
        var next = new StateMachineRuntime<TestOwner>(
            new TestOwner(), definition, new RuntimeBindings<TestOwner>());
        next.Initialize(0, Fixed64.Zero);
        Assert.True(next.Trigger("attack"));
        Assert.True(next.Trigger("leave"));
        Assert.True(next.Trigger("return"));

        Assert.Equal("active", legacyRoot.ActiveStateName);
        Assert.Equal("attack", legacyChild.ActiveStateName);
        Assert.Equal(new[] { "root/active", "child/attack" }, next.GetActivePath());
    }

    [Fact]
    public void PendingApprovalRunsFinalSourceTickBeforeTransitionInBothRuntimes()
    {
        var legacyTrace = new List<string>();
        var legacyCanExit = false;
        var legacy = new StateMachine();
        legacy.AddState("cast", new State(
            onEnter: _ => legacyTrace.Add("enter:cast"),
            onLogic: _ =>
            {
                legacyTrace.Add("tick:cast");
                legacyCanExit = true;
            },
            onExit: _ => legacyTrace.Add("exit:cast"),
            canExit: _ => legacyCanExit,
            needsExitTime: true));
        legacy.AddState("idle", new State(
            onEnter: _ => legacyTrace.Add("enter:idle"),
            onLogic: _ => legacyTrace.Add("tick:idle")));
        legacy.AddTriggerTransition("finish", new Transition("cast", "idle"));
        legacy.Init();
        legacy.Trigger("finish");
        legacy.OnLogic();

        var owner = new TestOwner();
        var definition = Fixtures.Flat(
            Fixtures.State("cast", "approval", requiresExitApproval: true),
            Fixtures.State("idle", "idle"));
        definition.Machines[0].Transitions.Add(
            Fixtures.Transition("finish", "cast", "idle", trigger: "finish"));
        var next = new StateMachineRuntime<TestOwner>(
            owner,
            definition,
            new RuntimeBindings<TestOwner>()
                .RegisterState("approval", () => new ApprovingOnTickState())
                .RegisterState("idle", () => new TraceState("idle")));
        next.Initialize(0, Fixed64.Zero);
        Assert.True(next.Trigger("finish"));
        next.Tick(1, Fixed64.One);

        Assert.Equal(
            new[] { "enter:cast", "tick:cast", "exit:cast", "enter:idle" },
            legacyTrace);
        Assert.Equal(legacyTrace, owner.Trace);
        Assert.Equal(new[] { "root/idle" }, next.GetActivePath());
    }

    [Fact]
    public void PendingReplacementIsAnIntentionalDeterministicStrengthening()
    {
        var legacyCanExit = false;
        var legacy = new StateMachine();
        legacy.AddState("cast", new State(canExit: _ => legacyCanExit, needsExitTime: true));
        legacy.AddState("first", new State());
        legacy.AddState("second", new State());
        legacy.AddTriggerTransition("first", new Transition("cast", "first"));
        legacy.AddTriggerTransition("second", new Transition("cast", "second"));
        legacy.Init();
        legacy.Trigger("first");
        legacy.Trigger("second");

        var owner = new TestOwner();
        var definition = Fixtures.Flat(
            Fixtures.State("cast", "cast", requiresExitApproval: true),
            Fixtures.State("first"),
            Fixtures.State("second"));
        definition.Machines[0].Transitions.Add(
            Fixtures.Transition("first", "cast", "first", trigger: "first"));
        definition.Machines[0].Transitions.Add(
            Fixtures.Transition("second", "cast", "second", trigger: "second"));
        var next = new StateMachineRuntime<TestOwner>(
            owner,
            definition,
            new RuntimeBindings<TestOwner>()
                .RegisterState("cast", () => new TraceState("cast")));
        next.Initialize(0, Fixed64.Zero);

        Assert.True(next.Trigger("first"));
        Assert.False(next.Trigger("second"));
        Assert.Equal("second", legacy.PendingStateName);
        Assert.Equal("first", next.CaptureSnapshot().Machines.Single().PendingTransitionId);
    }

    private static IReadOnlyList<string> RunLegacyLifecycle()
    {
        var trace = new List<string>();
        var machine = new StateMachine();
        machine.AddState("a", new State(
            onEnter: _ => trace.Add("enter:a"),
            onLogic: _ => trace.Add("tick:a"),
            onExit: _ => trace.Add("exit:a")));
        machine.AddState("b", new State(
            onEnter: _ => trace.Add("enter:b"),
            onLogic: _ => trace.Add("tick:b"),
            onExit: _ => trace.Add("exit:b")));
        machine.AddTransition(new Transition(
            "a",
            "b",
            onTransition: _ => trace.Add("before:a-b"),
            afterTransition: _ => trace.Add("after:a-b")));
        machine.Init();
        machine.OnLogic();
        return trace;
    }

    private static IReadOnlyList<string> RunNextLifecycle()
    {
        var owner = new TestOwner();
        var definition = Fixtures.Flat(
            Fixtures.State("a", "a"),
            Fixtures.State("b", "b"));
        definition.Machines[0].Transitions.Add(
            Fixtures.Transition("a-b", "a", "b"));
        definition.Machines[0].Transitions[0].ActionKey = "a-b";
        var machine = new StateMachineRuntime<TestOwner>(
            owner,
            definition,
            new RuntimeBindings<TestOwner>()
                .RegisterState("a", () => new TraceState("a"))
                .RegisterState("b", () => new TraceState("b"))
                .RegisterAction("a-b", () => new TraceTransitionAction("a-b")));
        machine.Initialize(0, Fixed64.Zero);
        machine.Tick(1, Fixed64.One);
        return owner.Trace;
    }

    private static StateMachineDefinition CreateTriggerHierarchy()
    {
        return new StateMachineDefinition
        {
            RootMachineId = "root",
            Machines =
            {
                new MachineDefinition
                {
                    Id = "root",
                    InitialStateId = "active",
                    States =
                    {
                        Fixtures.State("active", childMachine: "child"),
                        Fixtures.State("done"),
                    },
                    Transitions =
                    {
                        Fixtures.Transition("root-go", "active", "done", trigger: "go"),
                    },
                },
                new MachineDefinition
                {
                    Id = "child",
                    InitialStateId = "idle",
                    States =
                    {
                        Fixtures.State("idle"),
                        Fixtures.State("attack"),
                    },
                    Transitions =
                    {
                        Fixtures.Transition("child-go", "idle", "attack", trigger: "go"),
                    },
                },
            },
        };
    }

    private static StateMachineDefinition CreateRememberHierarchy()
    {
        var definition = CreateTriggerHierarchy();
        var root = definition.Machines.Single(machine => machine.Id == "root");
        root.States.Single(state => state.Id == "done").Id = "outside";
        root.Transitions.Clear();
        root.Transitions.Add(Fixtures.Transition("leave", "active", "outside", trigger: "leave"));
        root.Transitions.Add(Fixtures.Transition("return", "outside", "active", trigger: "return"));

        var child = definition.Machines.Single(machine => machine.Id == "child");
        child.RememberLastState = true;
        child.Transitions.Clear();
        child.Transitions.Add(Fixtures.Transition("attack", "idle", "attack", trigger: "attack"));
        return definition;
    }

    private sealed class ApprovingOnTickState : RuntimeStateBase<TestOwner>
    {
        public override void OnEnter(TestOwner owner, in TickContext context)
            => owner.Trace.Add("enter:cast");

        public override void OnTick(TestOwner owner, in TickContext context)
        {
            owner.Trace.Add("tick:cast");
            owner.AllowExit = true;
        }

        public override bool CanExit(TestOwner owner, in TickContext context) => owner.AllowExit;

        public override void OnExit(TestOwner owner, in TickContext context)
            => owner.Trace.Add("exit:cast");
    }

    private sealed class TraceTransitionAction : ITransitionAction<TestOwner>
    {
        private readonly string _id;

        public TraceTransitionAction(string id)
        {
            _id = id;
        }

        public void BeforeTransition(TestOwner owner, in TransitionContext context)
            => owner.Trace.Add("before:" + _id);

        public void AfterTransition(TestOwner owner, in TransitionContext context)
            => owner.Trace.Add("after:" + _id);
    }
}
