using System;
using System.Collections;
using AbilityKit.Demo.Common.Composition;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TinyConsumer.Tests
{
    public sealed class TinyConsumerPlayModeTests
    {
        [UnityTest]
        public IEnumerator OwnLobbyLaunchesTinyWithoutStarter()
        {
            SceneManager.LoadScene("ConsumerLobby", LoadSceneMode.Single);
            yield return null;
            var lobby = UnityEngine.Object.FindObjectOfType<TinyConsumerLobby>();
            Assert.That(lobby, Is.Not.Null);

            var launch = new DemoMultiplayerLaunchRequest(
                "127.0.0.1", 1, "dev", "local", "alice", "test-session",
                TimeSpan.FromSeconds(1));
            lobby.Enter(launch);
            yield return null;

            var bootstrap = UnityEngine.Object.FindObjectOfType<DemoGameplayBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.LastError, Is.Empty);
            Assert.That(bootstrap.ActiveRoot, Is.Not.Null);
            Assert.That(bootstrap.ActiveRoot.GetComponent<TinyGameplayRoot>(), Is.Not.Null);
            Assert.That(DemoMultiplayerLaunchIntent.TryPeek(out _, out _), Is.False);

            bootstrap.ActiveRoot.SendMessage("ReturnToLobbyAsync");
            for (var frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "ConsumerLobby"; frame++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("ConsumerLobby"));
            Assert.That(UnityEngine.Object.FindObjectOfType<TinyConsumerLobby>(), Is.Not.Null);
        }
    }
}
