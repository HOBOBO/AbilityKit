using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Battle.Projection;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleWorldProjectionViewSourceTests
    {
        [Test]
        public void ReplacingWorldOrSeeking_RebindsTheProjectionEpoch()
        {
            var producerA = new Producer();
            var producerB = new Producer();
            IWorld world = null;
            var frame = 0;
            var source = new BattleWorldProjectionViewSource(
                new WorldId("battle"), () => world, () => frame);

            Assert.That(source.TryGetProjection(out _, out _), Is.False);
            world = new World("battle", producerA);
            frame = 12;
            Assert.That(source.TryGetProjection(out var current, out var currentFrame), Is.True);
            Assert.That(current, Is.SameAs(producerA));
            Assert.That(currentFrame, Is.EqualTo(12));
            var epoch = source.ProjectionEpoch;
            Assert.That(source.TryGetProjection(out _, out _), Is.True);
            Assert.That(source.ProjectionEpoch, Is.EqualTo(epoch));

            world = new World("battle", producerB);
            Assert.That(source.TryGetProjection(out current, out _), Is.True);
            Assert.That(current, Is.SameAs(producerB));
            Assert.That(source.ProjectionEpoch, Is.GreaterThan(epoch));
            epoch = source.ProjectionEpoch;
            source.Invalidate();
            Assert.That(source.ProjectionEpoch, Is.GreaterThan(epoch));
        }

        private sealed class Producer : IActorProjectionProducer
        {
            public ActorProjectionData ExtractFull(int actorId) => default;
            public ActorProjectionData ExtractSpawn(int actorId) => default;
            public void ExtractAll(List<ActorProjectionData> buffer) { }
        }

        private sealed class World : IWorld
        {
            public World(string id, IActorProjectionProducer producer)
            {
                Id = new WorldId(id);
                Services = new Resolver(producer);
            }

            public WorldId Id { get; }
            public string WorldType => "test";
            public IWorldResolver Services { get; }
            public void Initialize() { }
            public void Tick(float deltaTime) { }
            public void Dispose() { }
        }

        private sealed class Resolver : IWorldResolver
        {
            private readonly IActorProjectionProducer _producer;
            public Resolver(IActorProjectionProducer producer) => _producer = producer;
            public object Resolve(Type serviceType) =>
                TryResolve(serviceType, out var value) ? value : throw new InvalidOperationException();
            public T Resolve<T>() => TryResolve(out T value) ? value : throw new InvalidOperationException();
            public bool TryResolve(Type serviceType, out object instance)
            {
                instance = serviceType == typeof(IActorProjectionProducer) ? _producer : null;
                return instance != null;
            }
            public bool TryResolve<T>(out T instance)
            {
                instance = _producer is T value ? value : default;
                return instance != null;
            }
        }
    }
}
