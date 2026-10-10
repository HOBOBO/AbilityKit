using System;
using System.Collections.Generic;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Modules;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleSessionModuleCompositionTests
    {
        [Test]
        public void ConfiguredModules_UseProfileOrderAndPassDependencyValidation()
        {
            var modules = new List<ISessionSubFeature<ISessionRuntimeFeature>>();
            SessionSubFeaturePipeline.AddConfiguredSessionSubFeatures(
                modules, new[] { "session_plan", "session_events" });

            var host = SessionSubFeaturePipeline.CreateHost(modules, message => Assert.Fail(message));
            Assert.IsTrue(host.TrySortByDependencies());
            Assert.AreEqual("session_events", ((IGameModuleId)modules[0]).Id);
            Assert.AreEqual("session_plan", ((IGameModuleId)modules[1]).Id);
        }

        [Test]
        public void ConfiguredModules_RejectUnknownId()
        {
            var modules = new List<ISessionSubFeature<ISessionRuntimeFeature>>();
            Assert.Throws<InvalidOperationException>(() =>
                SessionSubFeaturePipeline.AddConfiguredSessionSubFeatures(
                    modules, new[] { "not_registered" }));
        }

        [Test]
        public void DefaultSessionFactory_AdvertisesOnlyRegisteredModules()
        {
            IBattleSessionModuleCatalog catalog = new DefaultBattleSessionFeatureFactory();
            Assert.IsTrue(catalog.ContainsModule("session_events"));
            Assert.IsTrue(catalog.ContainsModule("session_plan"));
            Assert.IsFalse(catalog.ContainsModule("not_registered"));
        }

        [Test]
        public void SessionModule_RejectsMissingRuntimeCapabilityOnAttach()
        {
            var context = new FeatureModuleContext<ISessionRuntimeFeature>(
                default, new MissingCapabilityRuntime());
            var module = new SessionDispatchersSubFeature();

            var error = Assert.Throws<InvalidOperationException>(() => module.OnAttach(context));
            StringAssert.Contains("session_dispatchers", error.Message);
            StringAssert.Contains(nameof(ISessionDispatchersRuntime), error.Message);
        }

        [Test]
        public void SessionModules_RequireCapabilitiesForTheSelectedRunMode()
        {
            var replayModules = new[] { "session_events", "session_plan" };
            var localModules = new[]
            {
                "session_events", "session_plan", "snapshot_routing",
                "session_dispatchers", "session_lifecycle", "session_tick_loop"
            };
            var localPlan = CreateLocalPlan();

            Assert.DoesNotThrow(() => SessionSubFeaturePipeline.ValidateCapabilitiesForPlan(
                replayModules, localPlan.WithInputReplay("record.bin")));
            Assert.Throws<InvalidOperationException>(() =>
                SessionSubFeaturePipeline.ValidateCapabilitiesForPlan(replayModules, localPlan));
            Assert.DoesNotThrow(() =>
                SessionSubFeaturePipeline.ValidateCapabilitiesForPlan(localModules, localPlan));
        }

        private static BattleStartPlan CreateLocalPlan() => new BattleStartPlan(
            worldId: "1", worldType: "moba", clientId: "client", playerId: "1",
            tickRate: 30, inputDelayFrames: 0, useGatewayTransport: false,
            gatewayHost: string.Empty, gatewayPort: 0, numericRoomId: 0,
            gatewaySessionToken: string.Empty, gatewayRegion: string.Empty,
            gatewayServerId: string.Empty, gatewayAutoCreateRoom: false,
            gatewayAutoJoinRoom: false, gatewayJoinRoomId: string.Empty,
            gatewayCreateRoomOpCode: 0, gatewayJoinRoomOpCode: 0,
            autoConnect: false, autoCreateWorld: false, autoJoin: false,
            autoReady: false, syncMode: BattleSyncMode.Lockstep,
            viewEventSourceMode: BattleViewEventSourceMode.SnapshotOnly,
            enableConfirmedAuthorityWorld: false, enableInputRecording: false,
            inputRecordOutputPath: string.Empty, enableInputReplay: false,
            inputReplayPath: string.Empty, createWorldOpCode: 0,
            createWorldPayload: null);

        [Test]
        public void PauseControl_ResolvesRecoveryWithinItsBattleContext()
        {
            var first = BattleContext.Rent();
            var second = BattleContext.Rent();
            var firstRecovery = new MobaBattleRecoveryRuntime();
            var secondRecovery = new MobaBattleRecoveryRuntime();
            try
            {
                MobaBattlePauseController.Register(first, firstRecovery);
                MobaBattlePauseController.Register(second, secondRecovery);
                Assert.AreSame(firstRecovery, MobaBattlePauseController.For(first));
                Assert.AreSame(secondRecovery, MobaBattlePauseController.For(second));

                MobaBattlePauseController.Unregister(first, firstRecovery);
                Assert.IsNull(MobaBattlePauseController.For(first));
                Assert.AreSame(secondRecovery, MobaBattlePauseController.For(second));
            }
            finally
            {
                MobaBattlePauseController.Unregister(first, firstRecovery);
                MobaBattlePauseController.Unregister(second, secondRecovery);
                BattleContext.Return(first);
                BattleContext.Return(second);
            }
        }

        [Test]
        public void SessionTeardown_UnbindsContextBeforeResettingHandles()
        {
            var feature = new BattleSessionFeature(null);
            var runtime = (BattleSessionRuntime)typeof(BattleSessionFeature)
                .GetField("_runtime", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).GetValue(feature);
            var context = BattleContext.Rent();
            try
            {
                runtime.Handles.Phase.Ctx = context;
                runtime.BindContext(context);
                var createSteps = typeof(BattleSessionFeature).GetMethod(
                    "CreateTeardownSteps", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                var steps = (AsyncSessionTeardownStep[])createSteps.Invoke(feature,
                    new object[] { default(GamePhaseContext) });
                var inputIndex = Array.FindIndex(steps, step => step.Name == "input context");
                var handlesIndex = Array.FindIndex(steps, step => step.Name == "session handles");
                Assert.That(inputIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(handlesIndex, Is.GreaterThan(inputIndex));

                steps[inputIndex].Cleanup().GetAwaiter().GetResult();
                steps[handlesIndex].Cleanup().GetAwaiter().GetResult();
                Assert.IsNull(MobaBattlePauseController.For(context));
                Assert.IsNull(runtime.Handles.Phase.Ctx);
            }
            finally
            {
                MobaBattlePauseController.Unregister(context, runtime.LockstepRecovery);
                BattleContext.Return(context);
            }
        }

        private sealed class MissingCapabilityRuntime : ISessionRuntimeFeature
        {
            public ISessionPlanHost PlanHost => null;
            public ISessionEventsHost EventsHost => null;
        }
    }
}
