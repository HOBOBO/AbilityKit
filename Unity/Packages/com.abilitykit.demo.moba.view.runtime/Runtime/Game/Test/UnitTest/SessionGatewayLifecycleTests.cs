using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
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
using AbilityKit.Network.Sdk;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SessionGatewayLifecycleTests
    {
        [Test]
        public void GatewayRoomClient_ComposesNarrowCapabilitiesAndDisposalOwnership()
        {
            var client = new ControllableGatewayRoomClient();

            Assert.That(client, Is.InstanceOf<IGatewayAuthenticationCapability>());
            Assert.That(client, Is.InstanceOf<IGatewayClockCapability>());
            Assert.That(client, Is.InstanceOf<IGatewayRoomCommandCapability>());
            Assert.That(client, Is.InstanceOf<IGatewayRoomRecoveryQueryCapability>());
            Assert.That(client, Is.InstanceOf<IGatewayRoomPushDecodingCapability>());
            Assert.That(client, Is.InstanceOf<IDisposable>());
        }

        [Test]
        public void NarrowGatewayConsumers_DoNotDependOnAggregateClient()
        {
            AssertConstructorDoesNotDependOnAggregateClient(typeof(ClientRoomPushSynchronizer));
            AssertConstructorDoesNotDependOnAggregateClient(typeof(MobaRoomGatewaySessionClient));
            AssertMethodDoesNotDependOnAggregateClient(typeof(GatewayClockSynchronizer), "Start");
        }

        [Test]
        public void GatewayRootServices_PublishesNarrowCapabilitiesAndWithdrawsThem()
        {
            var world = new EntityWorld();
            var root = world.Create("gateway-root-capability-boundary");
            var config = ScriptableObject.CreateInstance<BattleGatewayConfigSO>();
            var store = new ClientRoomStore();
            var client = new ControllableGatewayRoomClient();
            var runtime = new StubGatewayRuntime();
            var gatewaySession = CreateUninitialized<GatewayMultiplayerRoomSession>();
            var snapshotProvider = CreateUninitialized<ClientRoomSnapshotProvider>();
            var controller = CreateUninitialized<MultiplayerRoomFlowController>();
            var pushSynchronizer = CreateUninitialized<ClientRoomPushSynchronizer>();
            var assetLoader = CreateUninitialized<MultiplayerBattleAssetLoader>();
            var services = new MultiplayerGatewayRootServices(
                config,
                null,
                store,
                client,
                client,
                client,
                gatewaySession,
                gatewaySession,
                snapshotProvider,
                controller,
                pushSynchronizer,
                assetLoader,
                runtime,
                runtime);

            try
            {
                services.Publish(root);

                Assert.That(root.TryGetRef<IGatewayRoomCommandCapability>(out var commands), Is.True);
                Assert.That(commands, Is.SameAs(client));
                Assert.That(root.TryGetRef<IGatewayRoomRecoveryQueryCapability>(out var recoveryQuery), Is.True);
                Assert.That(recoveryQuery, Is.SameAs(client));
                Assert.That(root.TryGetRef<IDemoRoomDirectoryClient>(out var directory), Is.True);
                Assert.That(directory, Is.SameAs(client));
                Assert.That(root.TryGetRef<IMultiplayerGatewayDiagnostics>(out var diagnostics), Is.True);
                Assert.That(diagnostics, Is.SameAs(runtime));
                Assert.That(root.TryGetRef<IMultiplayerGatewayRecoveryControl>(out var recoveryControl), Is.True);
                Assert.That(recoveryControl, Is.SameAs(runtime));
                Assert.That(root.TryGetRef<IGatewayRoomClient>(out _), Is.False);
                Assert.That(root.TryGetRef<IMultiplayerGatewayRuntime>(out _), Is.False);

                services.Withdraw();

                Assert.That(root.TryGetRef<IGatewayRoomCommandCapability>(out _), Is.False);
                Assert.That(root.TryGetRef<IGatewayRoomRecoveryQueryCapability>(out _), Is.False);
                Assert.That(root.TryGetRef<IDemoRoomDirectoryClient>(out _), Is.False);
                Assert.That(root.TryGetRef<IMultiplayerGatewayDiagnostics>(out _), Is.False);
                Assert.That(root.TryGetRef<IMultiplayerGatewayRecoveryControl>(out _), Is.False);
            }
            finally
            {
                services.Withdraw();
                UnityEngine.Object.DestroyImmediate(config);
            }
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

            var client = (ControllableGatewayRoomClient)clientFactory.LastClient;
            owner.Dispose();
            owner.Dispose();

            Assert.That(owner.IsBuilt, Is.False);
            Assert.That(handles.GatewayRoom.Conn, Is.Null);
            Assert.That(handles.GatewayRoom.Client, Is.Null);
            Assert.That(handles.GatewayRoom.ConnectionOwner, Is.Null);
            Assert.That(registry.RemoveCount, Is.EqualTo(1));
            Assert.That(client.DisposeCount, Is.EqualTo(1));
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
            var firstClient = (ControllableGatewayRoomClient)owner.Client;

            owner.Build(CreatePlan(), dispatcher, dispatcher);
            var secondConnection = connectionFactory.Connections[1];
            var secondClient = (ControllableGatewayRoomClient)owner.Client;

            Assert.That(firstClient.DisposeCount, Is.EqualTo(1));
            Assert.That(firstConnection.DisposeCount, Is.EqualTo(1));
            Assert.That(owner.Connection, Is.SameAs(secondConnection));
            Assert.That(owner.Client, Is.Not.SameAs(firstClient));
            Assert.That(handles.GatewayRoom.Conn, Is.SameAs(secondConnection));
            Assert.That(registry.RemoveCount, Is.EqualTo(1));

            owner.Dispose();
            Assert.That(secondClient.DisposeCount, Is.EqualTo(1));
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
            var staleClient = (ControllableGatewayRoomClient)staleOwner.Client;
            var activeConnection = new TrackingConnection();
            registry.Register(
                AbilityKitConnectionRole.GatewayReliable,
                activeConnection);
            activeOwner.Build(CreatePlan(), dispatcher, dispatcher);
            var activeClient = (ControllableGatewayRoomClient)activeOwner.Client;
            var activeToken = handles.GatewayRoom.ConnectionOwner;

            staleOwner.Dispose();
            activeOwner.Tick(0.5f);

            Assert.That(staleClient.DisposeCount, Is.EqualTo(1));
            Assert.That(activeClient.DisposeCount, Is.Zero);
            Assert.That(handles.GatewayRoom.ConnectionOwner, Is.SameAs(activeToken));
            Assert.That(handles.GatewayRoom.Conn, Is.SameAs(activeConnection));
            Assert.That(handles.GatewayRoom.Client, Is.SameAs(activeClient));
            Assert.That(registry.GetRequired(AbilityKitConnectionRole.GatewayReliable), Is.SameAs(activeConnection));
            Assert.That(activeConnection.DisposeCount, Is.Zero);
            Assert.That(activeConnection.TickCount, Is.EqualTo(1));
            Assert.That(registry.RemoveCount, Is.Zero);

            activeOwner.Dispose();
            Assert.That(activeClient.DisposeCount, Is.EqualTo(1));
            Assert.That(activeConnection.DisposeCount, Is.EqualTo(1));
            Assert.That(registry.RemoveCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GatewayRoom_CompletePreparation_PreservesSessionDataUntilDispose()
        {
            yield return AwaitTask(GatewayRoom_CompletePreparation_PreservesSessionDataUntilDisposeCore());
        }

        private static async Task GatewayRoom_CompletePreparation_PreservesSessionDataUntilDisposeCore()
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
                new AbilityKit.Ability.World.Abstractions.WorldId("42"),
                out var retainedAnchor), Is.True);
            Assert.That(retainedAnchor.StartFrame, Is.EqualTo(7));

            owner.Dispose();
            owner.Dispose();

            Assert.That(owner.ClockEstimate.HasClockSync, Is.False);
            Assert.That(owner.WorldStartAnchors, Is.Empty);
            Assert.That(connectionFactory.Connections[0].DisposeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GatewayPreparation_Success_PublishesTokenRoomAnchorAndCallOrder()
        {
            yield return AwaitTask(GatewayPreparation_Success_PublishesTokenRoomAnchorAndCallOrderCore());
        }

        private static async Task GatewayPreparation_Success_PublishesTokenRoomAnchorAndCallOrderCore()
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
                client,
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
            Assert.That(publishedPlan.World.WorldId, Is.EqualTo("42"));
            Assert.That(runtime.TryGetWorldStartAnchor(
                new AbilityKit.Ability.World.Abstractions.WorldId("42"),
                out var publishedAnchor), Is.True);
            Assert.That(publishedAnchor.StartFrame, Is.EqualTo(7));

            runtime.Dispose();
        }

        [UnityTest]
        public IEnumerator GatewayPreparation_StopWork_CancelsAndRejectsLateLoginCompletion()
        {
            yield return AwaitTask(GatewayPreparation_StopWork_CancelsAndRejectsLateLoginCompletionCore());
        }

        private static async Task GatewayPreparation_StopWork_CancelsAndRejectsLateLoginCompletionCore()
        {
            var runtime = new GatewayPreparationRuntime(new GatewayClockSynchronizer());
            var connection = new TrackingConnection();
            var client = new ControllableGatewayRoomClient();
            var publishCount = 0;
            connection.Open("127.0.0.1", 4000);

            runtime.Start(
                connection,
                client,
                client,
                client,
                CreatePlan(gatewaySessionToken: string.Empty),
                _ => publishCount++,
                null,
                null);
            var task = runtime.Task;
            var stopTask = runtime.StopWorkAsync();
            Assert.That(stopTask.IsCompleted, Is.False);
            client.GuestLoginCompletion.SetResult("late-token");

            await AwaitWithTimeoutAsync(stopTask);
            Assert.That(await IsCanceledAsync(task), Is.True);
            Assert.That(client.GuestLoginToken.IsCancellationRequested, Is.True);
            Assert.That(publishCount, Is.Zero);
            Assert.That(runtime.Task, Is.Null);

            runtime.Dispose();
        }

        [UnityTest]
        public IEnumerator GatewayPreparation_RepeatedStart_RejectsPreviousGenerationCompletion()
        {
            yield return AwaitTask(GatewayPreparation_RepeatedStart_RejectsPreviousGenerationCompletionCore());
        }

        private static async Task GatewayPreparation_RepeatedStart_RejectsPreviousGenerationCompletionCore()
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
                staleClient,
                staleClient,
                CreatePlan(gatewaySessionToken: string.Empty),
                plan => publishedTokens.Add(plan.Gateway.SessionToken),
                null,
                null);
            var staleTask = runtime.Task;
            runtime.Start(
                connection,
                activeClient,
                activeClient,
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

        [UnityTest]
        public IEnumerator GatewayClock_FirstSamplePublishesAndStopPreservesEstimateUntilClear()
        {
            yield return AwaitTask(GatewayClock_FirstSamplePublishesAndStopPreservesEstimateUntilClearCore());
        }

        private static async Task GatewayClock_FirstSamplePublishesAndStopPreservesEstimateUntilClearCore()
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
            var stopTask = runtime.StopWorkAsync();

            Assert.That(estimate.HasClockSync, Is.True);
            Assert.That(estimate.Samples, Is.EqualTo(1));
            await AwaitWithTimeoutAsync(stopTask);
            Assert.That(runtime.Estimate.HasClockSync, Is.True);
            runtime.ClearEstimate();
            Assert.That(runtime.Estimate.HasClockSync, Is.False);

            runtime.Dispose();
        }

        [UnityTest]
        public IEnumerator GatewayClock_NotifiesAtFailureThresholdAndRejectsStaleGeneration()
        {
            yield return AwaitTask(GatewayClock_NotifiesAtFailureThresholdAndRejectsStaleGenerationCore());
        }

        [UnityTest]
        public IEnumerator GatewayClock_StopWorkAsync_WaitsForPendingRequest()
        {
            yield return AwaitTask(GatewayClock_StopWorkAsync_WaitsForPendingRequestCore());
        }

        private static async Task GatewayClock_StopWorkAsync_WaitsForPendingRequestCore()
        {
            var runtime = new GatewayClockSynchronizer();
            var client = new ControllableGatewayRoomClient();
            var options = CreatePlan(timeSyncIntervalMs: 60000).TimeSync;

            runtime.Start(client, in options, null, null);
            var request = await client.WaitForTimeSyncRequestAsync();
            var cancellationReenteredOwner = false;
            using (request.Token.Register(() =>
                   {
                       var reentry = System.Threading.Tasks.Task.Run(() => runtime.Task);
                       cancellationReenteredOwner = reentry.Wait(1000);
                   }))
            {
                var firstStopTask = runtime.StopWorkAsync();
                var secondStopTask = runtime.StopWorkAsync();

                Assert.That(firstStopTask.IsCompleted, Is.False);
                Assert.That(secondStopTask.IsCompleted, Is.False);
                Assert.That(request.Token.IsCancellationRequested, Is.True);
                Assert.That(cancellationReenteredOwner, Is.True);

                request.Completion.SetResult(new GatewayTimeSyncResult(
                    request.ClientSendTicks,
                    request.ClientSendTicks,
                    System.Diagnostics.Stopwatch.Frequency));
                await AwaitWithTimeoutAsync(firstStopTask);
                await AwaitWithTimeoutAsync(secondStopTask);
            }

            Assert.That(runtime.Task, Is.Null);
            runtime.Dispose();
        }

        private static async Task GatewayClock_NotifiesAtFailureThresholdAndRejectsStaleGenerationCore()
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

        private static void AssertConstructorDoesNotDependOnAggregateClient(Type type)
        {
            foreach (var constructor in type.GetConstructors(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    Assert.That(
                        parameter.ParameterType,
                        Is.Not.EqualTo(typeof(IGatewayRoomClient)),
                        $"{type.Name} constructor parameter '{parameter.Name}' must use a narrow gateway capability.");
                }
            }
        }

        private static void AssertMethodDoesNotDependOnAggregateClient(Type type, string methodName)
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) continue;
                foreach (var parameter in method.GetParameters())
                {
                    Assert.That(
                        parameter.ParameterType,
                        Is.Not.EqualTo(typeof(IGatewayRoomClient)),
                        $"{type.Name}.{methodName} parameter '{parameter.Name}' must use a narrow gateway capability.");
                }
            }
        }

        private static IEnumerator AwaitTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.Exception.GetBaseException()).Throw();
            }

            if (task.IsCanceled)
            {
                throw new OperationCanceledException();
            }
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

        private static T CreateUninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        private sealed class StubGatewayRuntime : IMultiplayerGatewayRuntime
        {
            public bool IsRemoteActive => false;
            public ConnectionState ConnectionState => ConnectionState.Disconnected;
            public MultiplayerRecoveryState RecoveryState => MultiplayerRecoveryState.None;
            public NetworkSessionRecoveryDecision RecoveryDecision => default;
            public NetworkSessionRecoveryDiagnostics RecoveryDiagnostics => default;
            public SessionLifecycleDiagnosticsSnapshot LifecycleDiagnostics => default;

            public void ResetReconnect()
            {
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

        private sealed class ControllableGatewayRoomClient :
            IGatewayRoomClient,
            IDemoRoomDirectoryClient
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
            public int DisposeCount { get; private set; }

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

            public Task<DemoRoomDirectoryResult> ListRoomsAsync(
                DemoRoomDirectoryQuery query,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public ClientRoomSnapshot DeserializeRoomStateChangedPush(ArraySegment<byte> payload) =>
                throw new NotSupportedException();

            public bool IsRoomStateChangedPush(uint opCode) => false;

            public void Dispose()
            {
                DisposeCount++;
            }

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

                LastClient = Client ?? new ControllableGatewayRoomClient();
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
    }
}
