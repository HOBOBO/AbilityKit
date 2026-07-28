using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using UnityHFSM.Extension;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateMachine;

public sealed class MobaActorStateMachineRuntimeTests
{
    private const string HierarchicalProfileJson = """
        [
          {
            "id": "combat",
            "startState": "engage",
            "states": [
              {
                "id": "engage",
                "kind": "stateMachine",
                "startState": "approach",
                "states": [
                  {
                    "id": "approach",
                    "kind": "actionState",
                    "actions": [ { "type": "count", "argument": "approach" } ]
                  }
                ]
              },
              {
                "id": "done",
                "kind": "actionState",
                "actions": [ { "type": "count", "argument": "done" } ]
              }
            ],
            "transitions": [
              { "from": "engage", "to": "done", "condition": "flag:finish" }
            ]
          }
        ]
        """;

    [Fact]
    public void Json_profile_builds_and_ticks_hierarchical_runtime()
    {
        var catalog = new MobaActorStateMachineProfileCatalog();
        Assert.Equal(1, MobaActorStateMachineProfileJsonLoader.LoadJson(HierarchicalProfileJson, catalog));

        var counters = new Dictionary<string, int>();
        var finish = false;
        var registry = new MobaActorStateMachineRuntimeRegistry();
        registry.RegisterAction("count", (_, argument) => new CallbackBehaviour(() =>
        {
            counters.TryGetValue(argument, out var count);
            counters[argument] = count + 1;
        }));
        registry.RegisterCondition("flag", (_, argument) => argument == "finish" && finish);

        var actorContext = new ActorContext();
        var actor = actorContext.CreateEntity();
        var factory = new MobaActorStateMachineFactory(null, catalog, registry);

        Assert.True(factory.TryCreate(actor, "combat", out var runtime));
        Assert.Contains("engage", runtime.StateMachine.GetActiveHierarchyPath());
        Assert.Contains("approach", runtime.StateMachine.GetActiveHierarchyPath());

        runtime.Tick(new FrameIndex(20), 0.1f);
        Assert.Equal(1, counters["approach"]);
        Assert.Equal(0.1f, runtime.DeltaTime);
        Assert.Equal(20, runtime.State.EnteredFrame);
        Assert.Equal(0, runtime.State.DurationFrames);
        Assert.Equal(0.1f, runtime.State.DurationSeconds);

        runtime.Tick(new FrameIndex(21), 0.1f);
        Assert.Equal(1, runtime.State.DurationFrames);
        Assert.Equal(0.2f, runtime.State.DurationSeconds);

        finish = true;
        runtime.Tick(new FrameIndex(22), 0.2f);
        Assert.Equal(1, counters["done"]);
        Assert.Contains("done", runtime.StateMachine.GetActiveHierarchyPath());
        Assert.Contains("done", runtime.State.ActiveStatePath);
        Assert.Equal(22, runtime.State.EnteredFrame);
        Assert.Equal(0, runtime.State.DurationFrames);
        Assert.Equal(0f, runtime.State.DurationSeconds);
    }

    [Fact]
    public void Actor_component_replacement_and_removal_dispose_owned_runtimes()
    {
        var catalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(HierarchicalProfileJson, catalog);
        var registry = new MobaActorStateMachineRuntimeRegistry();
        registry.RegisterAction("count", (_, __) => new CallbackBehaviour(null));
        registry.RegisterCondition("flag", (_, __) => false);

        var actorContext = new ActorContext();
        var actor = actorContext.CreateEntity();
        var factory = new MobaActorStateMachineFactory(null, catalog, registry);
        Assert.True(factory.TryCreate(actor, "combat", out var first));
        Assert.True(factory.TryCreate(actor, "combat", out var second));

        actor.AddActorStateMachine("combat", first);
        actor.ReplaceActorStateMachine("combat", second);

        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Same(second, actor.actorStateMachine.Runtime);

        actor.RemoveActorStateMachine();

        Assert.True(second.IsDisposed);
        Assert.False(actor.hasActorStateMachine);
    }

