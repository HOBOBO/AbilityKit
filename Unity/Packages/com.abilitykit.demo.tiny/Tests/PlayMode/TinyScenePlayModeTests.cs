using System;
using System.Collections;
using System.Threading;
using AbilityKit.Demo.Common.Composition;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator ProjectLaunchInstantiatesTinyRootThroughSceneBootstrap()
        {
            var launch = new DemoMultiplayerLaunchRequest(
                "127.0.0.1", 1, "dev", "local", "alice", "test-session",
                TimeSpan.FromSeconds(1));
            TinyProjectLaunch.Open(launch, "TinyDemoGameplayScene");
            yield return null;

            var bootstrap = UnityEngine.Object.FindObjectOfType<DemoGameplayBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.LastError, Is.Empty);
            Assert.That(bootstrap.ActiveRoot, Is.Not.Null);
            Assert.That(bootstrap.ActiveRoot.GetComponent<TinyGameplayRoot>(), Is.Not.Null);
            Assert.That(bootstrap.ActiveRoot.scene.name, Is.EqualTo("TinyDemoGameplayScene"));
            Assert.That(DemoMultiplayerLaunchIntent.TryPeek(out _, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator TinySceneLoadsAndViewObjectsHaveOwnedLifetime()
        {
            LogAssert.Expect(LogType.Error, "[DemoGameplayBootstrap] No demo launch request is pending.");
            SceneManager.LoadScene("TinyDemoGameplayScene", LoadSceneMode.Single);
            yield return null;

            var bootstrap = UnityEngine.Object.FindObjectOfType<DemoGameplayBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.LastError, Does.Contain("No demo launch request"));

            var root = new GameObject("Tiny PlayMode View Root");
            var module = new TinyActorViewModule();
            var context = new TinyViewModuleContext(root.transform, () => null,
                CancellationToken.None, exception => throw exception);
            try
            {
                module.OnAttach(in context);
                Assert.That(root.transform.Find("Tiny Arena"), Is.Not.Null);
                Assert.That(root.GetComponentInChildren<Camera>(), Is.Not.Null);
                Assert.That(root.GetComponentInChildren<Light>(), Is.Not.Null);
                module.OnDetach(in context);
                yield return null;
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
