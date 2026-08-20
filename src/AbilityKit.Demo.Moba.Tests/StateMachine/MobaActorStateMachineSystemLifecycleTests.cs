using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Components;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateMachine;

public sealed class MobaActorStateMachineSystemLifecycleTests
{
    [Fact]
    public void Hfsm_activation_and_deactivation_report_truthfully_and_remove_owned_runtime()
    {
        var brains = new MutableBrainCatalog();
        brains.Set(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "idle"));
        var profiles = CreateProfiles("idle");
        var runtimeRegistry = new MobaActorStateMachineRuntimeRegistry();
        var factory = new MobaActorStateMachineFactory(null, profiles, runtimeRegistry);
        var service = new MobaBrainService(
            new MobaActorRegistry(),
            brains,
            config: null,
            MobaBrainDecisionDriverRegistry.CreateDefault(),
            profiles);
        var actor = CreateActor(101);
        actor.AddMoveInput(1f, 0f);

        Assert.True(service.ActivateBrain(actor, 1, sourceKind: 7, sourceId: 9));
        Assert.True(actor.hasActorBrain);
        Assert.Equal(1, actor.actorBrain.BrainId);
        Assert.Equal(0, actor.actorBrain.BehaviorInstanceId);
        Assert.Equal(0f, actor.moveInput.Dx);
        Assert.Equal(0f, actor.moveInput.Dz);

        Assert.True(factory.TryCreate(actor, "idle", out var runtime));
        actor.AddActorStateMachine("idle", runtime, MobaActorStateMachineOwnerKind.Brain);

