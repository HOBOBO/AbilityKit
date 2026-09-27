using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaEntitasRollbackTests
{
    [Fact]
    public void Rollback_removes_predicted_actor_and_cast_and_rewinds_actor_ids()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var registry = new MobaActorRegistry();
        var actor = context.CreateEntity();
        actor.AddActorId(ids.Next());
        registry.Register(actor.actorId.Value, actor);
        var provider = new MobaEntitasEntityRollbackProvider(context, ids, registry, null);
        var frame = new FrameIndex(4);
        var payload = provider.Export(frame);

        var predicted = context.CreateEntity();
        predicted.AddActorId(ids.Next());
        registry.Register(predicted.actorId.Value, predicted);
        var cast = context.CreateEntity();
        cast.AddSkillCastInstanceId(101);

        provider.Import(frame, payload);

        Assert.True(actor.isEnabled);
        Assert.False(predicted.isEnabled);
        Assert.False(cast.isEnabled);
        Assert.False(registry.Contains(2));
        Assert.Equal(2, ids.Next());
    }

    [Fact]
    public void Component_snapshot_restores_presence_and_deep_copies_skill_runtime()
    {
        var context = new ActorContext();
        var actor = context.CreateEntity();
        actor.AddActorId(1);
        actor.AddLifetime(100);
        var original = new ActiveSkillRuntime { SkillId = 7, Level = 2, CurrentCharges = 3 };
        actor.AddSkillLoadout(new[] { original }, new[] { new PassiveSkillRuntime { PassiveSkillId = 9, Level = 1 } });
        var provider = new MobaEntitasComponentRollbackProvider(context);
        var frame = new FrameIndex(4);
        var payload = provider.Export(frame);

        original.CurrentCharges = 0;
        actor.skillLoadout.PassiveSkills[0].Level = 8;
        actor.ReplaceLifetime(999);
        actor.AddActorDespawnRequest(5, 5, ActorDespawnReason.RollbackCleanup, 0, 0);
        provider.Import(frame, payload);

        Assert.Equal(100, actor.lifetime.EndTimeMs);
        Assert.False(actor.hasActorDespawnRequest);
        Assert.Equal(3, actor.skillLoadout.ActiveSkills[0].CurrentCharges);
        Assert.Equal(1, actor.skillLoadout.PassiveSkills[0].Level);
        Assert.NotSame(original, actor.skillLoadout.ActiveSkills[0]);
    }

    [Fact]
    public void Missing_confirmed_actor_fails_preflight_without_mutating_allocator()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var actor = context.CreateEntity();
        actor.AddActorId(ids.Next());
        var provider = new MobaEntitasEntityRollbackProvider(context, ids, new MobaActorRegistry(), null);
        var frame = new FrameIndex(2);
        var payload = provider.Export(frame);
        actor.Destroy();
        ids.Next();

        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(frame, payload));
        Assert.Equal(3, ids.NextId);
    }

    [Fact]
    public void Actor_command_journal_removes_actor_created_after_checkpoint()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var actors = new MobaActorRegistry();
        var entities = new MobaEntityManager(null);
        var frameTime = CreateFrameTime(10);
        var commands = entities.GetOrCreateActorRollbackCommands(context, actors, frameTime);
        var coordinator = CreateActorCoordinator(context, ids, actors, entities, commands);
        var snapshot = coordinator.Capture(frameTime.Frame);

        frameTime.StepTo(new FrameIndex(11), 1f / 60f);
        var actorId = ids.Next();
        var spec = CreateSpec(actorId);
        var predicted = ActorSpawnPipeline.BuildActorAndRegister(
            context,
            actors,
            entities,
            in spec).Entity;

        Assert.Equal(1, commands.Log.Count);
        Assert.True(actors.Contains(actorId));

        coordinator.Restore(snapshot);

        Assert.False(predicted.isEnabled);
        Assert.False(actors.Contains(actorId));
        Assert.False(entities.TryGetActorEntity(actorId, out _));
        Assert.Equal(0, commands.Log.Count);
        Assert.Equal(actorId, ids.Next());
    }

    [Fact]
    public void Actor_command_journal_restores_destroyed_actor_before_field_providers()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var actors = new MobaActorRegistry();
        var entities = new MobaEntityManager(null);
        var frameTime = CreateFrameTime(20);
        var commands = entities.GetOrCreateActorRollbackCommands(context, actors, frameTime);
        var coordinator = CreateActorCoordinator(context, ids, actors, entities, commands);
        var actorId = ids.Next();
        var spec = CreateSpec(actorId);
        var original = ActorSpawnPipeline.BuildActorAndRegister(
            context,
            actors,
            entities,
            in spec).Entity;
        original.AddLifetime(1234);
        original.AddModelId(9001);
        original.AddOwnerLink(701, 700);
        var snapshot = coordinator.Capture(frameTime.Frame);
        var retainedCommands = commands.Log.Count;

        frameTime.StepTo(new FrameIndex(21), 1f / 60f);
        var removed = new MobaActorSpawnRegistrar(actors, entities).Unregister(
            actorId,
            out var destroyed,
            publishDespawn: false);
        destroyed.Destroy();

        Assert.True(removed);
        Assert.Equal(retainedCommands + 1, commands.Log.Count);
        coordinator.Restore(snapshot);

        Assert.True(actors.TryGet(actorId, out var restored));
        Assert.True(restored.isEnabled);
        Assert.True(entities.TryGetActorEntity(actorId, out var indexed));
        Assert.Same(restored, indexed);
        Assert.Equal(1234, restored.lifetime.EndTimeMs);
        Assert.Equal(9001, restored.modelId.Value);
        Assert.Equal(701, restored.ownerLink.OwnerActorId);
        Assert.Equal(700, restored.ownerLink.RootOwnerActorId);
        Assert.Equal(spec.Info.Transform, restored.transform.Value);
        Assert.Equal(retainedCommands, commands.Log.Count);
    }

    [Fact]
    public void Actor_created_and_destroyed_after_checkpoint_rolls_back_in_reverse_order()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var actors = new MobaActorRegistry();
        var entities = new MobaEntityManager(null);
        var frameTime = CreateFrameTime(25);
        var commands = entities.GetOrCreateActorRollbackCommands(context, actors, frameTime);
        var coordinator = CreateActorCoordinator(context, ids, actors, entities, commands);
        var snapshot = coordinator.Capture(frameTime.Frame);
        var actorId = ids.Next();
        var spec = CreateSpec(actorId);
        var actor = ActorSpawnPipeline.BuildActorAndRegister(
            context,
            actors,
            entities,
            in spec).Entity;
        new MobaActorSpawnRegistrar(actors, entities).Unregister(
            actorId,
            out _,
            publishDespawn: false);
        actor.Destroy();

        Assert.Equal(2, commands.Log.Count);
        coordinator.Restore(snapshot);

        Assert.False(actors.Contains(actorId));
        Assert.False(entities.TryGetActorEntity(actorId, out _));
        Assert.Equal(0, commands.Log.Count);
    }

    [Fact]
    public void Missing_actor_preflight_is_relaxed_only_when_command_restore_is_enabled()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var actor = context.CreateEntity();
        actor.AddActorId(ids.Next());
        var frame = new FrameIndex(30);
        var strict = new MobaEntitasEntityRollbackProvider(
            context,
            ids,
            new MobaActorRegistry(),
            null);
        var payload = strict.Export(frame);
        actor.Destroy();
        var commandAware = new MobaEntitasEntityRollbackProvider(
            context,
            ids,
            new MobaActorRegistry(),
            null,
            allowMissingActorsRestoredByCommands: true);

        Assert.Throws<InvalidOperationException>(() => strict.ValidateImport(frame, payload));
        commandAware.ValidateImport(frame, payload);
    }

    [Fact]
    public void Missing_cast_remains_a_preflight_error_when_actor_commands_are_enabled()
    {
        var context = new ActorContext();
        var ids = new ActorIdAllocator();
        var cast = context.CreateEntity();
        cast.AddSkillCastInstanceId(4001);
        var provider = new MobaEntitasEntityRollbackProvider(
            context,
            ids,
            new MobaActorRegistry(),
            null,
            allowMissingActorsRestoredByCommands: true);
        var frame = new FrameIndex(40);
        var payload = provider.Export(frame);
        cast.Destroy();

        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(frame, payload));
    }

    [Fact]
    public void Entity_manager_reuses_one_actor_command_journal_per_world_binding()
    {
        var context = new ActorContext();
        var actors = new MobaActorRegistry();
        var entities = new MobaEntityManager(null);
        var frameTime = CreateFrameTime(1);

        var first = entities.GetOrCreateActorRollbackCommands(context, actors, frameTime);
        var second = entities.GetOrCreateActorRollbackCommands(context, actors, frameTime);

        Assert.Same(first, second);
        Assert.Same(first.StateProvider, second.StateProvider);
    }

    [Fact]
    public void Registry_builder_registers_and_reuses_actor_command_provider()
    {
        var contexts = new Contexts();
        var services = new TestWorldResolver();
        services.Add(new ActorIdAllocator());
        services.Add(new MobaActorRegistry());
        services.Add(new MobaEntityManager(null));
        services.Add<IFrameTime>(CreateFrameTime(1));
        using var world = new TestEntitasWorld(contexts, services);

        var first = MobaRollbackRegistryBuilder.Create(world);
        var second = MobaRollbackRegistryBuilder.Create(world);

        Assert.True(first.TryGet(CommandRollbackStateProvider.DefaultKey, out var firstProvider));
        Assert.True(second.TryGet(CommandRollbackStateProvider.DefaultKey, out var secondProvider));
        Assert.Same(firstProvider, secondProvider);
    }

    [Fact]
    public void Predicted_summon_owner_tracking_is_pruned_without_touching_confirmed_summons()
    {
        var summons = new MobaSummonService();
        var field = typeof(MobaSummonService).GetField("_summonsByRootOwner", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var tracked = Assert.IsType<Dictionary<int, List<int>>>(field.GetValue(summons));
        tracked[1] = new List<int> { 2, 3 };

        summons.PruneRollbackActors(new[] { 1, 2 });

        Assert.Equal(new[] { 2 }, tracked[1]);
    }

    [Fact]
    public void Context_registry_rollback_removes_predicted_context_and_reuses_generated_id()
    {
        var contexts = new MobaRuntimeContextService();
        var confirmed = contexts.Registry.Create().Build();
        var provider = new MobaContextEntityRollbackProvider(contexts);
        var frame = new FrameIndex(3);
        var payload = provider.Export(frame);

        var predicted = contexts.Registry.Create().Build();
        contexts.Registry.GenerateId();
        provider.Import(frame, payload);

        Assert.True(contexts.Registry.Exists(confirmed));
        Assert.False(contexts.Registry.Exists(predicted));
        Assert.Equal(predicted, contexts.Registry.GenerateId());
    }

    private static FrameTime CreateFrameTime(int frame)
    {
        var result = new FrameTime();
        result.Reset(new FrameIndex(frame), frame / 60f, 1f / 60f);
        return result;
    }

    private static RollbackCoordinator CreateActorCoordinator(
        ActorContext context,
        ActorIdAllocator ids,
        MobaActorRegistry actors,
        MobaEntityManager entities,
        MobaActorRollbackCommandRuntime commands)
    {
        var registry = new RollbackRegistry();
        registry.Register(new MobaEntitasEntityRollbackProvider(
            context,
            ids,
            actors,
            entities,
            allowMissingActorsRestoredByCommands: true));
        registry.Register(new MobaActorTransformRollbackProvider(actors));
        registry.Register(new MobaEntitasComponentRollbackProvider(
            context,
            allowMissingActorsRestoredByCommands: true));
        registry.Register(commands.StateProvider);
        return new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));
    }

    private static MobaActorBuildSpec CreateSpec(int actorId)
    {
        var transform = new Transform3(
            new Vec3(2f, 0f, 3f),
            Quat.Identity,
            Vec3.One);
        var info = new MobaEntityInfo(
            actorId,
            MobaEntityKind.Hero,
            in transform,
            (Team)1,
            EntityMainType.Unit,
            UnitSubType.Hero,
            new PlayerId("rollback-test"),
            templateId: 1001);
        return new MobaActorBuildSpec(
            in info,
            MobaActorBuildSourceKind.PlayerLoadout,
            sourceId: 1001,
            ownerActorId: 0);
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Add<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
            _services[service.GetType()] = service;
        }

        public object Resolve(Type serviceType) => _services[serviceType];

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public bool TryResolve(Type serviceType, out object instance) =>
            _services.TryGetValue(serviceType, out instance);

        public bool TryResolve<T>(out T instance)
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                instance = (T)service;
                return true;
            }

            instance = default;
            return false;
        }
    }

    private sealed class TestEntitasWorld : IEntitasWorld
    {
        public TestEntitasWorld(Contexts contexts, IWorldResolver services)
        {
            Contexts = contexts;
            Services = services;
            Systems = new Entitas.Systems();
        }

        public WorldId Id => new("moba-actor-rollback-test");
        public string WorldType => "moba";
        public IWorldResolver Services { get; }
        public Entitas.IContexts Contexts { get; }
        public Entitas.Systems Systems { get; }
        public void Initialize() { }
        public void Tick(float deltaTime) { }
        public void Dispose() { }
    }
}
