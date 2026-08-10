using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaActorLookupServiceTests
    {
        private const int ActorId = 811;

        private Contexts _contexts;
        private ActorIdIndex _index;
        private MobaActorRegistry _registry;
        private MobaEntityManager _entities;
        private MobaActorLookupService _lookup;

        [SetUp]
        public void SetUp()
        {
            _contexts = new Contexts();
            _index = new ActorIdIndex(_contexts);
            _registry = new MobaActorRegistry();
            _entities = new MobaEntityManager(null);
            _lookup = new MobaActorLookupService(
                _index,
                _registry,
                _entities,
                _contexts);
        }

        [TearDown]
        public void TearDown()
        {
            _index.Dispose();
            _contexts.actor.DestroyAllEntities();
            _registry.Dispose();
            _entities.Dispose();
        }

        [Test]
        public void TryGetActorEntity_IndexFallback_DoesNotRepairRegistries()
        {
            var entity = CreateActor(ActorId);

            var found = _lookup.TryGetActorEntity(ActorId, out var resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(entity));
            Assert.That(_registry.Contains(ActorId), Is.False);
            Assert.That(_entities.TryGetActorEntity(ActorId, out _), Is.False);
            Assert.That(_entities.Index.Registry.Contains(ActorId), Is.False);
        }

        [Test]
        public void TryGetActorEntity_RegistryFallback_DoesNotRepairEntityManager()
        {
            var entity = _contexts.actor.CreateEntity();
            _registry.Register(ActorId, entity);

            var found = _lookup.TryGetActorEntity(ActorId, out var resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(entity));
            Assert.That(_registry.Contains(ActorId), Is.True);
            Assert.That(_entities.TryGetActorEntity(ActorId, out _), Is.False);
            Assert.That(_entities.Index.Registry.Contains(ActorId), Is.False);
        }

        [Test]
        public void TryGetActorEntity_GroupFallback_DoesNotRepairRegistries()
        {
            _index.Dispose();
            var entity = CreateActor(ActorId);

            var found = _lookup.TryGetActorEntity(ActorId, out var resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(entity));
            Assert.That(_registry.Contains(ActorId), Is.False);
            Assert.That(_entities.TryGetActorEntity(ActorId, out _), Is.False);
            Assert.That(_entities.Index.Registry.Contains(ActorId), Is.False);
        }

        private ActorEntity CreateActor(int actorId)
        {
            var entity = _contexts.actor.CreateEntity();
            entity.AddActorId(actorId);
            return entity;
        }
    }
}
