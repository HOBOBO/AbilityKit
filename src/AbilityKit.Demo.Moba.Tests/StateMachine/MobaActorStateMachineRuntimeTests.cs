using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using MemoryPack;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using Newtonsoft.Json;
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
                    "behaviorRoot": { "kind": "action", "type": "count", "argument": "approach" }
                  }
                ]
              },
              {
                "id": "done",
                "kind": "actionState",
                "behaviorRoot": { "kind": "action", "type": "count", "argument": "done" }
              }
            ],
            "transitions": [
              { "from": "engage", "to": "done", "condition": "flag:finish" }
            ]
          }
        ]
        """;

    [Fact]
    public void Json_profile_maps_action_state_lifecycle_and_result_transition_settings()
    {
        const string json = """
            [
              {
                "id": "result-driven",
                "startState": "work",
                "states": [
                  {
                    "id": "work",
                    "kind": "actionState",
                    "needsExitTime": true,
                    "completionPolicy": "hold",
                    "behaviorRoot": { "kind": "action", "type": "count", "argument": "work" }
                  },
                  {
                    "id": "done",
                    "kind": "actionState",
                    "behaviorRoot": { "kind": "action", "type": "noop" }
                  }
                ],
                "transitions": [
                  {
                    "from": "work",
                    "to": "done",
                    "mode": "onSucceeded",
                    "priority": 42,
                    "forceInstantly": true
                  }
                ]
              }
            ]
            """;

        var catalog = new MobaActorStateMachineProfileCatalog();
        Assert.Equal(1, MobaActorStateMachineProfileJsonLoader.LoadJson(json, catalog));
        Assert.True(catalog.TryGet("result-driven", out var profile));

        Assert.Equal(2, profile.States.Count);
        var state = profile.States[0];
        Assert.True(state.NeedsExitTime);
        Assert.Equal(ActionStateCompletionPolicy.Hold, state.CompletionPolicy);

        var transition = Assert.Single(profile.Transitions);
        Assert.Equal(HfsmRuntimeTransitionMode.OnSucceeded, transition.Mode);
        Assert.Equal(42, transition.Priority);
        Assert.True(transition.ForceInstantly);
    }

    [Fact]
    public void Json_behavior_root_builds_and_executes_all_composite_node_types()
    {
        const string json = """
            [
              {
                "id": "composite",
                "startState": "work",
                "states": [
                  {
                    "id": "work",
                    "kind": "actionState",
                    "behaviorRoot": {
                      "kind": "sequence",
                      "children": [
                        { "kind": "condition", "condition": "always" },
                        { "kind": "action", "type": "trace", "argument": "begin" },
                        {
                          "kind": "selector",
                          "children": [
                            { "kind": "condition", "condition": "never" },
                            { "kind": "action", "type": "trace", "argument": "selected" }
                          ]
                        },
                        {
                          "kind": "parallel",
                          "successPolicy": "all",
                          "failurePolicy": "any",
                          "children": [
                            { "kind": "action", "type": "trace", "argument": "parallel" },
                            { "kind": "delay", "durationSeconds": 0.2 }
                          ]
                        },
                        {
                          "kind": "repeat",
                          "repeatCount": 2,
                          "child": { "kind": "action", "type": "trace", "argument": "repeat" }
                        },
                        {
                          "kind": "invert",
                          "child": { "kind": "condition", "condition": "never" }
                        },
                        {
                          "kind": "timeout",
                          "durationSeconds": 0.5,
                          "useUnscaledTime": true,
                          "child": { "kind": "delay", "durationSeconds": 0.1 }
                        }
                      ]
                    }
                  },
                  {
                    "id": "done",
                    "kind": "actionState",
                    "behaviorRoot": { "kind": "action", "type": "trace", "argument": "done" }
                  }
                ],
                "transitions": [
                  { "from": "work", "to": "done", "mode": "onSucceeded" }
                ]
              }
            ]
            """;

        var catalog = new MobaActorStateMachineProfileCatalog();
        Assert.Equal(1, MobaActorStateMachineProfileJsonLoader.LoadJson(json, catalog));
        Assert.True(catalog.TryGet("composite", out var profile));
        var root = Assert.IsType<HfsmRuntimeBehaviourSpec<MobaHfsmActionSpec>>(
            profile.States[0].BehaviourRoot);
        Assert.Equal(HfsmRuntimeBehaviourKind.Sequence, root.Kind);
        Assert.Equal(7, root.Children.Count);
        Assert.Equal(ParallelSuccessPolicy.All, root.Children[3].ParallelSuccessPolicy);
        Assert.Equal(ParallelFailurePolicy.Any, root.Children[3].ParallelFailurePolicy);
        Assert.Equal(0.2f, root.Children[3].Children[1].DurationSeconds);
        Assert.Equal(2, root.Children[4].RepeatCount);
        Assert.Equal(0.5f, root.Children[6].DurationSeconds);
        Assert.True(root.Children[6].UseUnscaledTime);
        Assert.Equal(ActionStateCompletionPolicy.Hold, profile.States[0].CompletionPolicy);

        var trace = new List<string>();
        var registry = new MobaActorStateMachineRuntimeRegistry();
        registry.RegisterAction("trace", (_, argument) => new CallbackBehaviour(() => trace.Add(argument)));
        var actor = new ActorContext().CreateEntity();
        var factory = new MobaActorStateMachineFactory(null, catalog, registry);
        Assert.True(factory.TryCreate(actor, "composite", out var runtime));

        runtime.Tick(0.1f);
        runtime.Tick(0.1f);
        runtime.Tick(0.1f);
        runtime.Tick(0.1f);

        Assert.Equal("done", runtime.StateMachine.ActiveStateName);
        Assert.Equal(
            new[] { "begin", "selected", "parallel", "repeat", "repeat", "done" },
            trace);
    }

    [Fact]
    public void Json_profile_rejects_removed_flat_format_and_malformed_decorator()
    {
        const string ambiguousJson = """
            [
              {
                "id": "invalid",
                "startState": "work",
                "states": [
                  {
                    "id": "work",
                    "kind": "actionState",
                    "actions": [ { "type": "noop" } ]
                  }
                ]
              }
            ]
            """;
        const string malformedDecoratorJson = """
            [
              {
                "id": "invalid",
                "startState": "work",
                "states": [
                  {
                    "id": "work",
                    "kind": "actionState",
                    "behaviorRoot": {
                      "kind": "repeat",
                      "children": [
                        { "kind": "action", "type": "noop" },
                        { "kind": "action", "type": "noop" }
                      ]
                    }
                  }
                ]
              }
            ]
            """;

        var legacyError = Assert.Throws<JsonSerializationException>(() =>
            MobaActorStateMachineProfileJsonLoader.LoadJson(
                ambiguousJson,
                new MobaActorStateMachineProfileCatalog()));
        Assert.Contains("actions", legacyError.Message);

        var decoratorError = Assert.Throws<InvalidOperationException>(() =>
            MobaActorStateMachineProfileJsonLoader.LoadJson(
                malformedDecoratorJson,
                new MobaActorStateMachineProfileCatalog()));
        Assert.Contains("must use 'child'", decoratorError.Message);
    }

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
                "startState": "nested",
                "states": [
                  {
                    "id": "nested",
                    "kind": "stateMachine",
                    "startState": "only",
                    "states": [
                      {
                        "id": "only",
                        "kind": "actionState",
                        "behaviorRoot": { "kind": "action", "type": "noop" }
                      }
                    ],
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
                    "kind": "actionState",
                    "behaviorRoot": { "kind": "action", "type": "delay", "argument": "1" }
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
    public void Profile_content_hash_is_deterministic_and_changes_with_runtime_specification()
    {
        const string modifiedProfileJson = """
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
                        "behaviorRoot": { "kind": "action", "type": "count", "argument": "changed" }
                      }
                    ]
                  },
                  {
                    "id": "done",
                    "kind": "actionState",
                    "behaviorRoot": { "kind": "action", "type": "count", "argument": "done" }
                  }
                ],
                "transitions": [
                  { "from": "engage", "to": "done", "condition": "flag:finish" }
                ]
              }
            ]
            """;

        var firstCatalog = new MobaActorStateMachineProfileCatalog();
        var secondCatalog = new MobaActorStateMachineProfileCatalog();
        var modifiedCatalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(HierarchicalProfileJson, firstCatalog);
        MobaActorStateMachineProfileJsonLoader.LoadJson(HierarchicalProfileJson, secondCatalog);
        MobaActorStateMachineProfileJsonLoader.LoadJson(modifiedProfileJson, modifiedCatalog);

        Assert.True(firstCatalog.TryGetContentHash("combat", out var firstHash));
        Assert.True(secondCatalog.TryGetContentHash("combat", out var secondHash));
        Assert.True(modifiedCatalog.TryGetContentHash("combat", out var modifiedHash));
        Assert.Equal(firstHash, secondHash);
        Assert.NotEqual(firstHash, modifiedHash);
    }

    [Fact]
    public void Rollback_provider_exports_v3_payload_with_runtime_profile_content_hash()
    {
        var catalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(HierarchicalProfileJson, catalog);
        var runtimeRegistry = new MobaActorStateMachineRuntimeRegistry();
        runtimeRegistry.RegisterAction("count", (_, __) => new CallbackBehaviour(null));
        runtimeRegistry.RegisterCondition("flag", (_, __) => false);
        var actor = new ActorContext().CreateEntity();
        var actors = new MobaActorRegistry();
        actors.Register(12, actor);
        var factory = new MobaActorStateMachineFactory(null, catalog, runtimeRegistry);
        Assert.True(factory.TryCreate(actor, "combat", out var runtime));
        actor.AddActorStateMachine("combat", runtime);
        var provider = new MobaActorStateMachineRollbackProvider(actors, factory);

        var payload = MemoryPackSerializer.Deserialize<MobaActorStateMachineRollbackPayload>(
            provider.Export(new FrameIndex(5)));

        Assert.Equal(4, payload.Version);
        var entry = Assert.Single(payload.Entries);
        Assert.Equal(runtime.ProfileContentHash, entry.ProfileContentHash);
        Assert.True(catalog.TryGetContentHash("combat", out var catalogHash));
        Assert.Equal(catalogHash, entry.ProfileContentHash);
    }

    [Fact]
    public void Rollback_provider_rejects_v3_payload_when_profile_content_changed_under_same_id()
    {
        const string changedProfileJson = """
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
                        "behaviorRoot": { "kind": "action", "type": "count", "argument": "different" }
                      }
                    ]
                  }
                ]
              }
            ]
            """;

        var originalCatalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(HierarchicalProfileJson, originalCatalog);
        var runtimeRegistry = new MobaActorStateMachineRuntimeRegistry();
        runtimeRegistry.RegisterAction("count", (_, __) => new CallbackBehaviour(null));
        runtimeRegistry.RegisterCondition("flag", (_, __) => false);
        var actor = new ActorContext().CreateEntity();
        var actors = new MobaActorRegistry();
        actors.Register(13, actor);
        var originalFactory = new MobaActorStateMachineFactory(null, originalCatalog, runtimeRegistry);
        Assert.True(originalFactory.TryCreate(actor, "combat", out var runtime));
        actor.AddActorStateMachine("combat", runtime);
        var originalProvider = new MobaActorStateMachineRollbackProvider(actors, originalFactory);
        var payload = originalProvider.Export(new FrameIndex(6));

        var changedCatalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(changedProfileJson, changedCatalog);
        var changedProvider = new MobaActorStateMachineRollbackProvider(
            actors,
            new MobaActorStateMachineFactory(null, changedCatalog, runtimeRegistry));

        var error = Assert.Throws<InvalidOperationException>(() =>
            changedProvider.Import(new FrameIndex(6), payload));
        Assert.Contains("content hash", error.Message);
    }

    [Fact]
    public void Rollback_provider_imports_legacy_v2_payload_without_profile_content_hash()
    {
        var catalog = new MobaActorStateMachineProfileCatalog();
        MobaActorStateMachineProfileJsonLoader.LoadJson(HierarchicalProfileJson, catalog);
        var runtimeRegistry = new MobaActorStateMachineRuntimeRegistry();
        runtimeRegistry.RegisterAction("count", (_, __) => new CallbackBehaviour(null));
        runtimeRegistry.RegisterCondition("flag", (_, __) => false);
        var actor = new ActorContext().CreateEntity();
        var actors = new MobaActorRegistry();
        actors.Register(14, actor);
        var factory = new MobaActorStateMachineFactory(null, catalog, runtimeRegistry);
        Assert.True(factory.TryCreate(actor, "combat", out var runtime));
        actor.AddActorStateMachine("combat", runtime);
        var provider = new MobaActorStateMachineRollbackProvider(actors, factory);
        var v3Payload = MemoryPackSerializer.Deserialize<MobaActorStateMachineRollbackPayload>(
            provider.Export(new FrameIndex(7)));
        var entry = Assert.Single(v3Payload.Entries);
        var legacyPayload = MemoryPackSerializer.Serialize(new LegacyStateMachineRollbackPayload(
            2,
            new[]
            {
                new LegacyStateMachineRollbackEntry(
                    entry.ActorId,
                    entry.HasRuntime,
                    entry.ProfileId,
                    entry.DeltaTime,
                    entry.State,
                    entry.Root)
            }));

        actor.RemoveActorStateMachine();
        provider.Import(new FrameIndex(7), legacyPayload);

        Assert.True(actor.hasActorStateMachine);
        Assert.Equal("combat", actor.actorStateMachine.ProfileId);
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

[MemoryPackable]
public readonly partial struct LegacyStateMachineRollbackPayload
{
    [MemoryPackOrder(0)] public readonly int Version;
    [MemoryPackOrder(1)] public readonly LegacyStateMachineRollbackEntry[] Entries;

    [MemoryPackConstructor]
    public LegacyStateMachineRollbackPayload(int version, LegacyStateMachineRollbackEntry[] entries)
    {
        Version = version;
        Entries = entries;
    }
}

[MemoryPackable]
public readonly partial struct LegacyStateMachineRollbackEntry
{
    [MemoryPackOrder(0)] public readonly int ActorId;
    [MemoryPackOrder(1)] public readonly bool HasRuntime;
    [MemoryPackOrder(2)] public readonly string ProfileId;
    [MemoryPackOrder(3)] public readonly float DeltaTime;
    [MemoryPackOrder(4)] public readonly MobaActorStateMachineRollbackState State;
    [MemoryPackOrder(5)] public readonly MobaHfsmSnapshotNode Root;

    public LegacyStateMachineRollbackEntry(
        int actorId,
        bool hasRuntime,
        string profileId,
        float deltaTime,
        MobaActorStateMachineRollbackState state,
        MobaHfsmSnapshotNode root)
    {
        ActorId = actorId;
        HasRuntime = hasRuntime;
        ProfileId = profileId ?? string.Empty;
        DeltaTime = deltaTime;
        State = state;
        Root = root;
    }
}
