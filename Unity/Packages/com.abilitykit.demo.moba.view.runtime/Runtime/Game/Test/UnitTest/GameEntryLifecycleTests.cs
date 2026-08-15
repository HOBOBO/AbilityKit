using System.Reflection;
using AbilityKit.Game.Flow;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class GameEntryLifecycleTests
    {
        [TearDown]
        public void TearDown()
        {
            if (GameEntry.IsInitialized)
            {
                Object.DestroyImmediate(GameEntry.Instance.gameObject);
            }
        }

        [Test]
        public void GameEntry_AwakeAttachesBootstrapModuleAndOnDestroyDetachesIt()
        {
            var go = new GameObject("GameEntryLifecycleTests.GameEntry");
            var entry = go.AddComponent<GameEntry>();

            InvokePrivate(entry, "Awake");

            Assert.IsTrue(GameEntry.IsInitialized);
            Assert.IsTrue(entry.Root.IsValid);
            Assert.IsTrue(entry.TryGet(out GameManager gm));
            Assert.IsTrue(gm.IsInGame);
            var flow = entry.Get<GameFlowDomain>();
            Assert.IsTrue(entry.TryGetNode(1, out var systems));
            Assert.IsTrue(systems.IsValid);

            InvokePrivate(entry, "OnDestroy");
            Object.DestroyImmediate(go);

            Assert.IsFalse(GameEntry.IsInitialized);
            Assert.IsFalse(gm.IsInGame);
            Assert.IsFalse(entry.Root.IsValid);
            Assert.IsNull(entry.World);
            Assert.Throws<System.InvalidOperationException>(() => flow.Start());
        }

        [Test]
        public void GameEntry_IsOnlyUnityLifecycleOwnerForEntryBootstrap()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(GameEntry)));
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(typeof(GameEntryBootstrap)));
            Assert.IsTrue(typeof(IGameEntryModule).IsAssignableFrom(typeof(GameEntryBootstrap)));
            Assert.IsNull(typeof(GameEntryModuleContext).GetField("Entry"));
        }

        [Test]
        public void EntryBootstrap_DependsOnlyOnRootLifecycle()
        {
            var world = new EntityWorld();
            var root = world.Create("EntryBootstrap_DependsOnlyOnRootLifecycle");
            var context = new GameEntryModuleContext(host: null, root: root);
            var bootstrap = new GameEntryBootstrap();

            bootstrap.OnAttach(in context);

            Assert.IsTrue(root.TryGetRef(out GameManager gameManager));
            Assert.IsTrue(gameManager.IsInGame);
            Assert.IsTrue(root.TryGetChildById(1, out var systems));
            Assert.IsTrue(systems.IsValid);

            bootstrap.OnDetach(in context);

            Assert.IsFalse(gameManager.IsInGame);
            world.DestroyRecursive(root.Id);
        }

        [Test]
        public void RuntimeAdapter_BindsFeatureByConcreteRuntimeType()
        {
            var root = new EntityWorld().Create("RuntimeAdapter_BindsFeatureByConcreteRuntimeType");
            var runtime = new GameFlowRuntimeAdapter(root);
            var feature = new TestRuntimeFeature();

            runtime.FeatureBinder.AttachFeature(feature);

            Assert.IsTrue(runtime.Features.TryGet<TestRuntimeFeature>(out var resolved));
            Assert.AreSame(feature, resolved);
            Assert.IsFalse(runtime.Features.TryGet<object>(out _));

            runtime.FeatureBinder.DetachFeature(feature);

            Assert.IsFalse(runtime.Features.TryGet<TestRuntimeFeature>(out _));
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing private method: {methodName}");
            method.Invoke(target, null);
        }

        private sealed class TestRuntimeFeature
        {
        }
    }
}
