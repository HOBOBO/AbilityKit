using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Triggering.Eventing;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaActorSpawnRegistrarTests
    {
        private const int ActorId = 701;

        private ActorContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = new ActorContext();
        }

        [TearDown]
        public void TearDown()
        {
            _context.DestroyAllEntities();
        }

        [Test]
        public void Register_CommitsActorRegistryAndEntityManagerTogether()
        {
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var entity = _context.CreateEntity();
            var spec = CreateSpec(ActorId);
            var registrar = new MobaActorSpawnRegistrar(registry, entities);

            var registered = registrar.Register(
                entity,
                in spec,
                registerActor: true,
                registerEntityManager: true,
                registerEntityManagerFromEntity: false);

            Assert.That(registered, Is.True);
            Assert.That(registry.TryGetRegistered(ActorId, out var registeredActor), Is.True);
            Assert.That(registeredActor, Is.SameAs(entity));
            Assert.That(entities.TryGetActorEntity(ActorId, out var indexedActor), Is.True);
            Assert.That(indexedActor, Is.SameAs(entity));
            Assert.That(entities.Index.Registry.Contains(ActorId), Is.True);
        }

        [Test]
        public void Register_EntityManagerValidationFailure_RollsBackActorRegistry()
        {
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var entity = _context.CreateEntity();
            var spec = CreateSpec(ActorId);
            var registrar = new MobaActorSpawnRegistrar(registry, entities);

            Assert.Throws<InvalidOperationException>(() => registrar.Register(
                entity,
                in spec,
                registerActor: true,
                registerEntityManager: true,
                registerEntityManagerFromEntity: true));

            Assert.That(registry.Contains(ActorId), Is.False);
            Assert.That(entities.TryGetActorEntity(ActorId, out _), Is.False);
            Assert.That(entities.Index.Registry.Contains(ActorId), Is.False);
        }

        [Test]
        public void Unregister_WithDespawnEnabled_PublishesOnceAfterBothRegistriesCommit()
        {
            var eventBus = new EventBus();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(eventBus);
            var entity = Register(registry, entities, ActorId);
            var registrar = new MobaActorSpawnRegistrar(registry, entities);
            var eventCount = 0;
            var key = CreateUnitEventKey(MobaUnitTriggering.Events.Despawn);
            using var subscription = eventBus.Subscribe<UnitEventPayload>(key, payload =>
            {
                eventCount++;
                Assert.That(payload.ActorId, Is.EqualTo(ActorId));
                Assert.That(registry.Contains(ActorId), Is.False);
                Assert.That(entities.TryGetActorEntity(ActorId, out _), Is.False);
            });

            var unregistered = registrar.Unregister(ActorId, out var removed, publishDespawn: true);

            Assert.That(unregistered, Is.True);
            Assert.That(removed, Is.SameAs(entity));
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void Unregister_ForCompensation_DoesNotPublishDespawn()
        {
            var eventBus = new EventBus();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(eventBus);
            Register(registry, entities, ActorId);
            var eventCount = 0;
            var key = CreateUnitEventKey(MobaUnitTriggering.Events.Despawn);
            using var subscription = eventBus.Subscribe<UnitEventPayload>(key, _ => eventCount++);

            var unregistered = new MobaActorSpawnRegistrar(registry, entities).Unregister(
                ActorId,
                out _,
                publishDespawn: false);

            Assert.That(unregistered, Is.True);
            Assert.That(eventCount, Is.Zero);
            Assert.That(registry.Contains(ActorId), Is.False);
            Assert.That(entities.TryGetActorEntity(ActorId, out _), Is.False);
        }

        [Test]
        public void Unregister_DisabledEntity_UsesRawRegistryEntryAndRemovesBothRegistries()
        {
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var entity = Register(registry, entities, ActorId);
            entity.Destroy();

            Assert.That(entity.isEnabled, Is.False);
            Assert.That(registry.TryGet(ActorId, out _), Is.False);
            Assert.That(registry.TryGetRegistered(ActorId, out var rawEntity), Is.True);
            Assert.That(rawEntity, Is.SameAs(entity));

            var unregistered = new MobaActorSpawnRegistrar(registry, entities).Unregister(
                ActorId,
                out var removed,
                publishDespawn: false);

            Assert.That(unregistered, Is.True);
            Assert.That(removed, Is.SameAs(entity));
            Assert.That(registry.Contains(ActorId), Is.False);
            Assert.That(entities.TryGetActorEntity(ActorId, out _), Is.False);
        }

        [Test]
        public void Unregister_DifferentEntityReferences_FailsBeforeMutation()
        {
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var registryEntity = _context.CreateEntity();
            var indexedEntity = _context.CreateEntity();
            registry.Register(ActorId, registryEntity);
            var spec = CreateSpec(ActorId);
            new MobaActorSpawnRegistrar(null, entities).Register(
                indexedEntity,
                in spec,
                registerActor: false,
                registerEntityManager: true,
                registerEntityManagerFromEntity: false);

            Assert.Throws<InvalidOperationException>(() =>
                new MobaActorSpawnRegistrar(registry, entities).Unregister(
                    ActorId,
                    out _,
                    publishDespawn: false));

            Assert.That(registry.TryGetRegistered(ActorId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(registryEntity));
            Assert.That(entities.TryGetActorEntity(ActorId, out var indexed), Is.True);
            Assert.That(indexed, Is.SameAs(indexedEntity));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void Unregister_SingleRegistryEntry_PerformsRepairWithoutDespawn(
            bool actorRegistered,
            bool entityRegistered)
        {
            var eventBus = new EventBus();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(eventBus);
            var entity = _context.CreateEntity();
            var spec = CreateSpec(ActorId);
            new MobaActorSpawnRegistrar(registry, entities).Register(
                entity,
                in spec,
                registerActor: actorRegistered,
                registerEntityManager: entityRegistered,
                registerEntityManagerFromEntity: false);
            var eventCount = 0;
            var key = CreateUnitEventKey(MobaUnitTriggering.Events.Despawn);
            using var subscription = eventBus.Subscribe<UnitEventPayload>(key, _ => eventCount++);

            var repaired = new MobaActorSpawnRegistrar(registry, entities).Unregister(
                ActorId,
                out var removed,
                publishDespawn: false);

            Assert.That(repaired, Is.True);
            Assert.That(removed, Is.SameAs(entity));
            Assert.That(eventCount, Is.Zero);
            Assert.That(registry.Contains(ActorId), Is.False);
            Assert.That(entities.TryGetActorEntity(ActorId, out _), Is.False);
        }

        [Test]
        public void Unregister_DespawnSubscriberFailure_PropagatesAfterMutationCommit()
        {
            var eventBus = new EventBus();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(eventBus);
            Register(registry, entities, ActorId);
            var key = CreateUnitEventKey(MobaUnitTriggering.Events.Despawn);
            using var subscription = eventBus.Subscribe<UnitEventPayload>(key, _ =>
                throw new InvalidOperationException("despawn subscriber failed"));

            var error = Assert.Throws<InvalidOperationException>(() =>
                new MobaActorSpawnRegistrar(registry, entities).Unregister(
                    ActorId,
                    out _,
                    publishDespawn: true));

            Assert.That(error.Message, Is.EqualTo("despawn subscriber failed"));
            Assert.That(registry.Contains(ActorId), Is.False);
            Assert.That(entities.TryGetActorEntity(ActorId, out _), Is.False);
        }

        private ActorEntity Register(MobaActorRegistry registry, MobaEntityManager entities, int actorId)
        {
            var entity = _context.CreateEntity();
            var spec = CreateSpec(actorId);
            entity.AddActorId(actorId);
            entity.AddTeam(spec.Info.Team);
            entity.AddEntityMainType(spec.Info.MainType);
            entity.AddUnitSubType(spec.Info.UnitSubType);
            entity.AddOwnerPlayerId(spec.Info.OwnerPlayer);
            new MobaActorSpawnRegistrar(registry, entities).Register(
                entity,
                in spec,
                registerActor: true,
                registerEntityManager: true,
                registerEntityManagerFromEntity: true);
            return entity;
        }

        private static EventKey<UnitEventPayload> CreateUnitEventKey(string eventId)
        {
            return new EventKey<UnitEventPayload>(TriggeringIdUtil.GetEventEid(eventId));
        }

        private static MobaActorBuildSpec CreateSpec(int actorId)
        {
            var transform = Transform3.Identity;
            var info = new MobaEntityInfo(
                actorId,
                MobaEntityKind.Hero,
                in transform,
                (Team)1,
                EntityMainType.Unit,
                UnitSubType.Hero,
                new PlayerId("registrar-test"),
                templateId: 1001);
            return new MobaActorBuildSpec(
                in info,
                MobaActorBuildSourceKind.PlayerLoadout,
                sourceId: 1001,
                ownerActorId: 0);
        }
    }
}
