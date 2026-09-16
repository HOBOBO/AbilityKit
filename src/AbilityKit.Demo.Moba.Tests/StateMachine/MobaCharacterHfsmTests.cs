using System.Runtime.CompilerServices;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Game.Battle.Component;
using AbilityKit.HFSM.Definition;
using Newtonsoft.Json.Linq;
using System.Text;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateMachine;

public sealed class MobaCharacterHfsmTests
{
    [Fact]
    public void AuthoredDefinitionMatchesDefaultRuntimeGraph()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!,
            "../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/character_hfsm.json"));
        var definition = DefinitionJson.Load(File.ReadAllText(path));
        Assert.Equal(MobaCharacterHfsmProfile.Id, definition.DefinitionId);
        Assert.Equal(MobaCharacterHfsmProfile.CreateDefinition().ComputeDefinitionHash(),
            definition.ComputeDefinitionHash());
    }

    [Fact]
    public void ActionInstanceAndLocalFrameSurviveSnapshotRestore()
    {
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        using var source = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        source.Tick(1, Time(1), true, false, true, 0, 0);
        source.Tick(2, Time(2), true, false, true, 0, 0);
        Assert.Equal("life/alive/action/moving", source.Action.Path);
        Assert.Equal(1, source.Action.LocalFrame);

        source.Tick(3, Time(3), true, false, true, 321, 10001);
        source.Tick(4, Time(4), true, false, false, 321, 10001);
        var cast = source.CaptureSnapshot();
        Assert.Equal("life/alive/action/casting", cast.Action.Path);
        Assert.Equal(1, cast.Action.LocalFrame);
        Assert.Equal(321, cast.Action.CastInstanceId);
        Assert.Equal(10001, cast.Action.SkillId);

        using var restored = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        restored.RestoreSnapshot(cast);
        Assert.Equal(cast.Action.InstanceId, restored.Action.InstanceId);
        Assert.Equal(cast.Action.LocalFrame, restored.Action.LocalFrame);
        restored.Tick(5, Time(5), true, true, false, 321, 10001);
        Assert.Equal("life/alive/action/controlled", restored.Action.Path);
        restored.Tick(6, Time(6), false, true, false, 321, 10001);
        Assert.Equal("life/dead", restored.Action.Path);
        restored.Tick(7, Time(7), true, false, false, 0, 0);
        Assert.Equal("life/alive/action/idle", restored.Action.Path);
    }

    [Fact]
    public void InvalidActionDoesNotPartiallyRestoreRuntime()
    {
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        using var runtime = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        runtime.Tick(1, Time(1), true, false, false, 0, 0);
        var original = runtime.Action;
        var snapshot = runtime.CaptureSnapshot();
        snapshot.Action = new MobaCharacterActionState(original.Path, original.InstanceId,
            original.StartFrame, original.LocalFrame + 1, 0, 0);
        Assert.Throws<InvalidOperationException>(() => runtime.RestoreSnapshot(snapshot));
        Assert.Equal(1, runtime.Frame);
        Assert.Equal(original.LocalFrame, runtime.Action.LocalFrame);
    }

    [Fact]
    public void ViewRestoresLocalFrameWithoutTransitionReplay()
    {
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        using var logic = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        logic.Tick(1, Time(1), true, false, false, 321, 10001);
        logic.Tick(2, Time(2), true, false, false, 321, 10001);
        var view = new BattleCharacterHfsmComponent(definition);
        view.ApplySnapshot(logic.CaptureSnapshot());
        Assert.Equal(1, view.Action.LocalFrame);
        Assert.Equal(logic.DefinitionHash, view.DefinitionHash);

        logic.Tick(3, Time(3), true, false, false, 321, 10001);
        var invalid = logic.CaptureSnapshot();
        invalid.Action = new MobaCharacterActionState(invalid.Action.Path,
            invalid.Action.InstanceId, invalid.Action.StartFrame, 99, 321, 10001);
        Assert.Throws<InvalidOperationException>(() => view.ApplySnapshot(invalid));
        Assert.Equal(2, view.SnapshotFrame);
        Assert.Equal(1, view.Action.LocalFrame);
    }

    [Fact]
    public void EntitasRollbackProviderRestoresActionFrame()
    {
        var context = new ActorContext();
        var actor = context.CreateEntity();
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        var registry = new MobaActorRegistry();
        try
        {
            registry.Register(17, actor);
            actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero));
            actor.characterHfsm.Runtime.Tick(1, Time(1), true, false, false, 321, 10001);
            actor.characterHfsm.Runtime.Tick(2, Time(2), true, false, false, 321, 10001);
            var provider = new MobaCharacterHfsmRollbackProvider(registry, definition);
            var bytes = provider.Export(new FrameIndex(2));
            var expected = actor.characterHfsm.Runtime.Action;
            actor.characterHfsm.Runtime.Tick(3, Time(3), true, false, true, 0, 0);

            provider.Import(new FrameIndex(2), bytes);
            Assert.Equal(2, actor.characterHfsm.Runtime.Frame);
            Assert.Equal(expected.Path, actor.characterHfsm.Runtime.Action.Path);
            Assert.Equal(expected.InstanceId, actor.characterHfsm.Runtime.Action.InstanceId);
            Assert.Equal(1, actor.characterHfsm.Runtime.Action.LocalFrame);
        }
        finally
        {
            if (actor.hasCharacterHfsm) actor.RemoveCharacterHfsm();
            actor.Destroy();
        }
    }

    [Fact]
    public void InvalidSecondActorDoesNotPartiallyImportFirstActor()
    {
        var context = new ActorContext();
        var first = context.CreateEntity();
        var second = context.CreateEntity();
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        var registry = new MobaActorRegistry();
        try
        {
            registry.Register(17, first);
            registry.Register(18, second);
            first.AddCharacterHfsm(new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero));
            second.AddCharacterHfsm(new MobaCharacterHfsmRuntime(18, definition, 0, Fixed64.Zero));
            first.characterHfsm.Runtime.Tick(1, Time(1), true, false, true, 0, 0);
            second.characterHfsm.Runtime.Tick(1, Time(1), true, false, false, 0, 0);
            var provider = new MobaCharacterHfsmRollbackProvider(registry, definition);
            var entries = JArray.Parse(Encoding.UTF8.GetString(provider.Export(new FrameIndex(1))));
            entries[1]!["LocalFrame"] = 999;
            first.characterHfsm.Runtime.Tick(2, Time(2), true, false, false, 0, 0);
            var original = first.characterHfsm.Runtime.Action;

            Assert.Throws<InvalidOperationException>(() => provider.Import(new FrameIndex(1),
                Encoding.UTF8.GetBytes(entries.ToString())));
            Assert.Equal(2, first.characterHfsm.Runtime.Frame);
            Assert.Equal(original.Path, first.characterHfsm.Runtime.Action.Path);
            Assert.Equal(original.InstanceId, first.characterHfsm.Runtime.Action.InstanceId);
        }
        finally
        {
            if (first.hasCharacterHfsm) first.RemoveCharacterHfsm();
            if (second.hasCharacterHfsm) second.RemoveCharacterHfsm();
            first.Destroy();
            second.Destroy();
        }
    }

    [Fact]
    public void HeadlessSequenceWaitAndLiveReplacementSeekFromLocalFrame()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!,
            "../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/character_view_actions.json"));
        var catalog = CharacterPresentationActionCatalog.LoadJson(File.ReadAllText(path));
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        using var logic = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        var view = new BattleCharacterHfsmComponent(definition, catalog);
        var sink = new HeadlessSink();

        logic.Tick(1, Time(1), true, false, false, 321, 10001);
        view.ApplySnapshot(logic.CaptureSnapshot());
        Assert.Null(view.Evaluate(1, 30, sink));
        Assert.Single(sink.Logs);
        view.Evaluate(1, 30, sink);
        Assert.Single(sink.Logs);

        var replacement = new CharacterPresentationAction
        {
            Id = "ignored", Type = "sequence", Actions = new List<CharacterPresentationAction>
            {
                new() { Id = "attack.play", Type = "play", AnimatorState = "Attack", FrameCount = 12 },
                new() { Id = "attack.wait", Type = "wait", Seconds = 0.5m }
            }
        };
        catalog.ReplaceAction("life/alive/action/casting", "attack.phase", replacement);
        var start = view.Evaluate(1, 30, sink);
        Assert.Equal("Attack", start!.Value.AnimatorState);
        Assert.Equal(0, start.Value.LocalFrame);
        Assert.Equal(2, start.Value.BindingVersion);
        Assert.Single(sink.Plays);
        Assert.Single(sink.Logs);

        logic.Tick(2, Time(2), true, false, false, 321, 10001);
        view.ApplySnapshot(logic.CaptureSnapshot());
        var middle = view.Evaluate(2, 30, sink);
        Assert.Equal(1, middle!.Value.LocalFrame);
        Assert.Equal(2, sink.Plays.Count);

        var restored = new BattleCharacterHfsmComponent(definition, catalog);
        restored.ApplySnapshot(logic.CaptureSnapshot());
        var seek = restored.Evaluate(2, 30, sink);
        Assert.Equal(1, seek!.Value.LocalFrame);
        Assert.Equal(3, sink.Plays.Count);
        Assert.Single(sink.Logs);
    }

    [Fact]
    public void SequenceOffsetsAreDeterministicAndInvalidReplacementIsAtomic()
    {
        var catalog = CharacterPresentationActionCatalog.LoadJson("""
        {"schemaVersion":1,"states":[{"path":"life/alive/action/casting","action":
          {"id":"root","type":"sequence","actions":[
            {"id":"wait","type":"wait","seconds":0.5},
            {"id":"play","type":"play","animatorState":"Attack"},
            {"id":"log","type":"log","message":"done"}]}}]}
        """);
        var runner = new CharacterPresentationActionRunner(catalog);
        var sink = new HeadlessSink();
        Assert.Null(runner.Evaluate("life/alive/action/casting", 123, 14, 14, 30, sink));
        Assert.Empty(sink.Logs);
        Assert.Equal("Attack", runner.Evaluate("life/alive/action/casting", 123, 15, 15, 30, sink)!.Value.AnimatorState);
        Assert.Single(sink.Logs);

        Assert.Throws<InvalidOperationException>(() => catalog.ReplaceAction(
            "life/alive/action/casting", "wait",
            new CharacterPresentationAction { Type = "wait", Seconds = -1 }));
        Assert.Equal(1, catalog.Version);
        Assert.Equal("Attack", runner.Evaluate("life/alive/action/casting", 123, 16, 16, 30, sink)!.Value.AnimatorState);
    }

    [Fact]
    public void AuthoredHeroOverrideAndLiveActorReplacementAreIsolated()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!,
            "../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/character_view_actions.json"));
        var catalog = CharacterPresentationActionCatalog.LoadJson(File.ReadAllText(path));
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        using var logic = new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero);
        logic.Tick(1, Time(1), true, false, false, 321, 10001);

        var hero = new BattleCharacterHfsmComponent(definition, catalog, 1001);
        var other = new BattleCharacterHfsmComponent(definition, catalog, 1002);
        hero.ApplySnapshot(logic.CaptureSnapshot());
        other.ApplySnapshot(logic.CaptureSnapshot());
        Assert.Equal("Attack", hero.Evaluate(1, 30)!.Value.AnimatorState);
        Assert.Null(other.Evaluate(1, 30));

        hero.ReplaceAction("life/alive/action/casting", "attack.phase",
            new CharacterPresentationAction { Type = "play", AnimatorState = "HeavyAttack" });
        Assert.Equal("HeavyAttack", hero.Evaluate(1, 30)!.Value.AnimatorState);
        Assert.Null(other.Evaluate(1, 30));
        Assert.Equal(logic.DefinitionHash, hero.DefinitionHash);
    }

    private sealed class HeadlessSink : ICharacterPresentationSink
    {
        public readonly List<CharacterPlaybackIntent> Plays = new();
        public readonly List<CharacterLogIntent> Logs = new();
        public void OnPlay(in CharacterPlaybackIntent intent) => Plays.Add(intent);
        public void OnLog(in CharacterLogIntent intent) => Logs.Add(intent);
    }

    private static Fixed64 Time(int frame) => Fixed64.FromRaw(frame * 1000L);

    private static string SourcePath([CallerFilePath] string sourceFile = "") => sourceFile;
}
