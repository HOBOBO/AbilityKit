using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Snapshot;
using NUnit.Framework;

namespace AbilityKit.World.StateSync.Editor.Tests
{
    public sealed class SnapshotBufferTests
    {
        [Test]
        public void StateSyncPackage_BufferOwnsCopiesAndKeepsLatestFrames()
        {
            var history = new SnapshotBuffer();
            Assert.That(history.Capacity, Is.EqualTo(128));
            Assert.That(history.TrySetCapacity(2), Is.True);
            var submitted = new WorldStateSnapshot { Frame = 1, WorldFlags = 10 };
            history.Store(1, submitted);
            submitted.WorldFlags = 99;
            var retrieved = history.Get(1);
            Assert.That(retrieved.WorldFlags, Is.EqualTo(10));
            retrieved.WorldFlags = 77;
            Assert.That(history.Get(1).WorldFlags, Is.EqualTo(10));

            history.Store(3, new WorldStateSnapshot { Frame = 3 });
            history.Store(2, new WorldStateSnapshot { Frame = 2 });
            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history.GetCapturedFrames(), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(history.TryGet(1, out _), Is.False);
        }
    }
}
