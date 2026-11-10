using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaProjectileBounceTargetProviderTests
    {
        private Contexts _contexts;
        private MobaActorRegistry _registry;
        private MobaProjectileBounceTargetProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _contexts = new Contexts();
            _registry = new MobaActorRegistry();
            _provider = new MobaProjectileBounceTargetProvider(_registry);
        }

        [TearDown]
        public void TearDown()
        {
            _provider.Dispose();
            _registry.Dispose();
            _contexts.Reset();
        }

        [Test]
        public void TrySelectNextTarget_SelectsNearestUnhitEnemyWithinHorizontalRadius()
        {
            RegisterActor(1, Team.Team1, new Vec3(0f, 0f, 0f)); // owner
            RegisterActor(2, Team.Team2, new Vec3(10f, 0f, 0f)); // current hit
            RegisterActor(3, Team.Team2, new Vec3(8f, 50f, 0f)); // nearest to the projectile, not the hit actor
            RegisterActor(7, Team.Team2, new Vec3(11f, 0f, 0f)); // nearer to the hit actor, farther from the projectile
            RegisterActor(4, Team.Team2, new Vec3(14f, 0f, 0f)); // already hit
            RegisterActor(5, Team.Team1, new Vec3(11f, 0f, 0f)); // ally
            RegisterActor(6, Team.Team2, new Vec3(20.01f, 0f, 0f)); // outside radius from projectile position

            var hitIds = new List<int> { 4 };
            var found = _provider.TrySelectNextTarget(1, 2, new Vec3(0f, 0f, 0f), hitIds, 20f, out var targetId, out var position);

            Assert.That(found, Is.True);
            Assert.That(targetId, Is.EqualTo(3));
            Assert.That(position, Is.EqualTo(new Vec3(8f, 50f, 0f)));
        }

        [Test]
        public void TrySelectNextTarget_UsesActorIdToBreakEqualDistanceTies()
        {
            RegisterActor(1, Team.Team1, Vec3.Zero);
            RegisterActor(2, Team.Team2, new Vec3(10f, 0f, 0f));
            RegisterActor(9, Team.Team2, new Vec3(11f, 0f, 0f));
            RegisterActor(7, Team.Team2, new Vec3(9f, 0f, 0f));

            var found = _provider.TrySelectNextTarget(1, 2, new Vec3(10f, 0f, 0f), null, 1f, out var targetId, out _);

            Assert.That(found, Is.True);
            Assert.That(targetId, Is.EqualTo(7));
        }

        [Test]
        public void TrySelectNextTarget_ReturnsFalseWhenNoEligibleEnemyRemains()
        {
            RegisterActor(1, Team.Team1, Vec3.Zero);
            RegisterActor(2, Team.Team2, new Vec3(10f, 0f, 0f));
            RegisterActor(3, Team.Team2, new Vec3(11f, 0f, 0f));
            RegisterActor(4, Team.Team1, new Vec3(10.5f, 0f, 0f));

            var found = _provider.TrySelectNextTarget(1, 2, new Vec3(10f, 0f, 0f), new[] { 3 }, 5f, out var targetId, out var position);

            Assert.That(found, Is.False);
            Assert.That(targetId, Is.Zero);
            Assert.That(position, Is.EqualTo(Vec3.Zero));
        }

        private void RegisterActor(int actorId, Team team, Vec3 position)
        {
            var entity = _contexts.actor.CreateEntity();
            entity.AddActorId(actorId);
            entity.AddTeam(team);
            entity.AddTransform(new Transform3(position, Quat.Identity, Vec3.One));
            _registry.Register(actorId, entity);
        }
    }
}
