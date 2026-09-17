using System.Runtime.CompilerServices;
using AbilityKit.Deterministic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using Xunit;

namespace AbilityKit.HFSM.Core.Tests;

public sealed class HfsmShowcaseConfigurationTests
{
    [Fact]
    public void PatrolBlockPreemptsMovementAndSnapshotRestoresFrame()
    {
        var owner = new Facts { Moving = true };
        var machine = Create(Load("hfsm_showcase_patrol"), owner);
        machine.Initialize(0, Fixed64.Zero);
        try
        {
            Tick(machine, 1);
            Assert.EndsWith("/Walking", machine.GetActivePath().Last());
            Assert.Contains(owner.Actions, action => action.Type == "play" && action.PlayState == "Walk");
            var saved = machine.CaptureSnapshot();
            owner.Blocked = true;
            Tick(machine, 2);
            Assert.EndsWith("/Blocked", machine.GetActivePath().Last());
            Assert.Contains(owner.Actions, action => action.Type == "log" && action.Message == "route blocked");
            machine.RestoreSnapshot(saved);
            Assert.Equal(1, machine.CurrentFrame);
            Assert.EndsWith("/Walking", machine.GetActivePath().Last());
        }
        finally { if (!machine.IsFaulted) machine.Shutdown(); }
    }

    [Fact]
    public void CombatNestedPriorityDeathAndTriggeredRespawn()
    {
        var owner = new Facts { Target = true, Distance = 1 };
        var machine = Create(Load("hfsm_showcase_combat"), owner);
        machine.Initialize(0, Fixed64.Zero);
        try
        {
            Tick(machine, 1);
            Assert.Equal(2, machine.GetActivePath().Count);
            Assert.EndsWith("/Strike", machine.GetActivePath().Last());
            Assert.Contains(owner.Actions, action => action.Type == "play" && action.PlayState == "Attack");
            owner.Distance = 5;
            Tick(machine, 2);
            Assert.EndsWith("/Strike", machine.GetActivePath().Last());
            Tick(machine, 4);
            Assert.EndsWith("/Chase", machine.GetActivePath().Last());
            owner.Health = 0;
            Tick(machine, 5);
            Assert.Single(machine.GetActivePath());
            Assert.EndsWith("/Dead", machine.GetActivePath().Last());
            owner.Health = 100;
            Tick(machine, 6);
            Assert.EndsWith("/Dead", machine.GetActivePath().Last());
            Assert.True(machine.Trigger("respawn"));
            Assert.Equal(2, machine.GetActivePath().Count);
            Assert.EndsWith("/Chase", machine.GetActivePath().Last());
        }
        finally { if (!machine.IsFaulted) machine.Shutdown(); }
    }

    [Fact]
    public void ChaseParallelPlaybackContinuesWhileFeedbackWaits()
    {
        var owner = new Facts { Target = true, Distance = 5 };
        var machine = Create(Load("hfsm_showcase_combat"), owner);
        machine.Initialize(0, Fixed64.Zero);
        try
        {
            Tick(machine, 1);
            Assert.EndsWith("/Chase", machine.GetActivePath().Last());
            Tick(machine, 2);
            Assert.DoesNotContain(owner.Actions, a => a.Message == "target pursued");
            Tick(machine, 3);
            Assert.Contains(owner.Actions, a => a.Type == "log" && a.Message == "target pursued" && a.LocalFrame == 2);
            Assert.Contains(owner.Actions, a => a.Type == "play" && a.PlayState == "Run" && a.LocalFrame == 2);
            Tick(machine, 4);
            Assert.Single(owner.Actions, a => a.Message == "target pursued");
        }
        finally { if (!machine.IsFaulted) machine.Shutdown(); }
    }

