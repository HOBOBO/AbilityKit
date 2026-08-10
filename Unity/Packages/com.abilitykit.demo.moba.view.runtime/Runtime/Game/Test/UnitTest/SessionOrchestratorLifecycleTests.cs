using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Common.Rooms;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SessionOrchestratorLifecycleTests
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
            Assert.That(runtime.Orchestrator, Is.Null);
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
            var staleOwner = new BattleSnapshotRoutingRuntime(handles);
            var activeOwner = new BattleSnapshotRoutingRuntime(handles);

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

        [Test]
        public void GatewayRoom_BuildTickAndDispose_PublishesAndClearsOwnedResources()
        {
            var handles = new BattleSessionHandles();
            var registry = new TrackingConnectionRegistry();
            var connectionFactory = new TrackingGatewayConnectionFactory();
            var clientFactory = new TrackingGatewayRoomClientFactory();
            var owner = CreateGatewayOwner(handles, registry, connectionFactory, clientFactory);
            var dispatcher = new InlineDispatcher();

            owner.Build(CreatePlan(), dispatcher, dispatcher);
            var connection = connectionFactory.Connections[0];

            Assert.That(owner.IsBuilt, Is.True);
            Assert.That(owner.Connection, Is.SameAs(connection));
            Assert.That(owner.Client, Is.SameAs(clientFactory.LastClient));
            Assert.That(handles.GatewayRoom.Conn, Is.SameAs(connection));
            Assert.That(handles.GatewayRoom.Client, Is.SameAs(clientFactory.LastClient));
            Assert.That(handles.GatewayRoom.ConnectionOwner, Is.Not.Null);
            Assert.That(connection.OpenCount, Is.EqualTo(1));
            Assert.That(connection.LastHost, Is.EqualTo("127.0.0.1"));
            Assert.That(connection.LastPort, Is.EqualTo(4000));

            owner.Tick(0.25f);
            Assert.That(connection.TickCount, Is.EqualTo(1));
            Assert.That(connection.LastDeltaTime, Is.EqualTo(0.25f));

            owner.Dispose();
            owner.Dispose();

            Assert.That(owner.IsBuilt, Is.False);
            Assert.That(handles.GatewayRoom.Conn, Is.Null);
            Assert.That(handles.GatewayRoom.Client, Is.Null);
            Assert.That(handles.GatewayRoom.ConnectionOwner, Is.Null);
            Assert.That(registry.RemoveCount, Is.EqualTo(1));
            Assert.That(connection.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void GatewayRoom_BuildFailure_RollsBackPublishedConnection()
        {
            var handles = new BattleSessionHandles();
            var registry = new TrackingConnectionRegistry();
            var connectionFactory = new TrackingGatewayConnectionFactory();
            var clientFactory = new TrackingGatewayRoomClientFactory { FailOnCreate = true };
            var owner = CreateGatewayOwner(handles, registry, connectionFactory, clientFactory);
            var dispatcher = new InlineDispatcher();

            Assert.Throws<InvalidOperationException>(() =>
                owner.Build(CreatePlan(), dispatcher, dispatcher));

            Assert.That(owner.IsBuilt, Is.False);
            Assert.That(owner.Connection, Is.Null);
            Assert.That(owner.Client, Is.Null);
            Assert.That(handles.GatewayRoom.Conn, Is.Null);
            Assert.That(handles.GatewayRoom.Client, Is.Null);
            Assert.That(handles.GatewayRoom.ConnectionOwner, Is.Null);
            Assert.That(registry.RemoveCount, Is.EqualTo(1));
            Assert.That(connectionFactory.Connections[0].DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void GatewayRoom_Rebuild_ReplacesAndDisposesPreviousGeneration()
        {
            var handles = new BattleSessionHandles();
            var registry = new TrackingConnectionRegistry();
            var connectionFactory = new TrackingGatewayConnectionFactory();
            var clientFactory = new TrackingGatewayRoomClientFactory();
            var owner = CreateGatewayOwner(handles, registry, connectionFactory, clientFactory);
            var dispatcher = new InlineDispatcher();

            owner.Build(CreatePlan(), dispatcher, dispatcher);
            var firstConnection = connectionFactory.Connections[0];
            var firstClient = owner.Client;

            owner.Build(CreatePlan(), dispatcher, dispatcher);
            var secondConnection = connectionFactory.Connections[1];

            Assert.That(firstConnection.DisposeCount, Is.EqualTo(1));
            Assert.That(owner.Connection, Is.SameAs(secondConnection));
            Assert.That(owner.Client, Is.Not.SameAs(firstClient));
            Assert.That(handles.GatewayRoom.Conn, Is.SameAs(secondConnection));
            Assert.That(registry.RemoveCount, Is.EqualTo(1));

            owner.Dispose();
            Assert.That(secondConnection.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void GatewayRoom_StaleOwnerDispose_DoesNotClearOrDisposeActiveGeneration()
        {
            var handles = new BattleSessionHandles();
            var registry = new TrackingConnectionRegistry();
            var staleFactory = new TrackingGatewayConnectionFactory();
            var activeFactory = new TrackingGatewayConnectionFactory();
            var staleOwner = CreateGatewayOwner(
                handles,
                registry,
                staleFactory,
                new TrackingGatewayRoomClientFactory());
            var activeOwner = CreateGatewayOwner(
                handles,
                registry,
                activeFactory,
                new TrackingGatewayRoomClientFactory());
            var dispatcher = new InlineDispatcher();

            staleOwner.Build(CreatePlan(), dispatcher, dispatcher);
            var activeConnection = new TrackingConnection();
            registry.Register(
                AbilityKitConnectionRole.GatewayReliable,
                activeConnection);
            activeOwner.Build(CreatePlan(), dispatcher, dispatcher);
            var activeClient = activeOwner.Client;
            var activeToken = handles.GatewayRoom.ConnectionOwner;

            staleOwner.Dispose();
            activeOwner.Tick(0.5f);

            Assert.That(handles.GatewayRoom.ConnectionOwner, Is.SameAs(activeToken));
            Assert.That(handles.GatewayRoom.Conn, Is.SameAs(activeConnection));
            Assert.That(handles.GatewayRoom.Client, Is.SameAs(activeClient));
            Assert.That(registry.GetRequired(AbilityKitConnectionRole.GatewayReliable), Is.SameAs(activeConnection));
            Assert.That(activeConnection.DisposeCount, Is.Zero);
            Assert.That(activeConnection.TickCount, Is.EqualTo(1));
            Assert.That(registry.RemoveCount, Is.Zero);

            activeOwner.Dispose();
            Assert.That(activeConnection.DisposeCount, Is.EqualTo(1));
            Assert.That(registry.RemoveCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GatewayRoom_CompletePreparation_PreservesSessionDataUntilDispose()
        {
            var handles = new BattleSessionHandles();
            var registry = new TrackingConnectionRegistry();
            var connectionFactory = new TrackingGatewayConnectionFactory();
            var client = new ControllableGatewayRoomClient();
            var clientFactory = new TrackingGatewayRoomClientFactory { Client = client };
            var owner = CreateGatewayOwner(handles, registry, connectionFactory, clientFactory);
            var dispatcher = new InlineDispatcher();
            var anchor = new GatewayWorldStartAnchor(1000L, 100L, 7, 1.0 / 30.0);
            var clockPublished = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            client.JoinRoomCompletion.SetResult(
                new GatewayJoinRoomResult(42UL, string.Empty, in anchor));

            owner.Build(
                CreatePlan(gatewayAutoJoinRoom: true),
                dispatcher,
                dispatcher);
            owner.StartPreparation(
                CreatePlan(gatewayAutoJoinRoom: true),
                null,
                (_, __) => clockPublished.TrySetResult(true),
                null);
            await AwaitWithTimeoutAsync(owner.PreparationTask);
            var request = await client.WaitForTimeSyncRequestAsync();
            request.Completion.SetResult(new GatewayTimeSyncResult(
                request.ClientSendTicks,
                request.ClientSendTicks,
                System.Diagnostics.Stopwatch.Frequency));
            await AwaitWithTimeoutAsync(clockPublished.Task);

            owner.CompletePreparation();

            Assert.That(owner.IsBuilt, Is.False);
            Assert.That(connectionFactory.Connections[0].DisposeCount, Is.EqualTo(1));
            Assert.That(owner.ClockEstimate.HasClockSync, Is.True);
            Assert.That(owner.TryGetWorldStartAnchor(
                new AbilityKit.Ability.World.Abstractions.WorldId("world"),
                out var retainedAnchor), Is.True);
            Assert.That(retainedAnchor.StartFrame, Is.EqualTo(7));

            owner.Dispose();
            owner.Dispose();

            Assert.That(owner.ClockEstimate.HasClockSync, Is.False);
            Assert.That(owner.WorldStartAnchors, Is.Empty);
            Assert.That(connectionFactory.Connections[0].DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GatewayPreparation_Success_PublishesTokenRoomAnchorAndCallOrder()
        {
            var clock = new GatewayClockSynchronizer();
            var runtime = new GatewayPreparationRuntime(clock);
            var connection = new TrackingConnection();
            var client = new ControllableGatewayRoomClient();
            var anchor = new GatewayWorldStartAnchor(1000L, 100L, 7, 1.0 / 30.0);
            BattleStartPlan publishedPlan = default;
            client.GuestLoginCompletion.SetResult("guest-token");
            client.CreateRoomCompletion.SetResult(new GatewayCreateRoomResult("room-42", 42UL));
            client.JoinRoomCompletion.SetResult(new GatewayJoinRoomResult(42UL, string.Empty, in anchor));
            connection.Open("127.0.0.1", 4000);

            runtime.Start(
                connection,
                client,
                CreatePlan(
                    gatewaySessionToken: string.Empty,
                    gatewayAutoCreateRoom: true),
                plan => publishedPlan = plan,
                null,
                null);
            await AwaitWithTimeoutAsync(runtime.Task);
            await client.WaitForTimeSyncRequestAsync();

            Assert.That(client.Calls, Is.EqualTo(new[] { "guest-login", "create-room", "join-room", "time-sync" }));
            Assert.That(client.GuestLoginToken.CanBeCanceled, Is.True);
            Assert.That(client.CreateRoomToken, Is.EqualTo(client.GuestLoginToken));
            Assert.That(client.JoinRoomToken, Is.EqualTo(client.GuestLoginToken));
            Assert.That(publishedPlan.Gateway.SessionToken, Is.EqualTo("guest-token"));
            Assert.That(publishedPlan.Gateway.NumericRoomId, Is.EqualTo(42UL));
            Assert.That(publishedPlan.World.WorldId, Is.EqualTo("room-42"));
            Assert.That(runtime.TryGetWorldStartAnchor(
                new AbilityKit.Ability.World.Abstractions.WorldId("room-42"),
                out var publishedAnchor), Is.True);
            Assert.That(publishedAnchor.StartFrame, Is.EqualTo(7));

            runtime.Dispose();
        }

        [Test]
        public async Task GatewayPreparation_StopWork_CancelsAndRejectsLateLoginCompletion()
        {
            var runtime = new GatewayPreparationRuntime(new GatewayClockSynchronizer());
            var connection = new TrackingConnection();
            var client = new ControllableGatewayRoomClient();
            var publishCount = 0;
            connection.Open("127.0.0.1", 4000);

            runtime.Start(
                connection,
                client,
                CreatePlan(gatewaySessionToken: string.Empty),
                _ => publishCount++,
                null,
                null);
            var task = runtime.Task;
            runtime.StopWork();
            client.GuestLoginCompletion.SetResult("late-token");

            Assert.That(await IsCanceledAsync(task), Is.True);
            Assert.That(client.GuestLoginToken.IsCancellationRequested, Is.True);
            Assert.That(publishCount, Is.Zero);
            Assert.That(runtime.Task, Is.Null);

            runtime.Dispose();
        }

        [Test]
        public async Task GatewayPreparation_RepeatedStart_RejectsPreviousGenerationCompletion()
        {
            var clock = new GatewayClockSynchronizer();
            var runtime = new GatewayPreparationRuntime(clock);
            var connection = new TrackingConnection();
            var staleClient = new ControllableGatewayRoomClient();
            var activeClient = new ControllableGatewayRoomClient();
            var publishedTokens = new List<string>();
            connection.Open("127.0.0.1", 4000);
            activeClient.GuestLoginCompletion.SetResult("active-token");

            runtime.Start(
                connection,
                staleClient,
                CreatePlan(gatewaySessionToken: string.Empty),
                plan => publishedTokens.Add(plan.Gateway.SessionToken),
                null,
                null);
            var staleTask = runtime.Task;
            runtime.Start(
                connection,
                activeClient,
                CreatePlan(gatewaySessionToken: string.Empty),
                plan => publishedTokens.Add(plan.Gateway.SessionToken),
                null,
                null);
            var activeTask = runtime.Task;
            staleClient.GuestLoginCompletion.SetResult("stale-token");

            await AwaitWithTimeoutAsync(activeTask);
            Assert.That(await IsCanceledAsync(staleTask), Is.True);
            Assert.That(publishedTokens, Is.EqualTo(new[] { "active-token" }));
            Assert.That(staleClient.GuestLoginToken.IsCancellationRequested, Is.True);

            runtime.Dispose();
        }

        [Test]
        public async Task GatewayClock_FirstSamplePublishesAndStopPreservesEstimateUntilClear()
        {
            var runtime = new GatewayClockSynchronizer();
            var client = new ControllableGatewayRoomClient();
            var published = new TaskCompletionSource<GatewayTimeSyncEwma>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var options = CreatePlan(timeSyncIntervalMs: 60000).TimeSync;

            runtime.Start(
                client,
                in options,
                (estimate, _) => published.TrySetResult(estimate),
                null);
            var request = await client.WaitForTimeSyncRequestAsync();
            request.Completion.SetResult(new GatewayTimeSyncResult(
                request.ClientSendTicks,
                request.ClientSendTicks,
                System.Diagnostics.Stopwatch.Frequency));
            var estimate = await AwaitWithTimeoutAsync(published.Task);

            Assert.That(estimate.HasClockSync, Is.True);
            Assert.That(estimate.Samples, Is.EqualTo(1));
            runtime.StopWork();
            Assert.That(runtime.Estimate.HasClockSync, Is.True);
            runtime.ClearEstimate();
            Assert.That(runtime.Estimate.HasClockSync, Is.False);

            runtime.Dispose();
        }

        [Test]
        public async Task GatewayClock_NotifiesAtFailureThresholdAndRejectsStaleGeneration()
        {
            var runtime = new GatewayClockSynchronizer();
            var failingClient = new ControllableGatewayRoomClient
            {
                AutomaticTimeSyncFailure = new InvalidOperationException("time sync failed"),
            };
            var failure = new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var fastOptions = CreatePlan(timeSyncIntervalMs: 1).TimeSync;

            runtime.Start(
                failingClient,
                in fastOptions,
                null,
                exception => failure.TrySetResult(exception));
            var notified = await AwaitWithTimeoutAsync(failure.Task);

            Assert.That(notified.Message, Is.EqualTo("time sync failed"));
            Assert.That(failingClient.TimeSyncCallCount, Is.GreaterThanOrEqualTo(3));
            runtime.StopWork();

            var staleClient = new ControllableGatewayRoomClient();
            var activeClient = new ControllableGatewayRoomClient();
            var stalePublishCount = 0;
            runtime.Start(
                staleClient,
                in fastOptions,
                (_, __) => stalePublishCount++,
                null);
            var staleRequest = await staleClient.WaitForTimeSyncRequestAsync();
            runtime.Start(activeClient, in fastOptions, null, null);
            staleRequest.Completion.SetResult(new GatewayTimeSyncResult(
                staleRequest.ClientSendTicks,
                staleRequest.ClientSendTicks,
                System.Diagnostics.Stopwatch.Frequency));
            await Task.Delay(20);

            Assert.That(stalePublishCount, Is.Zero);
            Assert.That(staleRequest.Token.IsCancellationRequested, Is.True);

            runtime.Dispose();
        }

        [Test]
        public void BattleSessionRuntime_RejectsNullOrchestratorHost()
        {
            var runtime = new BattleSessionRuntime();

            Assert.Throws<ArgumentNullException>(() => runtime.ConfigureOrchestrator(null));
            Assert.That(runtime.Orchestrator, Is.Null);
        }

        [Test]
        public void BattleSessionRuntime_RejectsOrchestratorReconfiguration()
        {
            var runtime = new BattleSessionRuntime();
            var host = new FailureInjectingHost(CreatePlan());

            runtime.ConfigureOrchestrator(host);

            Assert.That(runtime.Orchestrator, Is.Not.Null);
            Assert.That(runtime.Orchestrator, Is.SameAs(runtime.Orchestrator));
            Assert.Throws<InvalidOperationException>(() => runtime.ConfigureOrchestrator(host));
        }

        private static readonly string[] StartupFailureSteps =
        {
            "start-logic",
            "subscribe",
            "start-remote",
            "start-confirmed",
            "pipeline-start",
            "replay-setup",
        };

        private static readonly string[] CleanupOrder =
        {
            "pipeline-stop",
            "snapshot-routing",
            "confirmed-view",
            "destroy-worlds",
            "confirmed-world",
            "remote-world",
            "remote-interpolation",
            "unsubscribe",
            "stop-logic",
            "reset-handles",
        };

        [TestCaseSource(nameof(StartupFailureSteps))]
        public void StartSession_FailureAtEveryHostPhase_CleansUpAndFaults(string failureStep)
        {
            var fixture = CreateFixture();
            fixture.Host.FailNext(failureStep);

            Assert.Throws<InvalidOperationException>(() => fixture.Orchestrator.StartSession());

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Faulted));
            Assert.That(fixture.State.LastLifecycleFailure, Is.Not.Null);
            Assert.That(fixture.State.Generation, Is.EqualTo(1));
            Assert.That(fixture.Host.HasActiveSessionResources, Is.False);
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));

            var expectedCleanup = new List<string>(CleanupOrder);
            if (Array.IndexOf(StartupFailureSteps, failureStep) < 4)
            {
                expectedCleanup.Remove("pipeline-stop");
            }
            Assert.That(fixture.Host.CleanupCalls, Is.EqualTo(expectedCleanup));
        }

        [Test]
        public void StopSession_WhenCleanupStepThrows_ContinuesAndResumesOnlyFailedWork()
        {
            var fixture = CreateFixture();
            fixture.Orchestrator.StartSession();
            fixture.Host.FailNext("confirmed-world");

            Assert.Throws<AggregateException>(() => fixture.Orchestrator.StopSession());

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Faulted));
            Assert.That(fixture.Host.Calls, Does.Contain("remote-world"));
            Assert.That(fixture.Host.Calls, Does.Contain("stop-logic"));
            Assert.That(fixture.Host.ResetHandlesCount, Is.Zero);
            Assert.That(fixture.Host.CountCalls("confirmed-world"), Is.EqualTo(1));
            Assert.That(fixture.Host.CountCalls("pipeline-stop"), Is.EqualTo(1));

            fixture.Orchestrator.StopSession();

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Stopped));
            Assert.That(fixture.State.LastLifecycleFailure, Is.Null);
            Assert.That(fixture.Host.HasActiveSessionResources, Is.False);
            Assert.That(fixture.Host.CountCalls("confirmed-world"), Is.EqualTo(2));
            Assert.That(fixture.Host.CountCalls("pipeline-stop"), Is.EqualTo(1));
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));

            var callCount = fixture.Host.Calls.Count;
            fixture.Orchestrator.StopSession();
            Assert.That(fixture.Host.Calls.Count, Is.EqualTo(callCount));
        }

        [Test]
        public void StartSession_AfterStartupFailure_RetriesWithNewGeneration()
        {
            var fixture = CreateFixture();
            fixture.Host.FailNext("replay-setup");

            Assert.Throws<InvalidOperationException>(() => fixture.Orchestrator.StartSession());
            fixture.Orchestrator.StartSession();

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Running));
            Assert.That(fixture.State.LastLifecycleFailure, Is.Null);
            Assert.That(fixture.State.Generation, Is.EqualTo(2));
            Assert.That(fixture.Host.LogicSessionActive, Is.True);
            Assert.That(fixture.Host.CountCalls("start-logic"), Is.EqualTo(2));
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));

            fixture.Orchestrator.StopSession();
            Assert.That(fixture.Host.HasActiveSessionResources, Is.False);
        }

        [Test]
        public void StopSession_AfterSuccessfulStop_IsIdempotent()
        {
            var fixture = CreateFixture();
            fixture.Orchestrator.StartSession();

            fixture.Orchestrator.StopSession();
            var callCount = fixture.Host.Calls.Count;
            fixture.Orchestrator.StopSession();

            Assert.That(fixture.State.Lifecycle, Is.EqualTo(BattleSessionLifecycleState.Stopped));
            Assert.That(fixture.Host.Calls.Count, Is.EqualTo(callCount));
            Assert.That(fixture.Host.ResetHandlesCount, Is.EqualTo(1));
            Assert.That(fixture.Host.HasActiveSessionResources, Is.False);
        }

        [Test]
        public void DestroyBattleWorlds_WhenRemoteDestroyFails_StillDestroysConfirmedAndPropagates()
        {
            var calls = new List<string>();
            var failure = new InvalidOperationException("remote destroy failed");

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                SessionSimRuntimeDisposer.DestroyBattleWorlds(
                    () =>
                    {
                        calls.Add("remote");
                        throw failure;
                    },
                    () => calls.Add("confirmed")));

            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(calls, Is.EqualTo(new[] { "remote", "confirmed" }));
        }

        [Test]
        public void DestroyBattleWorlds_WhenBothDestroyOperationsFail_AggregatesBothFailures()
        {
            var calls = new List<string>();

            var thrown = Assert.Throws<AggregateException>(() =>
                SessionSimRuntimeDisposer.DestroyBattleWorlds(
                    () =>
                    {
                        calls.Add("remote");
                        throw new InvalidOperationException("remote destroy failed");
                    },
                    () =>
                    {
                        calls.Add("confirmed");
                        throw new InvalidOperationException("confirmed destroy failed");
                    }));

            Assert.That(calls, Is.EqualTo(new[] { "remote", "confirmed" }));
            Assert.That(thrown.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(thrown.InnerExceptions[0].Message, Is.EqualTo("remote destroy failed"));
            Assert.That(thrown.InnerExceptions[1].Message, Is.EqualTo("confirmed destroy failed"));
        }

        [Test]
        public void ExecuteCleanupSteps_WhenMiddleStepFails_ContinuesAndPropagatesOriginalFailure()
        {
            var calls = new List<string>();
            var failure = new InvalidOperationException("middle failed");

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                    "cleanup failed",
                    () => calls.Add("first"),
                    () =>
                    {
                        calls.Add("second");
                        throw failure;
                    },
                    () => calls.Add("third")));

            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(calls, Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void ExecuteCleanupSteps_WhenMultipleStepsFail_AggregatesInExecutionOrder()
        {
            var calls = new List<string>();

            var thrown = Assert.Throws<AggregateException>(() =>
                SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                    "cleanup failed",
                    () =>
                    {
                        calls.Add("first");
                        throw new InvalidOperationException("first failed");
                    },
                    () => calls.Add("second"),
                    () =>
                    {
                        calls.Add("third");
                        throw new InvalidOperationException("third failed");
                    }));

            Assert.That(calls, Is.EqualTo(new[] { "first", "second", "third" }));
            Assert.That(thrown.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(thrown.InnerExceptions[0].Message, Is.EqualTo("first failed"));
            Assert.That(thrown.InnerExceptions[1].Message, Is.EqualTo("third failed"));
        }

        [Test]
        public void ResetSessionResources_PreservesAttachmentOwnedPhaseAndGatewayBindings()
        {
            var handles = new BattleSessionHandles();
            var context = new BattleContext();
            var owner = new object();
            handles.Phase.Ctx = context;
            handles.GatewayRoom.ConnectionOwner = owner;

            handles.ResetSessionResources();

            Assert.That(handles.Phase.Ctx, Is.SameAs(context));
            Assert.That(handles.GatewayRoom.ConnectionOwner, Is.SameAs(owner));
        }

        private static GatewaySessionRuntime CreateGatewayOwner(
            BattleSessionHandles handles,
            TrackingConnectionRegistry registry,
            TrackingGatewayConnectionFactory connectionFactory,
            TrackingGatewayRoomClientFactory clientFactory)
        {
            return new GatewaySessionRuntime(
                handles,
                registry,
                connectionFactory,
                clientFactory,
                new NetworkConditionController());
        }

        private static Fixture CreateFixture()
        {
            var state = new BattleSessionState();
            var handles = new BattleSessionHandles();
            var host = new FailureInjectingHost(CreatePlan());
            return new Fixture(state, host, new SessionOrchestrator(state, handles, host));
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
                hostMode: BattleStartConfig.BattleHostMode.GatewayRemote,
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
                runMode: BattleStartConfig.BattleRunMode.Normal,
                createWorldOpCode: 0,
                createWorldPayload: null,
                timeSyncIntervalMs: timeSyncIntervalMs);
        }

        private static async Task AwaitWithTimeoutAsync(Task task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(5000));
            if (!ReferenceEquals(completed, task))
            {
                throw new TimeoutException("Asynchronous gateway lifecycle test timed out.");
            }

            await task;
        }

        private static async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task)
        {
            await AwaitWithTimeoutAsync((Task)task);
            return await task;
        }

        private static async Task<bool> IsCanceledAsync(Task task)
        {
            try
            {
                await AwaitWithTimeoutAsync(task);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        private readonly struct Fixture
        {
            public readonly BattleSessionState State;
            public readonly FailureInjectingHost Host;
            public readonly SessionOrchestrator Orchestrator;

            public Fixture(BattleSessionState state, FailureInjectingHost host, SessionOrchestrator orchestrator)
            {
                State = state;
                Host = host;
                Orchestrator = orchestrator;
            }
        }

        private sealed class InlineDispatcher : IDispatcher
        {
            public void Post(Action action) => action?.Invoke();
        }

        private sealed class TrackingGatewayConnectionFactory : IBattleSessionGatewayConnectionFactory
        {
            public List<TrackingConnection> Connections { get; } = new List<TrackingConnection>();

            public IConnection CreateGatewayRoomConnection(
                BattleStartPlan plan,
                IDispatcher callbackDispatcher,
                IDispatcher ioDispatcher)
            {
                var connection = new TrackingConnection();
                Connections.Add(connection);
                return connection;
            }
        }

        private sealed class ControllableGatewayRoomClient : IGatewayRoomClient
        {
            private readonly object _gate = new object();
            private readonly Queue<TimeSyncRequest> _timeSyncRequests = new Queue<TimeSyncRequest>();
            private readonly SemaphoreSlim _timeSyncRequestAvailable = new SemaphoreSlim(0);

            public TaskCompletionSource<string> GuestLoginCompletion { get; } =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<GatewayCreateRoomResult> CreateRoomCompletion { get; } =
                new TaskCompletionSource<GatewayCreateRoomResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<GatewayJoinRoomResult> JoinRoomCompletion { get; } =
                new TaskCompletionSource<GatewayJoinRoomResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            public List<string> Calls { get; } = new List<string>();
            public CancellationToken GuestLoginToken { get; private set; }
            public CancellationToken CreateRoomToken { get; private set; }
            public CancellationToken JoinRoomToken { get; private set; }
            public Exception AutomaticTimeSyncFailure { get; set; }
            public int TimeSyncCallCount { get; private set; }

            public Task<GatewayTimeSyncResult> TimeSyncAsync(
                uint timeSyncOpCode,
                long clientSendTicks,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    Calls.Add("time-sync");
                    TimeSyncCallCount++;
                }

                if (AutomaticTimeSyncFailure != null)
                {
                    return System.Threading.Tasks.Task.FromException<GatewayTimeSyncResult>(
                        AutomaticTimeSyncFailure);
                }

                var request = new TimeSyncRequest(clientSendTicks, cancellationToken);
                lock (_gate) _timeSyncRequests.Enqueue(request);
                _timeSyncRequestAvailable.Release();
                return request.Completion.Task;
            }

            public async Task<TimeSyncRequest> WaitForTimeSyncRequestAsync()
            {
                if (!await _timeSyncRequestAvailable.WaitAsync(5000))
                {
                    throw new TimeoutException("Timed out waiting for a gateway time sync request.");
                }

                lock (_gate) return _timeSyncRequests.Dequeue();
            }

            public Task<string> GuestLoginAsync(
                uint guestLoginOpCode,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                lock (_gate) Calls.Add("guest-login");
                GuestLoginToken = cancellationToken;
                return GuestLoginCompletion.Task;
            }

            public Task<GatewayCreateRoomResult> CreateRoomAsync(
                string sessionToken,
                string region,
                string serverId,
                string roomType,
                string title,
                bool isPublic,
                int maxPlayers,
                IReadOnlyDictionary<string, string> tags,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                lock (_gate) Calls.Add("create-room");
                CreateRoomToken = cancellationToken;
                return CreateRoomCompletion.Task;
            }

            public Task<GatewayJoinRoomResult> JoinRoomAsync(
                string sessionToken,
                string region,
                string serverId,
                string roomId,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                lock (_gate) Calls.Add("join-room");
                JoinRoomToken = cancellationToken;
                return JoinRoomCompletion.Task;
            }

            public Task<GatewayRoomSnapshotResult> SetReadyAsync(
                string sessionToken, string roomId, bool ready, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<GatewayRoomSnapshotResult> PickHeroAsync(
                string sessionToken, string roomId, int heroId, int teamId, int spawnPointId,
                int level, int attributeTemplateId, int basicAttackSkillId,
                IReadOnlyList<int> skillIds, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> BeginLoadingAsync(
                string sessionToken, string roomId, long? expectedRevision, string commandId,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> ReportAssetsLoadedAsync(
                string sessionToken, string roomId, long launchGeneration, int manifestVersion,
                string manifestHash, string commandId, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> LeaveRoomAsync(
                string sessionToken, string roomId, long? expectedRevision, string commandId,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> ReportLoadingProgressAsync(
                string sessionToken, string roomId, long launchGeneration, int manifestVersion,
                string manifestHash, int progress, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<GatewayRoomOperationResult> CancelLoadingAsync(
                string sessionToken, string roomId, long? expectedRevision, string commandId,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<GatewayGetSnapshotResult> GetSnapshotAsync(
                string sessionToken, string roomId, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public Task<GatewayRestoreRoomResult> RestoreRoomAsync(
                string sessionToken, string region, string serverId, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public ClientRoomSnapshot DeserializeRoomStateChangedPush(ArraySegment<byte> payload) =>
                throw new NotSupportedException();

            public bool IsRoomStateChangedPush(uint opCode) => false;

            internal sealed class TimeSyncRequest
            {
                internal TimeSyncRequest(long clientSendTicks, CancellationToken token)
                {
                    ClientSendTicks = clientSendTicks;
                    Token = token;
                    Completion = new TaskCompletionSource<GatewayTimeSyncResult>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }

                internal long ClientSendTicks { get; }
                internal CancellationToken Token { get; }
                internal TaskCompletionSource<GatewayTimeSyncResult> Completion { get; }
            }
        }

        private sealed class TrackingGatewayRoomClientFactory : IBattleSessionGatewayRoomClientFactory
        {
            public bool FailOnCreate { get; set; }
            public IGatewayRoomClient Client { get; set; }
            public IGatewayRoomClient LastClient { get; private set; }

            public IGatewayRoomClient CreateGatewayRoomClient(
                IConnection connection,
                GatewayRoomOpCodes opCodes)
            {
                if (FailOnCreate)
                {
                    throw new InvalidOperationException("Injected gateway client creation failure.");
                }

                LastClient = Client ?? new GatewayRoomClient(connection, opCodes);
                return LastClient;
            }
        }

        private sealed class TrackingConnectionRegistry : IAbilityKitConnectionRegistry
        {
            private IConnection _connection;

            public int RemoveCount { get; private set; }

            public bool TryGet(AbilityKitConnectionRole role, out IConnection connection)
            {
                connection = _connection;
                return connection != null;
            }

            public IConnection GetRequired(AbilityKitConnectionRole role)
            {
                return _connection ?? throw new InvalidOperationException("Connection is not registered.");
            }

            public void Register(
                AbilityKitConnectionRole role,
                IConnection connection,
                bool disposeOnReplace = true)
            {
                if (disposeOnReplace && _connection != null && !ReferenceEquals(_connection, connection))
                {
                    _connection.Dispose();
                }

                _connection = connection;
            }

            public IConnection GetOrCreate(
                AbilityKitConnectionDescriptor descriptor,
                Func<AbilityKitConnectionDescriptor, IConnection> factory)
            {
                if (_connection == null)
                {
                    _connection = factory(descriptor);
                }

                return _connection;
            }

            public bool Remove(AbilityKitConnectionRole role, bool dispose = true)
            {
                if (_connection == null)
                {
                    return false;
                }

                var connection = _connection;
                _connection = null;
                RemoveCount++;
                if (dispose)
                {
                    connection.Dispose();
                }

                return true;
            }

            public void Dispose()
            {
                Remove(AbilityKitConnectionRole.GatewayReliable);
            }
        }

        private sealed class TrackingConnection : IConnection
        {
            public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
            public bool IsConnected => State == ConnectionState.Connected;
            public int OpenCount { get; private set; }
            public int TickCount { get; private set; }
            public int DisposeCount { get; private set; }
            public string LastHost { get; private set; }
            public int LastPort { get; private set; }
            public float LastDeltaTime { get; private set; }

            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
            public event Action<uint, ArraySegment<byte>> ServerPushReceived;
            public event Action<string, string> Kicked;

            public void Open(string host, int port)
            {
                LastHost = host;
                LastPort = port;
                OpenCount++;
                State = ConnectionState.Connected;
                Connected?.Invoke();
            }

            public void Close()
            {
                State = ConnectionState.Disconnected;
                Disconnected?.Invoke();
            }

            public void Tick(float deltaTime)
            {
                LastDeltaTime = deltaTime;
                TickCount++;
            }

            public void Send(
                uint opCode,
                ArraySegment<byte> payload,
                ushort flags = 0,
                uint seq = 0)
            {
            }

            public void Dispose()
            {
                DisposeCount++;
                Close();
            }
        }

        private sealed class FailureInjectingHost : ISessionOrchestratorHost
        {
            private readonly Dictionary<string, int> _failures = new Dictionary<string, int>();

            public FailureInjectingHost(BattleStartPlan plan)
            {
                Plan = plan;
            }

            public BattleStartPlan Plan { get; }
            public BattleContext Context { get; } = new BattleContext();
            public List<string> Calls { get; } = new List<string>();
            public List<string> CleanupCalls { get; } = new List<string>();
            public bool LogicSessionActive { get; private set; }
            public bool FrameSubscriptionActive { get; private set; }
            public bool RemoteWorldActive { get; private set; }
            public bool ConfirmedWorldActive { get; private set; }
            public bool PipelineActive { get; private set; }
            public bool ReplayActive { get; private set; }
            public int ResetHandlesCount { get; private set; }

            public bool HasActiveSessionResources =>
                LogicSessionActive || FrameSubscriptionActive || RemoteWorldActive ||
                ConfirmedWorldActive || PipelineActive || ReplayActive;

            public void FailNext(string step)
            {
                _failures[step] = 1;
            }

            public int CountCalls(string step)
            {
                var count = 0;
                for (var i = 0; i < Calls.Count; i++)
                {
                    if (Calls[i] == step) count++;
                }
                return count;
            }

            public void StartBattleLogicSession(BattleLogicSessionOptions opts)
            {
                LogicSessionActive = true;
                Call("start-logic");
            }

            public void SubscribeFrameReceived()
            {
                FrameSubscriptionActive = true;
                Call("subscribe");
            }

            public void UnsubscribeFrameReceived()
            {
                CleanupCall("unsubscribe");
                FrameSubscriptionActive = false;
            }

            public void StopBattleLogicSession()
            {
                CleanupCall("stop-logic");
                LogicSessionActive = false;
            }

            public void InvokeSessionStartingPipeline()
            {
                PipelineActive = true;
                Call("pipeline-start");
            }

            public void InvokeSessionStoppingPipeline()
            {
                CleanupCall("pipeline-stop");
                PipelineActive = false;
            }

            public void InvokeReplaySetupPipeline()
            {
                ReplayActive = true;
                Call("replay-setup");
            }

            public void StartRemoteDrivenLocalWorld()
            {
                RemoteWorldActive = true;
                Call("start-remote");
            }

            public void StartConfirmedAuthorityWorld()
            {
                ConfirmedWorldActive = true;
                Call("start-confirmed");
            }

            public void TryDestroyBattleWorlds() => CleanupCall("destroy-worlds");
            public void DisposeSnapshotRouting() => CleanupCall("snapshot-routing");
            public void DisposeConfirmedView() => CleanupCall("confirmed-view");

            public void DisposeRemoteDrivenWorld()
            {
                CleanupCall("remote-world");
                RemoteWorldActive = false;
            }

            public void DisposeConfirmedWorld()
            {
                CleanupCall("confirmed-world");
                ConfirmedWorldActive = false;
            }

            public void DisposeRemoteInterpolation() => CleanupCall("remote-interpolation");

            public void ResetSessionHandles()
            {
                CleanupCall("reset-handles");
                ResetHandlesCount++;
                ReplayActive = false;
                LogicSessionActive = false;
                FrameSubscriptionActive = false;
                RemoteWorldActive = false;
                ConfirmedWorldActive = false;
                PipelineActive = false;
            }

            private void CleanupCall(string step)
            {
                CleanupCalls.Add(step);
                Call(step);
            }

            private void Call(string step)
            {
                Calls.Add(step);
                if (!_failures.TryGetValue(step, out var remaining) || remaining <= 0) return;

                _failures[step] = remaining - 1;
                throw new InvalidOperationException($"Injected failure: {step}");
            }
        }
    }
}