    [Fact]
    public void Invalid_nested_transition_is_rejected_when_runtime_is_built()
    {
        const string invalidJson = """
            [
              {
                "id": "invalid",
                "states": [
                  {
                    "id": "nested",
                    "kind": "machine",
                    "states": [ { "id": "only", "kind": "action" } ],
                    "transitions": [ { "from": "only", "to": "missing", "condition": "always" } ]
                  }
                ]
              }
            ]
            """;

        var catalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(invalidJson, catalog);
        var actor = new ActorContext().CreateEntity();
        var factory = new MobaActorStateMachineFactory(
            null,
            catalog,
            new MobaActorStateMachineRuntimeRegistry());

        var error = Assert.Throws<InvalidOperationException>(() => factory.TryCreate(actor, "invalid", out _));
        Assert.Contains("missing", error.Message);
    }
    [Fact]
    public void Rollback_provider_restores_nested_action_progress_from_binary_snapshot()
    {
        const string json = """
            [
              {
                "id": "timed",
                "startState": "wait",
                "states": [
                  {
                    "id": "wait",
                    "kind": "action",
                    "actions": [ { "type": "delay", "argument": "1" } ]
                  }
                ]
              }
            ]
            """;

        var catalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(json, catalog);
        var runtimeRegistry = new MobaActorStateMachineRuntimeRegistry();
        runtimeRegistry.RegisterAction("delay", (_, argument) => new DelayBehaviour(float.Parse(argument)));

        var actor = new ActorContext().CreateEntity();
        var actors = new MobaActorRegistry();
        actors.Register(7, actor);
        var factory = new MobaActorStateMachineFactory(null, catalog, runtimeRegistry);
        Assert.True(factory.TryCreate(actor, "timed", out var runtime));
        actor.AddActorStateMachine("timed", runtime);
        var provider = new MobaActorStateMachineRollbackProvider(actors, factory);
        var actionState = Assert.IsAssignableFrom<CompositeActionState<string, string>>(
            runtime.StateMachine.GetState("wait"));

        runtime.Tick(new FrameIndex(10), 0.25f);
        runtime.Tick(new FrameIndex(11), 0.25f);
        var payload = provider.Export(new FrameIndex(11));
        runtime.Tick(new FrameIndex(12), 0.8f);
        Assert.True(actionState.IsCompleted);

        provider.Import(new FrameIndex(11), payload);

        Assert.False(actionState.IsCompleted);
        Assert.Equal(0.25f, runtime.DeltaTime);
        Assert.Equal(10, runtime.State.EnteredFrame);
        Assert.Equal(11, runtime.State.LastUpdatedFrame);
        Assert.Equal(1, runtime.State.DurationFrames);
        Assert.Equal(0.5f, runtime.State.DurationSeconds);

        runtime.Tick(new FrameIndex(12), 0.4f);
        Assert.False(actionState.IsCompleted);
        Assert.Equal(2, runtime.State.DurationFrames);
        Assert.Equal(0.9f, runtime.State.DurationSeconds, 3);
        runtime.Tick(new FrameIndex(13), 0.2f);
        Assert.True(actionState.IsCompleted);
    }

    [Fact]
    public void Rollback_provider_removes_runtime_absent_at_snapshot_frame()
    {
        var catalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(HierarchicalProfileJson, catalog);
        var runtimeRegistry = new MobaActorStateMachineRuntimeRegistry();
        runtimeRegistry.RegisterAction("count", (_, __) => new CallbackBehaviour(null));
        runtimeRegistry.RegisterCondition("flag", (_, __) => false);

        var actor = new ActorContext().CreateEntity();
        var actors = new MobaActorRegistry();
        actors.Register(9, actor);
        var factory = new MobaActorStateMachineFactory(null, catalog, runtimeRegistry);
        var provider = new MobaActorStateMachineRollbackProvider(actors, factory);
        var payload = provider.Export(new FrameIndex(20));

        Assert.True(factory.TryCreate(actor, "combat", out var predictedRuntime));
        actor.AddActorStateMachine("combat", predictedRuntime);
        provider.Import(new FrameIndex(20), payload);

        Assert.False(actor.hasActorStateMachine);
        Assert.True(predictedRuntime.IsDisposed);
    }
}
