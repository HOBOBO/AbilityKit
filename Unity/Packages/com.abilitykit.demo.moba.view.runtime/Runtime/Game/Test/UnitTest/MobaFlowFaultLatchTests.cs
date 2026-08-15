using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.View.Settings;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaFlowFaultLatchTests
    {
        [Test]
        public void Tick_WhenHfsmStepFails_LatchesFaultAndSkipsFeatureDispatch()
        {
            var countingFeature = new CountingFeature("demo_lobby");
            var faultFeature = new ControlledAttachFeature(throwOnAttachCount: 2);
            var trailingFeature = new CountingFeature("root_debug");
            var log = new RecordingLogSink();
            var domain = CreateDomain(countingFeature, faultFeature, trailingFeature, log);

            domain.Start();
            domain.Tick(0.016f);

            Assert.IsTrue(domain.IsFaulted);
            Assert.AreEqual(2, countingFeature.AttachCount);
            Assert.AreEqual(1, countingFeature.DetachCount);
            Assert.AreEqual(2, faultFeature.AttachCount);
            Assert.AreEqual(0, countingFeature.TickCount);
            Assert.AreEqual(0, countingFeature.GuiCount);
            Assert.AreEqual(1, trailingFeature.AttachCount);
            Assert.AreEqual(1, log.Exceptions.Count);

            domain.Tick(0.016f);
            domain.OnGUI();

            Assert.AreEqual(0, countingFeature.TickCount);
            Assert.AreEqual(0, countingFeature.GuiCount);
            Assert.AreEqual(1, log.Exceptions.Count);

            domain.Shutdown();
            domain.Shutdown();

            Assert.AreEqual(2, countingFeature.DetachCount);
            Assert.AreEqual(2, faultFeature.DetachCount);
            Assert.AreEqual(1, trailingFeature.DetachCount);
            Assert.AreEqual(1, log.Exceptions.Count);
        }

        [Test]
        public void Lifecycle_DoesNotRestartAnExistingOrShutdownFlow()
        {
            var domain = CreateDomain(
                new CountingFeature("demo_lobby"),
                new ControlledAttachFeature(),
                new CountingFeature("root_debug"),
                new RecordingLogSink());

            domain.Start();

            Assert.Throws<InvalidOperationException>(() => domain.Start());

            domain.Shutdown();

            Assert.Throws<InvalidOperationException>(() => domain.StartWithPersistentSettingsSync());
        }

        private static GameFlowDomain CreateDomain(
            CountingFeature countingFeature,
            ControlledAttachFeature controlledFeature,
            CountingFeature trailingFeature,
            RecordingLogSink log)
        {
            return new GameFlowDomain(
                new TestRuntimeServices(),
                new TestFeatureFactoryProvider(countingFeature, controlledFeature, trailingFeature),
                log);
        }

        private sealed class TestFeatureFactoryProvider : IMobaFeatureFactoryProvider
        {
            private readonly CountingFeature _countingFeature;
            private readonly ControlledAttachFeature _controlledFeature;
            private readonly CountingFeature _trailingFeature;

            public TestFeatureFactoryProvider(
                CountingFeature countingFeature,
                ControlledAttachFeature controlledFeature,
                CountingFeature trailingFeature)
            {
                _countingFeature = countingFeature;
                _controlledFeature = controlledFeature;
                _trailingFeature = trailingFeature;
            }

            public MobaFeatureFactoryRegistry CreateFeatureFactoryRegistry(
                Func<IBattleSessionFeature> createBattleSessionFeature)
            {
                return new MobaFeatureFactoryRegistry()
                    .Register("demo_lobby", (in GamePhaseContext ctx) => _countingFeature)
                    .Register("formal_lobby", (in GamePhaseContext ctx) => _controlledFeature)
                    .Register("root_debug", (in GamePhaseContext ctx) => _trailingFeature);
            }
        }

        private sealed class CountingFeature : IGamePhaseFeature, IOnGUIFeature
        {
            public CountingFeature(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public int Priority => 0;
            public bool IsEnabled { get; set; } = true;
            public int AttachCount { get; private set; }
            public int DetachCount { get; private set; }
            public int TickCount { get; private set; }
            public int GuiCount { get; private set; }

            public void OnAttach(in GamePhaseContext ctx) => AttachCount++;
            public void OnDetach(in GamePhaseContext ctx) => DetachCount++;
            public void Tick(in GamePhaseContext ctx, float deltaTime) => TickCount++;
            public void OnGUI(in GamePhaseContext ctx) => GuiCount++;
        }

        private sealed class ControlledAttachFeature : IGamePhaseFeature
        {
            private readonly int _throwOnAttachCount;

            public ControlledAttachFeature(int throwOnAttachCount = 0)
            {
                _throwOnAttachCount = throwOnAttachCount;
            }

            public string Name => "formal_lobby";
            public int Priority => 0;
            public bool IsEnabled { get; set; } = true;
            public int AttachCount { get; private set; }
            public int DetachCount { get; private set; }

            public void OnAttach(in GamePhaseContext ctx)
            {
                AttachCount++;
                if (AttachCount == _throwOnAttachCount)
                    throw new InvalidOperationException("Expected feature attach failure.");
            }

            public void OnDetach(in GamePhaseContext ctx) => DetachCount++;

            public void Tick(in GamePhaseContext ctx, float deltaTime)
            {
            }
        }

        private sealed class TestRuntimeServices : IGameFlowRuntimeServices
        {
            public IGameHost Host => null;
            public IFeatureBinder FeatureBinder { get; } = new TestFeatureBinder();
            public IGameFeatureStore Features { get; } = new TestFeatureStore();
            public IBattleEntityRuntime BattleEntities { get; } = new TestBattleEntityRuntime();

            public void LoadPersistentSettings(LayeredJsonSettingsStore settings)
            {
            }

            public void LoadPersistentSettingsSync(LayeredJsonSettingsStore settings)
            {
            }

            public bool TrySaveSettingsOverridesToPersistent(LayeredJsonSettingsStore settings) => true;
        }

        private sealed class TestFeatureBinder : IFeatureBinder
        {
            public void AttachFeature(object feature)
            {
            }

            public void DetachFeature(object feature)
            {
            }
        }

        private sealed class TestFeatureStore : IGameFeatureStore
        {
            private readonly Dictionary<Type, object> _components = new Dictionary<Type, object>();

            public bool TryGet<T>(out T component) where T : class
            {
                if (_components.TryGetValue(typeof(T), out var value))
                {
                    component = (T)value;
                    return true;
                }

                component = null;
                return false;
            }

            public void Set<T>(T component) where T : class => _components[typeof(T)] = component;
            public void Remove<T>() where T : class => _components.Remove(typeof(T));
            public void Remove(Type componentType) => _components.Remove(componentType);
        }

        private sealed class TestBattleEntityRuntime : IBattleEntityRuntime
        {
            public bool TryGetWorld<TWorld>(out TWorld world)
            {
                world = default(TWorld);
                return false;
            }

            public bool TryCreateNode<TNode>(string debugName, out TNode node)
            {
                node = default(TNode);
                return false;
            }

            public void DestroyTree<TNode>(TNode root)
            {
            }
        }

        private sealed class RecordingLogSink : ILogSink
        {
            public List<Exception> Exceptions { get; } = new List<Exception>();

            public void Info(string message)
            {
            }

            public void Warning(string message)
            {
            }

            public void Error(string message)
            {
            }

            public void Exception(Exception exception, string message = null)
            {
                Exceptions.Add(exception);
            }
        }
    }
}
