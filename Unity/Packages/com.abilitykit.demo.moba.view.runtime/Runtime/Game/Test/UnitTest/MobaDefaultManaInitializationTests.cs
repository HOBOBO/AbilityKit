using System.Collections.Generic;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using NUnit.Framework;

namespace AbilityKit.Game.UnitTests
{
    public sealed class MobaDefaultManaInitializationTests
    {
        [Test]
        public void EnsureContainers_CreatesDefaultManaState()
        {
            var context = new ActorContext();
            var actor = context.CreateEntity();

            new MobaActorAttributeInitializer().EnsureContainers(actor);

            Assert.IsTrue(actor.hasResourceContainer);
            Assert.IsTrue(actor.resourceContainer.Value.Map.TryGetValue(ResourceType.Mana, out var mana));
            Assert.NotNull(mana);
            Assert.AreEqual(MobaAttributeIds.MAX_MANA, mana.MaxAttribute);
            Assert.AreEqual(Fixed64.Zero, mana.Current);
            Assert.AreEqual(Fixed64.Zero, mana.LastMax);
        }

        [Test]
        public void EnsureContainers_DoesNotResetExistingManaState()
        {
            var context = new ActorContext();
            var actor = context.CreateEntity();
            var existing = new ResourceState
            {
                MaxAttribute = MobaAttributeIds.MAX_MANA,
                Current = Fixed64.FromSingle(17f),
                LastMax = Fixed64.FromSingle(80f)
            };
            actor.AddResourceContainer(
                new ResourceContainer
                {
                    Map = new Dictionary<ResourceType, ResourceState>
                    {
                        [ResourceType.Mana] = existing
                    }
                },
                true);

            new MobaActorAttributeInitializer().EnsureContainers(actor);

            Assert.AreSame(existing, actor.resourceContainer.Value.Map[ResourceType.Mana]);
            Assert.AreEqual(Fixed64.FromSingle(17f), existing.Current);
            Assert.AreEqual(Fixed64.FromSingle(80f), existing.LastMax);
        }
    }
}
