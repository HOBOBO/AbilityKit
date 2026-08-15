using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Demo.Common.Rooms;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SessionSimulationLifecycleTests
    {
        [Test]
        public void BattleSessionRuntime_OwnsStableStateAndHandlesForItsLifetime()
        {
            var runtime = new BattleSessionRuntime();

            Assert.That(runtime.State, Is.Not.Null);
            Assert.That(runtime.Handles, Is.Not.Null);
            Assert.That(runtime.State, Is.SameAs(runtime.State));
            Assert.That(runtime.Handles, Is.SameAs(runtime.Handles));
            Assert.That(runtime.SnapshotRouting, Is.Not.Null);
            Assert.That(runtime.SnapshotRouting, Is.SameAs(runtime.SnapshotRouting));
            Assert.That(runtime.Presentation, Is.Not.Null);
            Assert.That(runtime.Presentation, Is.SameAs(runtime.Presentation));
            Assert.That(runtime.Replication, Is.Not.Null);
            Assert.That(runtime.Replication, Is.SameAs(runtime.Replication));
            Assert.That(runtime.Diagnostics, Is.Not.Null);
            Assert.That(runtime.Diagnostics, Is.SameAs(runtime.Diagnostics));
            Assert.That(runtime.Simulation, Is.Null);
            Assert.That(runtime.Orchestrator, Is.Null);
        }

        [Test]
        public void BattleSessionRuntime_ConfiguresStableSimulationOwnerOnce()
        {
            var runtime = new BattleSessionRuntime();
            var installer = new TrackingSimulationInstaller();

            runtime.ConfigureSimulation(installer);

            Assert.That(runtime.Simulation, Is.Not.Null);
            Assert.That(runtime.Simulation, Is.SameAs(runtime.Simulation));
            Assert.That(runtime.Simulation.RemoteDriven, Is.SameAs(runtime.Handles.RemoteDriven));
            Assert.That(runtime.Simulation.Confirmed, Is.SameAs(runtime.Handles.Confirmed));
            Assert.Throws<InvalidOperationException>(() => runtime.ConfigureSimulation(installer));
        }

        [Test]
        public void BattleSessionRuntime_RejectsNullSimulationInstaller()
        {
            var runtime = new BattleSessionRuntime();

            Assert.Throws<ArgumentNullException>(() => runtime.ConfigureSimulation(null));
            Assert.That(runtime.Simulation, Is.Null);
        }

        [Test]
        public void BattleSimulationRuntime_StartsWorldsAndKeepsTickStateIsolated()
        {
            var state = new BattleSessionState();
            var handles = new BattleSessionHandles();
            var installer = new TrackingSimulationInstaller();
            var simulation = new BattleSimulationRuntime(state, handles, installer);
            var plan = CreatePlan();
            simulation.RemoteDrivenLastTickedFrame = 41;
            simulation.ConfirmedLastTickedFrame = 73;

            simulation.StartRemoteDriven(plan, null, 1f / 30f, _ => 1, () => false);

            Assert.That(installer.RemoteDrivenStartCount, Is.EqualTo(1));
            Assert.That(simulation.RemoteDrivenLastTickedFrame, Is.Zero);
            Assert.That(simulation.ConfirmedLastTickedFrame, Is.EqualTo(73));

            simulation.StartConfirmedAuthority(plan, null, null, false, 1f / 30f, _ => 1, _ => { });

            Assert.That(installer.ConfirmedStartCount, Is.EqualTo(1));
            Assert.That(simulation.RemoteDrivenLastTickedFrame, Is.Zero);
            Assert.That(simulation.ConfirmedLastTickedFrame, Is.Zero);
        }

        [Test]
        public void BattleSimulationRuntime_RemoteStartupFailureRollsBackOnlyRemoteState()
        {
            var state = new BattleSessionState();
            var handles = new BattleSessionHandles();
            var installer = new TrackingSimulationInstaller
            {
                RemoteDrivenFailure = new InvalidOperationException("remote start failed"),
            };
            var simulation = new BattleSimulationRuntime(state, handles, installer);
            var plan = CreatePlan();
            simulation.RemoteDrivenLastTickedFrame = 41;
            simulation.ConfirmedLastTickedFrame = 73;

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                simulation.StartRemoteDriven(plan, null, 1f / 30f, _ => 1, () => false));

            Assert.That(thrown, Is.SameAs(installer.RemoteDrivenFailure));
            Assert.That(simulation.RemoteDrivenLastTickedFrame, Is.Zero);
            Assert.That(simulation.ConfirmedLastTickedFrame, Is.EqualTo(73));
            Assert.That(handles.RemoteDriven.World, Is.Null);
            Assert.That(handles.Confirmed.World, Is.Null);
        }

        [Test]
        public void BattleSimulationRuntime_ConfirmedStartupFailureRollsBackOnlyConfirmedState()
        {
            var state = new BattleSessionState();
            var handles = new BattleSessionHandles();
            var installer = new TrackingSimulationInstaller
            {
                ConfirmedFailure = new InvalidOperationException("confirmed start failed"),
            };
            var simulation = new BattleSimulationRuntime(state, handles, installer);
            var plan = CreatePlan();
            simulation.RemoteDrivenLastTickedFrame = 41;
            simulation.ConfirmedLastTickedFrame = 73;

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                simulation.StartConfirmedAuthority(
                    plan,
                    null,
                    null,
                    false,
                    1f / 30f,
                    _ => 1,
                    _ => { }));

            Assert.That(thrown, Is.SameAs(installer.ConfirmedFailure));
            Assert.That(simulation.RemoteDrivenLastTickedFrame, Is.EqualTo(41));
            Assert.That(simulation.ConfirmedLastTickedFrame, Is.Zero);
            Assert.That(handles.RemoteDriven.World, Is.Null);
            Assert.That(handles.Confirmed.World, Is.Null);
        }

        [Test]
        public void BattleSimulationRuntime_RepeatedStartDoesNotRecreateOrResetWorlds()
        {
            var installer = new TrackingSimulationInstaller();
            var simulation = new BattleSimulationRuntime(
                new BattleSessionState(),
                new BattleSessionHandles(),
                installer);
            var plan = CreatePlan();

            simulation.StartRemoteDriven(plan, null, 1f / 30f, _ => 1, () => false);
            simulation.StartConfirmedAuthority(plan, null, null, false, 1f / 30f, _ => 1, _ => { });
            simulation.RemoteDrivenLastTickedFrame = 41;
            simulation.ConfirmedLastTickedFrame = 73;

            simulation.StartRemoteDriven(plan, null, 1f / 30f, _ => 1, () => false);
            simulation.StartConfirmedAuthority(plan, null, null, false, 1f / 30f, _ => 1, _ => { });

            Assert.That(installer.RemoteDrivenStartCount, Is.EqualTo(1));
            Assert.That(installer.ConfirmedStartCount, Is.EqualTo(1));
            Assert.That(simulation.RemoteDrivenLastTickedFrame, Is.EqualTo(41));
            Assert.That(simulation.ConfirmedLastTickedFrame, Is.EqualTo(73));
        }

        [Test]
        public void BattleSimulationRuntime_DisposeStepsAreIdempotentAndWorldIsolated()
        {
            var simulation = new BattleSimulationRuntime(
                new BattleSessionState(),
                new BattleSessionHandles(),
                new TrackingSimulationInstaller());
            simulation.RemoteDrivenLastTickedFrame = 41;
            simulation.ConfirmedLastTickedFrame = 73;

            simulation.DisposeRemoteDrivenWorld();
            simulation.DisposeRemoteDrivenWorld();

            Assert.That(simulation.RemoteDrivenLastTickedFrame, Is.Zero);
            Assert.That(simulation.ConfirmedLastTickedFrame, Is.EqualTo(73));

            simulation.DisposeConfirmedWorld(null);
            simulation.DisposeConfirmedWorld(null);

            Assert.That(simulation.ConfirmedLastTickedFrame, Is.Zero);
        }

        [Test]
        public void PresentationResources_DisabledInstallAndDisposeAreIdempotent()
        {
            var owner = new BattlePresentationSessionResources();

            owner.EnsureConfirmedViewInstalled(
                sourceContext: null,
                flow: null,
                authWorldId: default,
                enabled: false,
                destroyEntityTree: null);
            owner.DisposeConfirmedView(flow: null, destroyEntityTree: null);
            owner.DisposeConfirmedView(flow: null, destroyEntityTree: null);

            Assert.That(owner.ConfirmedContext, Is.Null);
            Assert.That(owner.ConfirmedFeature, Is.Null);
            Assert.That(owner.ConfirmedSnapshots, Is.Null);
        }

        [Test]
        public void PresentationResources_CreateTwoContextsAndDisposeThemIndependently()
        {
            var first = ConfirmedViewSideRuntimeFactory.Create(null, default, null);
            var second = ConfirmedViewSideRuntimeFactory.Create(null, default, null);

            Assert.That(first.Context, Is.Not.Null);
            Assert.That(second.Context, Is.Not.Null);
            Assert.That(first.Context, Is.Not.SameAs(second.Context));
            Assert.That(first.SnapshotRuntime, Is.Not.Null);
            Assert.That(second.SnapshotRuntime, Is.Not.Null);
            Assert.That(first.SnapshotRuntime.Snapshots, Is.Not.SameAs(second.SnapshotRuntime.Snapshots));
            Assert.That(first.Feature, Is.Not.Null);
            Assert.That(second.Feature, Is.Not.Null);

            first.SnapshotRuntime.Dispose();
            ConfirmedViewContextDisposer.Dispose(first.Context, null);

            Assert.That(second.Context.FrameSnapshots, Is.SameAs(second.SnapshotRuntime.Snapshots));
            Assert.That(second.SnapshotRuntime.Snapshots, Is.Not.Null);

            second.SnapshotRuntime.Dispose();
            second.SnapshotRuntime.Dispose();
            ConfirmedViewContextDisposer.Dispose(second.Context, null);
        }

        [Test]
        public void PresentationResources_RepeatedInstallKeepsCurrentGeneration()
        {
            var owner = new BattlePresentationSessionResources();
            var current = ConfirmedViewSideRuntimeFactory.Create(null, default, null);
            SetPrivateField(owner, "_confirmedContext", current.Context);
            SetPrivateField(owner, "_confirmedSnapshotRuntime", current.SnapshotRuntime);
            SetPrivateField(owner, "_confirmedFeature", current.Feature);

            owner.EnsureConfirmedViewInstalled(
                sourceContext: null,
                flow: new GameFlowDomain((IGameHost)null),
                authWorldId: default,
                enabled: true,
                destroyEntityTree: null);

            Assert.That(owner.ConfirmedContext, Is.SameAs(current.Context));
            Assert.That(owner.ConfirmedSnapshots, Is.SameAs(current.SnapshotRuntime.Snapshots));
            Assert.That(owner.ConfirmedFeature, Is.SameAs(current.Feature));

            owner.DisposeConfirmedView(flow: null, destroyEntityTree: null);
        }

        [Test]
        public void PresentationResources_StaleContextCleanupDoesNotClearReplacement()
        {
            var owner = new BattlePresentationSessionResources();
            var stale = ConfirmedViewSideRuntimeFactory.Create(null, default, null);
            var replacement = ConfirmedViewSideRuntimeFactory.Create(null, default, null);
            SetPrivateField(owner, "_confirmedContext", stale.Context);
            SetPrivateField(owner, "_confirmedSnapshotRuntime", stale.SnapshotRuntime);
            SetPrivateField(owner, "_confirmedFeature", stale.Feature);

            owner.DisposeConfirmedView(
                flow: null,
                destroyEntityTree: _ =>
                {
                    SetPrivateField(owner, "_confirmedContext", replacement.Context);
                    SetPrivateField(owner, "_confirmedSnapshotRuntime", replacement.SnapshotRuntime);
                    SetPrivateField(owner, "_confirmedFeature", replacement.Feature);
                });

            Assert.That(owner.ConfirmedContext, Is.SameAs(replacement.Context));
            Assert.That(owner.ConfirmedSnapshots, Is.SameAs(replacement.SnapshotRuntime.Snapshots));
            Assert.That(owner.ConfirmedFeature, Is.SameAs(replacement.Feature));

            owner.DisposeConfirmedView(flow: null, destroyEntityTree: null);
        }

        [Test]
        public void PresentationResources_TwoStableOwnersRemainIndependent()
        {
            var firstOwner = new BattlePresentationSessionResources();
            var secondOwner = new BattlePresentationSessionResources();

            Assert.That(firstOwner, Is.Not.SameAs(secondOwner));
            firstOwner.DisposeConfirmedView(flow: null, destroyEntityTree: null);
            secondOwner.DisposeConfirmedView(flow: null, destroyEntityTree: null);

            Assert.That(firstOwner.ConfirmedContext, Is.Null);
            Assert.That(secondOwner.ConfirmedContext, Is.Null);
        }

        [Test]
        public void SnapshotRouting_BuildAndDispose_PublishesAndClearsOwnedResources()
        {
            var runtime = new BattleSessionRuntime();
            var context = new BattleContext();

            runtime.SnapshotRouting.Build(CreatePlan(), context, null, null, null);

            Assert.That(runtime.SnapshotRouting.IsBuilt, Is.True);
            Assert.That(runtime.Handles.Snapshot.Snapshots, Is.SameAs(context.FrameSnapshots));
            Assert.That(runtime.Handles.Snapshot.Pipeline, Is.SameAs(context.SnapshotPipeline));
            Assert.That(runtime.Handles.Snapshot.CmdHandler, Is.SameAs(context.CmdHandler));
            Assert.That(runtime.Handles.Snapshot.Routing, Is.Not.Null);

            runtime.SnapshotRouting.Dispose();
            runtime.SnapshotRouting.Dispose();

            Assert.That(runtime.SnapshotRouting.IsBuilt, Is.False);
            Assert.That(runtime.Handles.Snapshot.Snapshots, Is.Null);
            Assert.That(runtime.Handles.Snapshot.Pipeline, Is.Null);
            Assert.That(runtime.Handles.Snapshot.CmdHandler, Is.Null);
            Assert.That(runtime.Handles.Snapshot.Routing, Is.Null);
            Assert.That(context.FrameSnapshots, Is.Null);
            Assert.That(context.SnapshotPipeline, Is.Null);
            Assert.That(context.CmdHandler, Is.Null);
        }

        [Test]
        public void SnapshotRouting_Rebuild_ReplacesGenerationAndClearsPreviousContext()
        {
            var runtime = new BattleSessionRuntime();
            var firstContext = new BattleContext();
            var secondContext = new BattleContext();

            runtime.SnapshotRouting.Build(CreatePlan(), firstContext, null, null, null);
            var firstSnapshots = firstContext.FrameSnapshots;
            var firstRouting = runtime.Handles.Snapshot.Routing;

            runtime.SnapshotRouting.Build(CreatePlan(), secondContext, null, null, null);

            Assert.That(firstContext.FrameSnapshots, Is.Null);
            Assert.That(firstContext.SnapshotPipeline, Is.Null);
            Assert.That(firstContext.CmdHandler, Is.Null);
            Assert.That(secondContext.FrameSnapshots, Is.Not.SameAs(firstSnapshots));
            Assert.That(runtime.Handles.Snapshot.Routing, Is.Not.SameAs(firstRouting));
            Assert.That(runtime.Handles.Snapshot.Snapshots, Is.SameAs(secondContext.FrameSnapshots));

            runtime.SnapshotRouting.Dispose();
        }

        [Test]
        public void SnapshotRouting_StaleOwnerDispose_DoesNotClearReplacementBindings()
        {
            var handles = new BattleSessionHandles();
            var context = new BattleContext();
            var staleDiagnostics = new BattleSessionDiagnostics(
                new BattleReplicationRuntime());
            var activeDiagnostics = new BattleSessionDiagnostics(
                new BattleReplicationRuntime());
            var staleOwner = new BattleSnapshotRoutingRuntime(
                handles,
                staleDiagnostics);
            var activeOwner = new BattleSnapshotRoutingRuntime(
                handles,
                activeDiagnostics);

            staleOwner.Build(CreatePlan(), context, null, null, null);
            activeOwner.Build(CreatePlan(), context, null, null, null);
            var activeSnapshots = context.FrameSnapshots;
            var activePipeline = context.SnapshotPipeline;
            var activeCmdHandler = context.CmdHandler;
            var activeRouting = handles.Snapshot.Routing;

            staleOwner.Dispose();

            Assert.That(context.FrameSnapshots, Is.SameAs(activeSnapshots));
            Assert.That(context.SnapshotPipeline, Is.SameAs(activePipeline));
            Assert.That(context.CmdHandler, Is.SameAs(activeCmdHandler));
            Assert.That(handles.Snapshot.Snapshots, Is.SameAs(activeSnapshots));
            Assert.That(handles.Snapshot.Pipeline, Is.SameAs(activePipeline));
            Assert.That(handles.Snapshot.CmdHandler, Is.SameAs(activeCmdHandler));
            Assert.That(handles.Snapshot.Routing, Is.SameAs(activeRouting));

            activeOwner.Dispose();
        }

        private static BattleStartPlan CreatePlan(
            string gatewaySessionToken = "token",
            bool gatewayAutoCreateRoom = false,
            bool gatewayAutoJoinRoom = false,
            int timeSyncIntervalMs = 1000)
        {
            return new BattleStartPlan(
                worldId: "world",
                worldType: "moba",
                clientId: "client",
                playerId: "1",
                tickRate: 30,
                inputDelayFrames: 0,
                hostMode: BattleHostMode.GatewayRemote,
                useGatewayTransport: true,
                gatewayHost: "127.0.0.1",
                gatewayPort: 4000,
                numericRoomId: 1,
                gatewaySessionToken: gatewaySessionToken,
                gatewayRegion: "test",
                gatewayServerId: "server",
                gatewayAutoCreateRoom: gatewayAutoCreateRoom,
                gatewayAutoJoinRoom: gatewayAutoJoinRoom,
                gatewayJoinRoomId: string.Empty,
                gatewayCreateRoomOpCode: 110,
                gatewayJoinRoomOpCode: 111,
                autoConnect: false,
                autoCreateWorld: false,
                autoJoin: false,
                autoReady: false,
                syncMode: BattleSyncMode.SnapshotAuthority,
                viewEventSourceMode: BattleViewEventSourceMode.SnapshotOnly,
                enableClientPrediction: true,
                enableConfirmedAuthorityWorld: true,
                enableInputRecording: false,
                inputRecordOutputPath: string.Empty,
                enableInputReplay: false,
                inputReplayPath: string.Empty,
                runMode: BattleRunMode.Normal,
                createWorldOpCode: 0,
                createWorldPayload: null,
                timeSyncIntervalMs: timeSyncIntervalMs);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private sealed class TrackingSimulationInstaller : IBattleSessionWorldInstaller
        {
            private bool _remoteDrivenStarted;
            private bool _confirmedStarted;

            public int RemoteDrivenStartCount { get; private set; }
            public int ConfirmedStartCount { get; private set; }
            public Exception RemoteDrivenFailure { get; set; }
            public Exception ConfirmedFailure { get; set; }

            public void EnsureRemoteDrivenStarted(RemoteDrivenWorldInstallOptions options)
            {
                if (_remoteDrivenStarted) return;

                RemoteDrivenStartCount++;
                options.ResetTickState?.Invoke();
                if (RemoteDrivenFailure != null) throw RemoteDrivenFailure;
                _remoteDrivenStarted = true;
            }

            public void EnsureConfirmedAuthorityStarted(ConfirmedAuthorityWorldInstallOptions options)
            {
                if (_confirmedStarted) return;

                ConfirmedStartCount++;
                options.ResetTickState?.Invoke();
                if (ConfirmedFailure != null) throw ConfirmedFailure;
                _confirmedStarted = true;
            }
        }
    }
}
