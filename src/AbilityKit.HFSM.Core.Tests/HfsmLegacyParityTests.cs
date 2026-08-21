using AbilityKit.Deterministic;
using AbilityKit.HFSM;
using UnityHFSM;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

/// <summary>
/// Executable characterization of the concepts retained from UnityHFSM. These tests compare
/// observable behavior rather than implementation details so the legacy runtime can be retired.
/// </summary>
public sealed class HfsmLegacyParityTests
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

        var definition = HfsmFixtures.Flat(
            HfsmFixtures.State("a"),
            HfsmFixtures.State("local"),
            HfsmFixtures.State("global"));
        definition.Machines[0].Transitions.Add(
            HfsmFixtures.Transition("local", "a", "local"));
        definition.Machines[0].Transitions.Add(
            HfsmFixtures.Transition("global", string.Empty, "global", fromAny: true));
        var next = new HfsmRuntime<TestOwner>(
            new TestOwner(), definition, new HfsmRuntimeBindings<TestOwner>());
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
        var next = new HfsmRuntime<TestOwner>(
            new TestOwner(), definition, new HfsmRuntimeBindings<TestOwner>());
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
        var next = new HfsmRuntime<TestOwner>(
            new TestOwner(), definition, new HfsmRuntimeBindings<TestOwner>());
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
        var definition = HfsmFixtures.Flat(
            HfsmFixtures.State("cast", "approval", requiresExitApproval: true),
            HfsmFixtures.State("idle", "idle"));
        definition.Machines[0].Transitions.Add(
            HfsmFixtures.Transition("finish", "cast", "idle", trigger: "finish"));
        var next = new HfsmRuntime<TestOwner>(
            owner,
            definition,
            new HfsmRuntimeBindings<TestOwner>()
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
        var definition = HfsmFixtures.Flat(
            HfsmFixtures.State("cast", "cast", requiresExitApproval: true),
            HfsmFixtures.State("first"),
            HfsmFixtures.State("second"));
        definition.Machines[0].Transitions.Add(
            HfsmFixtures.Transition("first", "cast", "first", trigger: "first"));
        definition.Machines[0].Transitions.Add(
            HfsmFixtures.Transition("second", "cast", "second", trigger: "second"));
        var next = new HfsmRuntime<TestOwner>(
            owner,
            definition,
            new HfsmRuntimeBindings<TestOwner>()
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
        var definition = HfsmFixtures.Flat(
            HfsmFixtures.State("a", "a"),
            HfsmFixtures.State("b", "b"));
        definition.Machines[0].Transitions.Add(
            HfsmFixtures.Transition("a-b", "a", "b"));
        definition.Machines[0].Transitions[0].ActionKey = "a-b";
        var machine = new HfsmRuntime<TestOwner>(
            owner,
            definition,
            new HfsmRuntimeBindings<TestOwner>()
                .RegisterState("a", () => new TraceState("a"))
                .RegisterState("b", () => new TraceState("b"))
                .RegisterAction("a-b", () => new TraceTransitionAction("a-b")));
        machine.Initialize(0, Fixed64.Zero);
        machine.Tick(1, Fixed64.One);
        return owner.Trace;
    }

    private static HfsmDefinition CreateTriggerHierarchy()
    {
        return new HfsmDefinition
        {
            RootMachineId = "root",
            Machines =
            {
                new HfsmMachineDefinition
                {
                    Id = "root",
                    InitialStateId = "active",
                    States =
                    {
                        HfsmFixtures.State("active", childMachine: "child"),
                        HfsmFixtures.State("done"),
                    },
                    Transitions =
                    {
                        HfsmFixtures.Transition("root-go", "active", "done", trigger: "go"),
                    },
                },
                new HfsmMachineDefinition
                {
                    Id = "child",
                    InitialStateId = "idle",
                    States =
                    {
                        HfsmFixtures.State("idle"),
                        HfsmFixtures.State("attack"),
                    },
                    Transitions =
                    {
                        HfsmFixtures.Transition("child-go", "idle", "attack", trigger: "go"),
                    },
                },
            },
        };
    }

    private static HfsmDefinition CreateRememberHierarchy()
    {
        var definition = CreateTriggerHierarchy();
        var root = definition.Machines.Single(machine => machine.Id == "root");
        root.States.Single(state => state.Id == "done").Id = "outside";
        root.Transitions.Clear();
        root.Transitions.Add(HfsmFixtures.Transition("leave", "active", "outside", trigger: "leave"));
        root.Transitions.Add(HfsmFixtures.Transition("return", "outside", "active", trigger: "return"));

        var child = definition.Machines.Single(machine => machine.Id == "child");
        child.RememberLastState = true;
        child.Transitions.Clear();
        child.Transitions.Add(HfsmFixtures.Transition("attack", "idle", "attack", trigger: "attack"));
        return definition;
    }

    private sealed class ApprovingOnTickState : HfsmStateBase<TestOwner>
    {
        public override void OnEnter(TestOwner owner, in HfsmTickContext context)
            => owner.Trace.Add("enter:cast");

        public override void OnTick(TestOwner owner, in HfsmTickContext context)
        {
            owner.Trace.Add("tick:cast");
            owner.AllowExit = true;
        }

        public override bool CanExit(TestOwner owner, in HfsmTickContext context) => owner.AllowExit;

        public override void OnExit(TestOwner owner, in HfsmTickContext context)
            => owner.Trace.Add("exit:cast");
    }

    private sealed class TraceTransitionAction : IHfsmTransitionAction<TestOwner>
    {
        private readonly string _id;

        public TraceTransitionAction(string id)
        {
            _id = id;
        }

        public void BeforeTransition(TestOwner owner, in HfsmTransitionContext context)
            => owner.Trace.Add("before:" + _id);

        public void AfterTransition(TestOwner owner, in HfsmTransitionContext context)
            => owner.Trace.Add("after:" + _id);
    }
}
