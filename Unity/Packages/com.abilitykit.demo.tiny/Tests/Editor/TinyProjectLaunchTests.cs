using System;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;
using NUnit.Framework;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyProjectLaunchTests
    {
        [TearDown]
        public void TearDown() => TinyProjectLaunch.Clear();

        [Test]
        public void AuthenticatedLaunchIsConsumedOnceWithReturnScene()
        {
            var launch = new DemoMultiplayerLaunchRequest(
                "127.0.0.1", 4000, "dev", "local", "alice", "session", TimeSpan.FromSeconds(10));
            TinyProjectLaunch.Prepare(launch, "MyLobby");

            Assert.That(DemoLaunchIntent.TryConsume(out var composition), Is.True);
            Assert.That(composition.Gameplay, Is.EqualTo(DemoGameplayId.Tiny));
            Assert.That(composition.Mode, Is.EqualTo(DemoLaunchMode.Multiplayer));
            Assert.That(composition.ProfileId, Is.EqualTo("tiny-multiplayer"));
            Assert.That(DemoMultiplayerLaunchIntent.TryPeek(out var gameplay, out var multiplayer), Is.True);
            Assert.That(gameplay, Is.EqualTo(DemoMultiplayerGameplay.Tiny));
            Assert.That(multiplayer, Is.SameAs(launch));

            Assert.That(TinyProjectLaunch.TryConsume(out var consumed, out var returnScene), Is.True);
            Assert.That(consumed, Is.SameAs(launch));
            Assert.That(returnScene, Is.EqualTo("MyLobby"));
            Assert.That(TinyProjectLaunch.TryConsume(out _, out _), Is.False);
            Assert.That(DemoMultiplayerLaunchIntent.TryPeek(out _, out _), Is.False);
        }

        [Test]
        public void UnauthenticatedLaunchIsRejected()
        {
            var launch = new DemoMultiplayerLaunchRequest(
                "127.0.0.1", 4000, "dev", "local", "alice", "", TimeSpan.FromSeconds(10));
            Assert.That(() => TinyProjectLaunch.Prepare(launch, "MyLobby"), Throws.ArgumentException);
            Assert.That(DemoLaunchIntent.TryConsume(out _), Is.False);
            Assert.That(DemoMultiplayerLaunchIntent.TryPeek(out _, out _), Is.False);
        }
    }
}
