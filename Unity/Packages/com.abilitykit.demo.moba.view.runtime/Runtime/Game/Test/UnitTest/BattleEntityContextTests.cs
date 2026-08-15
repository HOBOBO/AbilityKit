using System.Collections.Generic;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using AbilityKit.World.ECS;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleEntityContextTests
    {
        [Test]
        public void Bind_TwoContextsDoNotShareMutableEntityState()
        {
            var first = CreateRuntime("first");
            var second = CreateRuntime("second");
            var firstContext = new BattleEntityContext();
            var secondContext = new BattleEntityContext();

            firstContext.Bind(
                first.Node,
                first.World,
                first.Lookup,
                first.Factory,
                first.Query,
                new List<IEntityId>());
            secondContext.Bind(
                second.Node,
                second.World,
                second.Lookup,
                second.Factory,
                second.Query,
                new List<IEntityId>());

            Assert.That(firstContext.EntityWorld, Is.Not.SameAs(secondContext.EntityWorld));
            Assert.That(firstContext.EntityLookup, Is.Not.SameAs(secondContext.EntityLookup));
            Assert.That(firstContext.EntityFactory, Is.Not.SameAs(secondContext.EntityFactory));
            Assert.That(firstContext.EntityQuery, Is.Not.SameAs(secondContext.EntityQuery));
            Assert.That(firstContext.DirtyEntities, Is.Not.SameAs(secondContext.DirtyEntities));
        }

        [Test]
        public void ClearBinding_StaleGenerationDoesNotClearReplacementBinding()
        {
            var owner = new BattleEntityContext();
            var original = CreateRuntime("original");
            var replacement = CreateRuntime("replacement");
            var originalGeneration = owner.Bind(
                original.Node,
                original.World,
                original.Lookup,
                original.Factory,
                original.Query);
            var replacementGeneration = owner.Bind(
                replacement.Node,
                replacement.World,
                replacement.Lookup,
                replacement.Factory,
                replacement.Query);

            var staleCleared = owner.ClearBinding(originalGeneration);

            Assert.That(staleCleared, Is.False);
            Assert.That(owner.EntityNode, Is.EqualTo(replacement.Node));
            Assert.That(owner.EntityWorld, Is.SameAs(replacement.World));
            Assert.That(owner.EntityLookup, Is.SameAs(replacement.Lookup));
            Assert.That(owner.EntityFactory, Is.SameAs(replacement.Factory));
            Assert.That(owner.EntityQuery, Is.SameAs(replacement.Query));
            Assert.That(owner.ClearBinding(replacementGeneration), Is.True);
            Assert.That(owner.EntityWorld, Is.Null);
        }

        [Test]
        public void Reset_ClearsEntityStateAndRetainsReusableDirtyCollection()
        {
            var runtime = CreateRuntime("reset");
            var dirty = new List<IEntityId> { runtime.Node.Id };
            var owner = new BattleEntityContext();
            owner.Bind(
                runtime.Node,
                runtime.World,
                runtime.Lookup,
                runtime.Factory,
                runtime.Query,
                dirty);

            owner.Reset(destroyCollections: false);

            Assert.That(owner.EntityNode.IsValid, Is.False);
            Assert.That(owner.EntityWorld, Is.Null);
            Assert.That(owner.EntityLookup, Is.Null);
            Assert.That(owner.EntityFactory, Is.Null);
            Assert.That(owner.EntityQuery, Is.Null);
            Assert.That(owner.DirtyEntities, Is.SameAs(dirty));
            Assert.That(owner.DirtyEntities, Is.Empty);
        }

        private static EntityRuntime CreateRuntime(string name)
        {
            var world = new EntityWorld();
            var lookup = new BattleEntityLookup();
            var node = world.Create(name);
            var factory = new BattleEntityFactory(world, lookup, node);
            var query = new BattleEntityQuery(world, lookup);
            return new EntityRuntime(world, lookup, node, factory, query);
        }

        private readonly struct EntityRuntime
        {
            public EntityRuntime(
                EntityWorld world,
                BattleEntityLookup lookup,
                IEntity node,
                BattleEntityFactory factory,
                BattleEntityQuery query)
            {
                World = world;
                Lookup = lookup;
                Node = node;
                Factory = factory;
                Query = query;
            }

            public EntityWorld World { get; }
            public BattleEntityLookup Lookup { get; }
            public IEntity Node { get; }
            public BattleEntityFactory Factory { get; }
            public BattleEntityQuery Query { get; }
        }
    }
}