        Assert.True(service.DeactivateBrain(actor));
        Assert.False(actor.hasActorBrain);
        Assert.False(actor.hasActorStateMachine);
        Assert.True(runtime.IsDisposed);
    }

    [Fact]
    public void Hfsm_to_btree_switch_removes_stale_state_machine()
    {
        var brains = new MutableBrainCatalog();
        brains.Set(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "idle"));
        brains.Set(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "tree"));
        var fixture = CreateSystemFixture(brains, CreateProfiles("idle"));
        var actor = CreateActor(fixture.Contexts.actor, 102);
        actor.AddActorBrain(1, 102, 1, 10, 0L);

        fixture.System.Execute();
        var runtime = actor.actorStateMachine.Runtime;
        Assert.NotNull(runtime);

        actor.ReplaceActorBrain(2, 102, 1, 11, 0L);
        fixture.System.Execute();

        Assert.False(actor.hasActorStateMachine);
        Assert.True(runtime.IsDisposed);
        fixture.System.TearDown();
    }

    [Fact]
    public void Hfsm_profile_switch_replaces_runtime_and_binding_change_does_not_reuse_it()
    {
        var brains = new MutableBrainCatalog();
        brains.Set(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "idle-a"));
        brains.Set(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.Hfsm, "idle-b"));
        var fixture = CreateSystemFixture(brains, CreateProfiles("idle-a", "idle-b"));
        var actor = CreateActor(fixture.Contexts.actor, 103);
        actor.AddActorBrain(1, 103, 2, 20, 0L);

        fixture.System.Execute();
        var first = actor.actorStateMachine.Runtime;

        actor.ReplaceActorBrain(2, 103, 2, 21, 0L);
        fixture.System.Execute();
        var second = actor.actorStateMachine.Runtime;

        Assert.True(first.IsDisposed);
        Assert.NotSame(first, second);
        Assert.Equal("idle-b", actor.actorStateMachine.ProfileId);

        actor.ReplaceActorBrain(2, 103, 2, 22, 0L);
        fixture.System.Execute();

        Assert.True(second.IsDisposed);
        Assert.NotSame(second, actor.actorStateMachine.Runtime);
        fixture.System.TearDown();
    }

    [Fact]
    public void Failed_creation_is_suppressed_until_configuration_identity_changes()
    {
        var brains = new MutableBrainCatalog();
        brains.Set(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "missing"));
        var fixture = CreateSystemFixture(brains, CreateProfiles("available"));
        var actor = CreateActor(fixture.Contexts.actor, 104);
        actor.AddActorBrain(1, 104, 3, 30, 0L);

        fixture.System.Execute();
        Assert.False(actor.hasActorStateMachine);

        fixture.System.Execute();
        Assert.False(actor.hasActorStateMachine);

        brains.Set(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "available"));
        fixture.System.Execute();

        Assert.NotNull(actor.actorStateMachine.Runtime);
        Assert.Equal("available", actor.actorStateMachine.ProfileId);
        fixture.System.TearDown();
    }

    [Fact]
    public void Failed_btree_switch_preserves_previous_hfsm_binding()
    {
        var brains = new MutableBrainCatalog();
        brains.Set(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.Hfsm, "idle"));
        brains.Set(new MobaActorBrainDefinition(2, MobaBrainDriverKeys.BehaviorTree, "asset-that-does-not-exist"));
        var profiles = CreateProfiles("idle");
        var factory = new MobaActorStateMachineFactory(
            null,
            profiles,
            new MobaActorStateMachineRuntimeRegistry());
        var service = new MobaBrainService(
            new MobaActorRegistry(),
            brains,
            config: null,
            MobaBrainDecisionDriverRegistry.CreateDefault(),
            profiles);
        var actor = CreateActor(105);
        Assert.True(service.ActivateBrain(actor, 1, sourceKind: 4, sourceId: 40));
        Assert.True(factory.TryCreate(actor, "idle", out var runtime));
        actor.AddActorStateMachine("idle", runtime, MobaActorStateMachineOwnerKind.Brain);

        Assert.False(service.ActivateBrain(actor, 2, sourceKind: 4, sourceId: 41));

        Assert.Equal(1, actor.actorBrain.BrainId);
        Assert.Same(runtime, actor.actorStateMachine.Runtime);
        Assert.False(runtime.IsDisposed);
    }

    [Fact]
    public void Brain_system_does_not_reconcile_projectile_owned_state_machine()
    {
        var brains = new MutableBrainCatalog();
        brains.Set(new MobaActorBrainDefinition(1, MobaBrainDriverKeys.BehaviorTree, "tree"));
        var profiles = CreateProfiles("projectile");
        var fixture = CreateSystemFixture(brains, profiles);
        var actor = CreateActor(fixture.Contexts.actor, 106);
        actor.AddActorBrain(1, 106, 5, 50, 0L);
        var factory = new MobaActorStateMachineFactory(
            null,
            profiles,
            new MobaActorStateMachineRuntimeRegistry());
        Assert.True(factory.TryCreate(actor, "projectile", out var runtime));
        actor.AddActorStateMachine("projectile", runtime, MobaActorStateMachineOwnerKind.Projectile);

        fixture.System.Execute();

        Assert.True(actor.hasActorStateMachine);
        Assert.Same(runtime, actor.actorStateMachine.Runtime);
        Assert.False(runtime.IsDisposed);
        fixture.System.TearDown();
    }

    private static (MobaActorStateMachineSystem System, Contexts Contexts, TestWorldClock Clock) CreateSystemFixture(
        IMobaActorBrainCatalog brains,
        IMobaActorStateMachineProfileCatalog profiles)
    {
        var clock = new TestWorldClock();
        clock.Tick(0.1f);
        var resolver = new TestWorldResolver();
        resolver.Add<IWorldClock>(clock);
        resolver.Add<IMobaActorBrainCatalog>(brains);
        resolver.Add(new MobaActorStateMachineFactory(
            resolver,
            profiles,
            new MobaActorStateMachineRuntimeRegistry()));
        var contexts = new Contexts();
        var system = new MobaActorStateMachineSystem(contexts, resolver);
        system.Initialize();
        return (system, contexts, clock);
    }

    private static ActorEntity CreateActor(int actorId)
    {
        return CreateActor(new ActorContext(), actorId);
    }

    private static ActorEntity CreateActor(ActorContext context, int actorId)
    {
        var actor = context.CreateEntity();
        actor.AddActorId(actorId);
        return actor;
    }

    private static MobaActorStateMachineProfileCatalog CreateProfiles(params string[] profileIds)
    {
        var profiles = new MobaActorStateMachineProfileCatalog();
        foreach (var profileId in profileIds)
        {
            var json = $$"""
                [
                  {
                    "id": "{{profileId}}",
                    "startState": "idle",
                    "states": [
                      {
                        "id": "idle",
                        "kind": "actionState",
                        "behaviorRoot": { "kind": "action", "type": "noop" }
                      }
                    ]
                  }
                ]
                """;
            MobaActorStateMachineProfileJsonLoader.LoadJson(json, profiles);
        }

        return profiles;
    }

    private sealed class MutableBrainCatalog : IMobaActorBrainCatalog
    {
        private readonly Dictionary<int, MobaActorBrainDefinition> _definitions = new();

        public IReadOnlyList<MobaActorBrainDefinition> Definitions
        {
            get
            {
                var definitions = new List<MobaActorBrainDefinition>(_definitions.Values);
                definitions.Sort((left, right) => left.BrainId.CompareTo(right.BrainId));
                return definitions;
            }
        }

        public void Set(in MobaActorBrainDefinition definition)
        {
            _definitions[definition.BrainId] = definition;
        }

        public bool TryGet(int brainId, out MobaActorBrainDefinition definition)
        {
            return _definitions.TryGetValue(brainId, out definition);
        }

        public void Dispose()
        {
            _definitions.Clear();
        }
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Add<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public object Resolve(Type serviceType) => _services[serviceType];

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public bool TryResolve(Type serviceType, out object instance)
        {
            return _services.TryGetValue(serviceType, out instance);
        }

        public bool TryResolve<T>(out T instance)
        {
            if (_services.TryGetValue(typeof(T), out var value))
            {
                instance = (T)value;
                return true;
            }

            instance = default;
            return false;
        }
    }

    private sealed class TestWorldClock : IWorldClock
    {
        public float DeltaTime { get; private set; }
        public float Time => TimeSeconds;
        public float TimeSeconds { get; private set; }

        public void Tick(float deltaTime)
        {
            DeltaTime = deltaTime;
            TimeSeconds += deltaTime;
        }
    }
}
