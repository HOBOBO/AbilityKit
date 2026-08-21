using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Battle.Projection;
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
        public void FixedStepBudget_NormalUpdate_PreservesRemainderWithoutOverBudget()
        {
            var result = FixedStepBudgetPolicy.Evaluate(
                accumulatorSeconds: 0.01f,
                deltaTime: 0.24f,
                fixedDeltaSeconds: 0.1f);

            Assert.That(result.Steps, Is.EqualTo(2));
            Assert.That(result.BacklogSteps, Is.Zero);
            Assert.That(result.AccumulatorSeconds, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(result.DroppedSeconds, Is.Zero);
            Assert.That(result.OverBudget, Is.False);
            Assert.That(result.InvalidDelta, Is.False);
        }

        [Test]
        public void FixedStepBudget_LargeDelta_ClampsAndRetainsBoundedBacklog()
        {
            var result = FixedStepBudgetPolicy.Evaluate(
                accumulatorSeconds: 0f,
                deltaTime: 1.2f,
                fixedDeltaSeconds: 0.1f);

            Assert.That(result.Steps, Is.EqualTo(FixedStepBudgetPolicy.MaxStepsPerUpdate));
            Assert.That(result.BacklogSteps, Is.EqualTo(5));
            Assert.That(result.AccumulatorSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(result.DroppedSeconds, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(result.OverBudget, Is.True);

            var drained = FixedStepBudgetPolicy.Evaluate(
                result.AccumulatorSeconds,
                deltaTime: 0f,
                fixedDeltaSeconds: 0.1f);

            Assert.That(drained.Steps, Is.EqualTo(5));
            Assert.That(drained.BacklogSteps, Is.Zero);
            Assert.That(drained.AccumulatorSeconds, Is.Zero.Within(0.0001f));
            Assert.That(drained.DroppedSeconds, Is.Zero);
            Assert.That(drained.OverBudget, Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-0.1f)]
        public void FixedStepBudget_InvalidDelta_IsRejected(float deltaTime)
        {
            var result = FixedStepBudgetPolicy.Evaluate(
                accumulatorSeconds: 0f,
                deltaTime,
                fixedDeltaSeconds: 0.1f);

            Assert.That(result.Steps, Is.Zero);
            Assert.That(result.AccumulatorSeconds, Is.Zero);
            Assert.That(result.DroppedSeconds, Is.Zero);
            Assert.That(result.InvalidDelta, Is.True);
        }

        [Test]
        public void BattleSessionTickProjection_ExposesBudgetTelemetry()
        {
            var projection = BattleSessionTickProjector.Create(
                lastFrame: 30,
                tickAccumulator: 0.05f,
                fixedDeltaSeconds: 0.1f,
                lastUpdateSteps: 5,
                backlogSteps: 3,
                overBudgetUpdateCount: 7L,
                droppedTimeSeconds: 1.25d,
                invalidDeltaCount: 2L);

            Assert.That(projection.LastFrame, Is.EqualTo(30));
            Assert.That(projection.LogicTimeSeconds, Is.EqualTo(3.05d).Within(0.0001d));
            Assert.That(projection.LastUpdateSteps, Is.EqualTo(5));
            Assert.That(projection.BacklogSteps, Is.EqualTo(3));
            Assert.That(projection.OverBudgetUpdateCount, Is.EqualTo(7L));
            Assert.That(projection.DroppedTimeSeconds, Is.EqualTo(1.25d));
            Assert.That(projection.InvalidDeltaCount, Is.EqualTo(2L));
        }

        [Test]
        public void TickState_Reset_ClearsBudgetTelemetry()
        {
            var tick = new BattleSessionState.TickState
            {
                LastFrame = 30,
                TickAcc = 0.5f,
                LastUpdateSteps = 5,
                BacklogSteps = 3,
                OverBudgetUpdateCount = 7L,
                DroppedTimeSeconds = 1.25d,
                InvalidDeltaCount = 2L,
                WorldReady = true,
                FirstFrameReceived = true,
            };

            tick.Reset();

            Assert.That(tick.LastFrame, Is.Zero);
            Assert.That(tick.TickAcc, Is.Zero);
            Assert.That(tick.LastUpdateSteps, Is.Zero);
            Assert.That(tick.BacklogSteps, Is.Zero);
            Assert.That(tick.OverBudgetUpdateCount, Is.Zero);
            Assert.That(tick.DroppedTimeSeconds, Is.Zero);
            Assert.That(tick.InvalidDeltaCount, Is.Zero);
            Assert.That(tick.WorldReady, Is.False);
            Assert.That(tick.FirstFrameReceived, Is.False);
        }

        [Test]
        public void BattleSessionState_PublishesLifecycleGenerationAndTransitions()
        {
            var state = new BattleSessionState();

            state.BeginStart();
            var starting = state.LifecycleDiagnosticsSnapshot;
            state.CompleteStart();
            state.BeginStop();
            state.CompleteStop();
            var stopped = state.LifecycleDiagnosticsSnapshot;

            Assert.That(starting.Generation, Is.EqualTo(1));
            Assert.That(starting.State, Is.EqualTo(SessionLifecycleDiagnosticState.Starting));
            Assert.That(stopped.Generation, Is.EqualTo(1));
            Assert.That(stopped.PreviousState, Is.EqualTo(SessionLifecycleDiagnosticState.Stopping));
            Assert.That(stopped.State, Is.EqualTo(SessionLifecycleDiagnosticState.Stopped));
            Assert.That(stopped.HasPendingOperation, Is.False);
            Assert.That(stopped.HasTeardownFailure, Is.False);
        }

        [Test]
        public void LifecycleDiagnosticsRecorder_RejectsStaleOperationCompletion()
        {
            var diagnostics = new SessionLifecycleDiagnosticsRecorder();
            diagnostics.BeginGeneration(1, SessionLifecycleDiagnosticState.Running);
            var staleGeneration = diagnostics.BeginPendingOperation("old-stop");
            diagnostics.BeginGeneration(2, SessionLifecycleDiagnosticState.Starting);

            diagnostics.CompletePendingOperation(
                staleGeneration,
                TimeSpan.FromSeconds(3),
                new InvalidOperationException("stale"),
                SessionLifecycleDiagnosticState.Faulted);

            var snapshot = diagnostics.Snapshot;
            Assert.That(snapshot.Generation, Is.EqualTo(2));
            Assert.That(snapshot.State, Is.EqualTo(SessionLifecycleDiagnosticState.Starting));
            Assert.That(snapshot.LastStopLatency, Is.Zero);
            Assert.That(snapshot.HasTeardownFailure, Is.False);
        }

        [Test]
        public void WorldCapabilities_StaleOwnerClearDoesNotClearReplacement()
        {
            var firstProducer = new TrackingProjectionProducer();
            var replacementProducer = new TrackingProjectionProducer();
            var firstWorld = CreateWorld(firstProducer, "first");
            var replacementWorld = CreateWorld(replacementProducer, "replacement");
            var capabilities = new BattleSessionWorldCapabilities();

            capabilities.Bind(firstWorld);
            capabilities.Bind(replacementWorld);

            Assert.That(capabilities.OwnerWorld, Is.SameAs(replacementWorld));
            Assert.That(capabilities.ProjectionProducer, Is.SameAs(replacementProducer));
            Assert.That(capabilities.Clear(firstWorld), Is.False);
            Assert.That(capabilities.OwnerWorld, Is.SameAs(replacementWorld));
            Assert.That(capabilities.ProjectionProducer, Is.SameAs(replacementProducer));
            Assert.That(capabilities.Clear(replacementWorld), Is.True);
            Assert.That(capabilities.OwnerWorld, Is.Null);
            Assert.That(capabilities.ProjectionProducer, Is.Null);
        }

        [Test]
        public void RemoteDrivenWorldRuntime_ResetClearsCapabilities()
        {
            var producer = new TrackingProjectionProducer();
            var world = CreateWorld(producer, "remote");
            var handles = new BattleSessionRemoteDrivenWorldRuntime();
            handles.BindWorldRuntime(new RemoteDrivenWorldRuntime(
                world.Id,
                worlds: null,
                runtime: null,
                world));

            Assert.That(handles.World, Is.SameAs(world));
            Assert.That(handles.Capabilities.OwnerWorld, Is.SameAs(world));
            Assert.That(handles.Capabilities.ProjectionProducer, Is.SameAs(producer));

            handles.Reset();

            Assert.That(handles.World, Is.Null);
            Assert.That(handles.Capabilities.OwnerWorld, Is.Null);
            Assert.That(handles.Capabilities.ProjectionProducer, Is.Null);
        }

        [Test]
        public void BattleSessionDiagnostics_StaleOwnerCannotClearReplacementMetricSink()
        {
            var diagnostics = new BattleSessionDiagnostics(new BattleReplicationRuntime());
            var firstWorld = CreateWorld(new TrackingProjectionProducer(), "first");
            var replacementWorld = CreateWorld(new TrackingProjectionProducer(), "replacement");

            InvokeBindMetricSink(diagnostics, firstWorld);
            InvokeBindMetricSink(diagnostics, replacementWorld);

            Assert.That(diagnostics.ClearMetricSink(firstWorld), Is.False);
            Assert.That(GetPrivateField<IWorld>(diagnostics, "_metricSinkWorld"),
                Is.SameAs(replacementWorld));
            Assert.That(diagnostics.ClearMetricSink(replacementWorld), Is.True);
            Assert.That(GetPrivateField<IWorld>(diagnostics, "_metricSinkWorld"), Is.Null);
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
        public void SessionRuntimeResourcesPort_UsesCurrentAccessorsAndRuntimeHandles()
        {
            var runtime = new BattleSessionRuntime();
            var installer = new TrackingSimulationInstaller();
            runtime.ConfigureSimulation(installer);
            var plan = CreatePlan();
            var initialContext = new BattleContext();
            var currentContext = new BattleContext();
            var fixedDeltaSeconds = 0.05f;
            var port = new SessionRuntimeResourcesPort(
                runtime,
                () => plan,
                () => currentContext,
                () => null,
                () => false,
                () => fixedDeltaSeconds,
                _ => 17,
                _ => { });
            var world = CreateWorld(new TrackingProjectionProducer(), "remote");
            runtime.Handles.RemoteDriven.BindWorldRuntime(new RemoteDrivenWorldRuntime(
                world.Id,
                worlds: null,
                runtime: null,
                world));

            currentContext = initialContext;
            fixedDeltaSeconds = 0.125f;
            port.StartRemoteDrivenLocalWorld();

            Assert.That(installer.LastRemoteDrivenOptions.Context, Is.SameAs(initialContext));
            Assert.That(installer.LastRemoteDrivenOptions.FixedDeltaSeconds, Is.EqualTo(0.125f));
            Assert.That(installer.LastRemoteDrivenOptions.ResolveIdealFrameLimit(default), Is.EqualTo(17));

            port.ResetSessionHandles();

            Assert.That(runtime.Handles.RemoteDriven.World, Is.Null);
            Assert.That(runtime.Handles.RemoteDriven.Capabilities.OwnerWorld, Is.Null);
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

        private static TestWorld CreateWorld(IActorProjectionProducer producer, string id)
        {
            var services = new TestWorldResolver();
            services.Add(producer);
            return new TestWorld(services, id);
        }

        private static void InvokeBindMetricSink(BattleSessionDiagnostics diagnostics, IWorld world)
        {
            var method = typeof(BattleSessionDiagnostics).GetMethod(
                "BindMetricSink",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Metric sink bind method was not found.");
            method.Invoke(diagnostics, new object[] { world, null });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field '{fieldName}' was not found.");
            return (T)field.GetValue(target);
        }

        private sealed class TestWorldResolver : IWorldResolver
        {
            private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

            internal void Add<T>(T service)
            {
                _services[typeof(T)] = service;
                _services[service.GetType()] = service;
            }

            public object Resolve(Type serviceType) => _services[serviceType];

            public T Resolve<T>() => (T)Resolve(typeof(T));

            public bool TryResolve(Type serviceType, out object instance) =>
                _services.TryGetValue(serviceType, out instance);

            public bool TryResolve<T>(out T instance)
            {
                if (_services.TryGetValue(typeof(T), out var service))
                {
                    instance = (T)service;
                    return true;
                }

                instance = default;
                return false;
            }
        }

        private sealed class TestWorld : IWorld
        {
            internal TestWorld(IWorldResolver services, string id)
            {
                Services = services;
                Id = new WorldId(id);
            }

            public WorldId Id { get; }
            public string WorldType => "moba";
            public IWorldResolver Services { get; }
            public void Initialize() { }
            public void Tick(float deltaTime) { }
            public void Dispose() { }
        }

        private sealed class TrackingProjectionProducer : IActorProjectionProducer
        {
            public ActorProjectionData ExtractFull(int actorId) => default;

            public void ExtractAll(List<ActorProjectionData> buffer)
            {
            }

            public ActorProjectionData ExtractSpawn(int actorId) => default;
        }

        private sealed class TrackingSimulationInstaller : IBattleSessionWorldInstaller
        {
            private bool _remoteDrivenStarted;
            private bool _confirmedStarted;

            public int RemoteDrivenStartCount { get; private set; }
            public int ConfirmedStartCount { get; private set; }
            public RemoteDrivenWorldInstallOptions LastRemoteDrivenOptions { get; private set; }
            public Exception RemoteDrivenFailure { get; set; }
            public Exception ConfirmedFailure { get; set; }

            public void EnsureRemoteDrivenStarted(RemoteDrivenWorldInstallOptions options)
            {
                if (_remoteDrivenStarted) return;

                LastRemoteDrivenOptions = options;
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