    [Fact]
    public void StrikePhasesUseExactFramesAndRestoreDoesNotReplayImpact()
    {
        var owner = new Facts { Target = true, Distance = 1 };
        var machine = Create(Load("hfsm_showcase_combat"), owner);
        machine.Initialize(0, Fixed64.Zero);
        try
        {
            Tick(machine, 1);
            for (var frame = 2; frame <= 4; frame++) Tick(machine, frame);
            Assert.Contains(owner.Actions, a => a.Message == "attack impact" && a.Frame == 4 && a.LocalFrame == 3);
            var saved = machine.CaptureSnapshot();
            Tick(machine, 5);
            Tick(machine, 6);
            Assert.Contains(owner.Actions, a => a.Message == "attack recovered" && a.Frame == 6 && a.LocalFrame == 5);

            machine.RestoreSnapshot(saved);
            Assert.Equal(4, machine.CurrentFrame);
            owner.Actions.Clear();
            Tick(machine, 5);
            Tick(machine, 6);
            Assert.DoesNotContain(owner.Actions, a => a.Message == "attack impact");
            Assert.Contains(owner.Actions, a => a.Message == "attack recovered" && a.Frame == 6);
            Assert.EndsWith("/Strike", machine.GetActivePath().Last());
        }
        finally { if (!machine.IsFaulted) machine.Shutdown(); }
    }

    private static StateMachineDefinition Load(string name)
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!,
            "../../Unity/Packages/com.abilitykit.hfsm/Samples~/CompleteHfsmShowcase/Resources/" + name + ".json"));
        return DefinitionJson.Load(File.ReadAllText(path));
    }

    private static StateMachineRuntime<Facts> Create(StateMachineDefinition definition, Facts owner)
    {
        var catalog = CompositeActionCatalog.LoadJson(File.ReadAllText(Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(SourcePath())!,
            "../../Unity/Packages/com.abilitykit.hfsm/Samples~/CompleteHfsmShowcase/Resources/hfsm_showcase_actions.json"))));
        var bindings = new RuntimeBindings<Facts>();
        bindings.RegisterState("showcase.state", () => new EmptyState());
        foreach (var key in new[] { "showcase.patrol.walk", "showcase.patrol.block",
                     "showcase.combat.chase", "showcase.combat.strike" })
        {
            var bindingKey = key;
            bindings.RegisterState(bindingKey,
                () => new CompositeStateAction<Facts>(catalog.Get(bindingKey), 10));
        }
        bindings.RegisterAction("showcase.transition", () => new EmptyAction());
        bindings.RegisterCondition("showcase.blocked", () => new Condition(f => f.Blocked));
        bindings.RegisterCondition("showcase.walk", () => new Condition(f => !f.Blocked && f.Moving));
        bindings.RegisterCondition("showcase.idle", () => new Condition(f => !f.Blocked && !f.Moving));
        bindings.RegisterCondition("showcase.dead", () => new Condition(f => f.Health <= 0));
        bindings.RegisterCondition("showcase.alive", () => new Condition(f => f.Health > 0));
        bindings.RegisterCondition("showcase.strike", () => new Condition(f => f.Target && f.Distance <= 2));
        bindings.RegisterCondition("showcase.chase", () => new Condition(f => f.Target && f.Distance > 2));
        bindings.RegisterCondition("showcase.no-target", () => new Condition(f => !f.Target));
        return new StateMachineRuntime<Facts>(owner, definition, bindings);
    }

    private static void Tick(StateMachineRuntime<Facts> machine, int frame) =>
        machine.Tick(frame, Fixed64.FromRatio(frame, 10));

    private static string SourcePath([CallerFilePath] string sourceFile = "") => sourceFile;

    private sealed class Facts : ICompositeActionSink
    {
        public bool Moving;
        public bool Blocked;
        public int Health = 100;
        public bool Target;
        public float Distance;
        public readonly List<CompositeActionIntent> Actions = new();
        public void OnCompositeAction(in CompositeActionIntent intent) => Actions.Add(intent);
    }

    private sealed class Condition : ITransitionCondition<Facts>
    {
        private readonly Func<Facts, bool> _predicate;
        public Condition(Func<Facts, bool> predicate) => _predicate = predicate;
        public bool Evaluate(Facts owner, in TransitionContext context) => _predicate(owner);
    }

    private sealed class EmptyState : RuntimeStateBase<Facts> { }

    private sealed class EmptyAction : ITransitionAction<Facts>
    {
        public void BeforeTransition(Facts owner, in TransitionContext context) { }
        public void AfterTransition(Facts owner, in TransitionContext context) { }
    }
}
