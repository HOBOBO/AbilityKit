using System;
using AbilityKit.World.ECS;
using NUnit.Framework;

namespace AbilityKit.Game.Flow
{
    public sealed class EntityWorldAllocationPlayModeTests
    {
        [Test]
        public void Create_UsesReservedSlotsBeforeGrowing_AndReusesDestroyedSlot()
        {
            var world = new EntityWorld(initialCapacity: 3, maxCapacity: 5);
            var first = world.Create();
            var second = world.Create();
            var third = world.Create();

            Assert.AreEqual(0, first.Id.Index);
            Assert.AreEqual(1, second.Id.Index);
            Assert.AreEqual(2, third.Id.Index);
            Assert.AreEqual(3, world.TotalCapacity);

            var fourth = world.Create();
            Assert.AreEqual(3, fourth.Id.Index);
            Assert.AreEqual(5, world.TotalCapacity);

            world.Destroy(second.Id);
            var reused = world.Create();
            Assert.AreEqual(second.Id.Index, reused.Id.Index);
            Assert.AreNotEqual(second.Id.Version, reused.Id.Version);
            Assert.IsFalse(second.IsValid);
            Assert.AreEqual(5, world.TotalCapacity);

            Assert.AreEqual(4, world.Create().Id.Index);
            Assert.Throws<InvalidOperationException>(() => world.Create());
            Assert.AreEqual(5, world.AliveCount);
        }

        [Test]
        public void Create_ClampsInitialCapacityToMaximum()
        {
            var world = new EntityWorld(maxCapacity: 2);
            Assert.AreEqual(2, world.TotalCapacity);
            world.Create();
            world.Create();
            Assert.Throws<InvalidOperationException>(() => world.Create());
        }
    }
}
