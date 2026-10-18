using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Demo.Moba.View.Settings;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaFlowFaultLatchTests
    {
        [Test]
        public void Tick_WhenStateMachineStepFails_LatchesFaultAndSkipsFeatureDispatch()
        {
            var countingFeature = new CountingFeature("demo_lobby");
            var faultFeature = new ControlledAttachFeature(throwOnAttachCount: 1);
            var trailingFeature = new CountingFeature("root_debug");
            var log = new RecordingLogSink();
            var domain = CreateDomain(countingFeature, faultFeature, trailingFeature, log);

            domain.Start();
            domain.Tick(0.016f);

            Assert.IsTrue(domain.IsFaulted,
                $"phase={domain.CurrentPhase}, demoAttach={countingFeature.AttachCount}, " +
                $"formalAttach={faultFeature.AttachCount}, exceptions={log.Exceptions.Count}");
            Assert.AreEqual(1, countingFeature.AttachCount);
            Assert.AreEqual(1, countingFeature.DetachCount);
            Assert.AreEqual(1, faultFeature.AttachCount);
            Assert.AreEqual(0, countingFeature.TickCount);
            Assert.AreEqual(0, countingFeature.GuiCount);
            Assert.AreEqual(0, trailingFeature.AttachCount);
            Assert.AreEqual(1, log.Exceptions.Count);

            domain.Tick(0.016f);
            domain.OnGUI();

            Assert.AreEqual(0, countingFeature.TickCount);
            Assert.AreEqual(0, countingFeature.GuiCount);
            Assert.AreEqual(1, log.Exceptions.Count);

            domain.Shutdown();
            domain.Shutdown();

            Assert.AreEqual(1, countingFeature.DetachCount);
            Assert.AreEqual(1, faultFeature.DetachCount);
            Assert.AreEqual(0, trailingFeature.DetachCount);
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

        [Test]
        public void EnterBattle_UsesPresetFlowProfileBeforeBattleStarts()
        {
            var preset = ScriptableObject.CreateInstance<BattleStartPresetSO>();
            preset.FlowProfileId = "moba.quiet";
            var domain = CreateDomain(new CountingFeature("demo_lobby"),
                new ControlledAttachFeature(), new CountingFeature("root_debug"),
                new RecordingLogSink());
            try
            {
                domain.Start();
                domain.Tick(0.016f);
                domain.EnterBattle(new ConfiguredBattleBootstrapper(null, preset));
                Assert.AreEqual("moba.quiet", domain.CurrentBattleProfileId);
                Assert.IsNotEmpty(domain.CurrentSessionModuleIds);
                Assert.AreEqual(0, domain.CurrentGameplayId);
            }
            finally
            {
                domain.Shutdown();
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void EnterBattle_WhenGateRejects_ReleasesScopeAndAllowsAnotherAttempt()
        {
            var domain = CreateDomain(new CountingFeature("demo_lobby"),
                new ControlledAttachFeature(), new CountingFeature("root_debug"),
                new RecordingLogSink());
            domain.Start();
            domain.Tick(0.016f);

            domain.EnterBattle(new RejectedGateBootstrapper());
            Assert.AreEqual(MobaBattleEntryState.Pending, domain.BattleEntryState);
            domain.Tick(0.016f);

            Assert.AreEqual(MobaBattleEntryState.Rejected, domain.BattleEntryState);
            Assert.AreEqual(MobaRootState.Lobby, domain.CurrentPhase);
            StringAssert.Contains("roomReady=False", domain.BattleEntryRejection);
            Assert.IsFalse(domain.IsFaulted);
            Assert.DoesNotThrow(() => domain.EnterBattle(new RejectedGateBootstrapper()));
            domain.Shutdown();
        }

        [Test]
        public void SessionFailure_ReturnsToLobbyWithoutSettlement()
        {
            var settlement = new CountingFeature("end_settlement");
            var factory = new TestFeatureFactoryProvider(new CountingFeature("demo_lobby"),
                new ControlledAttachFeature(), new CountingFeature("root_debug"), settlement);
            var domain = new GameFlowDomain(new TestRuntimeServices(), factory,
                new FailingSessionFactory(), new RecordingLogSink());
            domain.Start();
            domain.Tick(0.016f);
            domain.EnterBattle(null);
            domain.Tick(0.016f);

            for (var i = 0; i < 6 && domain.CurrentPhase != MobaRootState.Lobby; i++)
                domain.Tick(0.016f);

            Assert.AreEqual(MobaRootState.Lobby, domain.CurrentPhase);
            Assert.AreEqual(MobaBattleExitKind.SessionFailed, domain.LastBattleExitKind);
            Assert.AreEqual(0, settlement.AttachCount);
            Assert.IsFalse(domain.IsFaulted);
            domain.Shutdown();
        }

        private sealed class FailingSessionFactory : IBattleSessionFeatureFactory
        {
            public IBattleSessionFeature Create(IBattleBootstrapper bootstrapper,
                Func<BattleStartPlan, AbilityKit.Network.Abstractions.IConnection> connectionFactory) =>
                new FailingSessionFeature();
        }

        private sealed class FailingSessionFeature : IBattleSessionFeature
        {
            public event Action SessionStarted;
            public event Action WorldReady;
            public event Action FirstFrameReceived;
            public event Action<Exception> SessionFailed;
            public event Action AssetsLoadCompleted;

            public void OnAttach(in GamePhaseContext ctx) =>
                SessionFailed?.Invoke(new InvalidOperationException("Session failed."));
            public void OnDetach(in GamePhaseContext ctx) { }
            public void Tick(in GamePhaseContext ctx, float deltaTime) { }
            public Task DetachAsync(GamePhaseContext ctx) => Task.CompletedTask;
        }

        private sealed class RejectedGateBootstrapper : IBattleBootstrapper, IFlowGateProvider
        {
            public BattleStartPlan Build() => throw new InvalidOperationException("Gate should reject before build.");
            public bool IsAuthenticated => true;
            public bool IsRoomReady => false;
            public bool IsConnectivityReady => true;
            public bool IsAssetsReady => true;
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
            private readonly CountingFeature _settlementFeature;

            public TestFeatureFactoryProvider(
                CountingFeature countingFeature,
                ControlledAttachFeature controlledFeature,
                CountingFeature trailingFeature,
                CountingFeature settlementFeature = null)
            {
                _countingFeature = countingFeature;
                _controlledFeature = controlledFeature;
                _trailingFeature = trailingFeature;
                _settlementFeature = settlementFeature ?? new CountingFeature("end_settlement");
            }

            public MobaFeatureFactoryRegistry CreateFeatureFactoryRegistry(
                Func<IBattleSessionFeature> createBattleSessionFeature)
            {
                return new MobaFeatureFactoryRegistry()
                    .Register("demo_lobby", (in GamePhaseContext ctx) => _countingFeature)
                    .Register("formal_lobby", (in GamePhaseContext ctx) => _controlledFeature)
                    .Register("formal_lobby_ui", (in GamePhaseContext ctx) => _trailingFeature)
                    .Register("root_debug", (in GamePhaseContext ctx) => _trailingFeature)
                    .Register("context", (in GamePhaseContext ctx) => new CountingFeature("context"))
                    .Register("entity", (in GamePhaseContext ctx) => new CountingFeature("entity"))
                    .Register("session", (in GamePhaseContext ctx) => createBattleSessionFeature())
                    .Register("battle_outcome", (in GamePhaseContext ctx) => new CountingFeature("battle_outcome"))
                    .Register("gameplay_presentation", (in GamePhaseContext ctx) => new CountingFeature("gameplay_presentation"))
                    .Register("debug_ongui", (in GamePhaseContext ctx) => new CountingFeature("debug_ongui"))
                    .Register("loading_screen", (in GamePhaseContext ctx) => new CountingFeature("loading_screen"))
                    .Register("sync", (in GamePhaseContext ctx) => new CountingFeature("sync"))
                    .Register("input", (in GamePhaseContext ctx) => new CountingFeature("input"))
                    .Register("view", (in GamePhaseContext ctx) => new CountingFeature("view"))
                    .Register("hud", (in GamePhaseContext ctx) => new CountingFeature("hud"))
                    .Register("end_settlement", (in GamePhaseContext ctx) => _settlementFeature);
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
