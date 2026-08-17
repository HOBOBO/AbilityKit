using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Flow.Tests
{
    public sealed class FormalLobbyRuntimeTests
    {
        [UnityTest]
        public IEnumerator DetachReattach_RejectsPreviousOperationCompletion()
        {
            var runtime = new FormalLobbyRuntime();
            var firstCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstExited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            runtime.Attach();
            runtime.StartOperation("first", async _ =>
            {
                try
                {
                    await firstCompletion.Task;
                }
                finally
                {
                    firstExited.TrySetResult(true);
                }
            });

            runtime.Detach();
            runtime.Attach();
            runtime.StartOperation("second", _ => secondCompletion.Task);

            firstCompletion.SetException(new InvalidOperationException("stale failure"));
            for (var i = 0; i < 20 && !firstExited.Task.IsCompleted; i++) yield return null;
            yield return null;

            Assert.That(firstExited.Task.IsCompleted, Is.True);
            Assert.That(runtime.IsOperationBusy, Is.True);
            Assert.That(runtime.OperationLabel, Is.EqualTo("second"));
            Assert.That(runtime.OperationError, Is.Empty);

            secondCompletion.SetResult(true);
            for (var i = 0; i < 20 && runtime.IsOperationBusy; i++) yield return null;

            Assert.That(runtime.IsOperationBusy, Is.False);
            Assert.That(runtime.OperationLabel, Is.Empty);
            runtime.Dispose();
        }

        [UnityTest]
        public IEnumerator CancelLifetime_RejectsLateOperationCompletion()
        {
            var runtime = new FormalLobbyRuntime();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            runtime.Attach();
            runtime.StartOperation("exiting", async _ =>
            {
                try
                {
                    await completion.Task;
                }
                finally
                {
                    exited.TrySetResult(true);
                }
            });

            runtime.CancelLifetime();

            Assert.That(runtime.IsAttached, Is.False);
            Assert.That(runtime.IsOperationBusy, Is.False);
            Assert.That(runtime.OperationLabel, Is.Empty);
            Assert.That(runtime.OperationError, Is.Empty);

            completion.SetException(new InvalidOperationException("late failure"));
            for (var i = 0; i < 20 && !exited.Task.IsCompleted; i++) yield return null;
            yield return null;

            Assert.That(exited.Task.IsCompleted, Is.True);
            Assert.That(runtime.IsOperationBusy, Is.False);
            Assert.That(runtime.OperationLabel, Is.Empty);
            Assert.That(runtime.OperationError, Is.Empty);
            runtime.Dispose();
        }

        [Test]
        public void AutomationPolicy_CreateAndRefreshPreserveLobbyOrderingGates()
        {
            Assert.That(
                LobbyAutomationPolicy.ShouldRefreshDirectory(
                    MultiplayerRoomFlowState.Idle,
                    connected: true,
                    operationBusy: false,
                    directoryBusy: false,
                    lastRefreshUnixMs: 1000L,
                    nowUnixMs: 4000L,
                    intervalMilliseconds: 3000L),
                Is.True);
            Assert.That(
                LobbyAutomationPolicy.ShouldCreateRoom(
                    enabled: true,
                    MultiplayerRoomFlowState.Idle,
                    connected: true,
                    operationBusy: false,
                    directoryLoaded: true,
                    openRoomCount: 0,
                    attempted: false),
                Is.True);
            Assert.That(
                LobbyAutomationPolicy.ShouldCreateRoom(
                    enabled: true,
                    MultiplayerRoomFlowState.Idle,
                    connected: true,
                    operationBusy: false,
                    directoryLoaded: false,
                    openRoomCount: 0,
                    attempted: false),
                Is.False,
                "Automatic create must wait until the directory refresh publishes its result.");
        }

        [Test]
        public void BattleEntryCoordinator_AcceptsOnceAndReopensAfterEntryFailure()
        {
            var coordinator = new LobbyBattleEntryCoordinator();
            var snapshot = NewBattleSnapshot();
            var entered = 0;
            coordinator.Attach();

            Assert.Throws<InvalidOperationException>(() => coordinator.TryEnter(
                MultiplayerRoomFlowState.InBattle,
                snapshot,
                () => throw new InvalidOperationException("entry failed")));
            Assert.That(coordinator.TryEnter(
                MultiplayerRoomFlowState.InBattle,
                snapshot,
                () => entered++), Is.True);
            Assert.That(coordinator.TryEnter(
                MultiplayerRoomFlowState.InBattle,
                snapshot,
                () => entered++), Is.False);
            Assert.That(entered, Is.EqualTo(1));
        }

        [Test]
        public void BattleEntryGate_WaitsForAuthoritativeSyncCapabilitiesWithoutConsumingGeneration()
        {
            var gate = new MultiplayerBattleEntryGate();
            var snapshot = NewBattleSnapshot();
            snapshot.SyncCapabilities = null;

            Assert.That(
                MultiplayerBattleEntryGate.CanEnter(
                    MultiplayerRoomFlowState.InBattle,
                    snapshot),
                Is.False);
            Assert.That(
                gate.TryAccept(MultiplayerRoomFlowState.InBattle, snapshot),
                Is.False);

            snapshot.SyncCapabilities = NewStateSyncCapabilities();

            Assert.That(
                MultiplayerBattleEntryGate.CanEnter(
                    MultiplayerRoomFlowState.InBattle,
                    snapshot),
                Is.True);
            Assert.That(
                gate.TryAccept(MultiplayerRoomFlowState.InBattle, snapshot),
                Is.True);
            Assert.That(
                gate.TryAccept(MultiplayerRoomFlowState.InBattle, snapshot),
                Is.False);
        }

        [Test]
        public void RoomStoreSubscription_ReattachDoesNotDuplicateAndDetachStopsPublication()
        {
            var store = new ClientRoomStore();
            var subscription = new LobbyRoomStoreSubscription();
            var snapshots = 0;
            Action<ClientRoomSnapshot> onSnapshot = _ => snapshots++;

            subscription.Attach(store, onSnapshot, null, null);
            store.ApplySnapshot(NewClientSnapshot(1));
            subscription.Attach(store, onSnapshot, null, null);

            Assert.That(snapshots, Is.EqualTo(2),
                "Reattach forwards the current snapshot exactly once.");

            store.ApplySnapshot(NewClientSnapshot(2));
            Assert.That(snapshots, Is.EqualTo(3),
                "Only the active subscription may publish updates.");

            subscription.Detach();
            subscription.Detach();
            store.ApplySnapshot(NewClientSnapshot(3));
            Assert.That(snapshots, Is.EqualTo(3));
        }

        [Test]
        public void SceneExitLifecycle_CommitsCleanupInOrderAndNormalizesSceneName()
        {
            var calls = new List<string>();
            var lifecycle = new LobbySceneExitLifecycle(
                () => calls.Add("destroy"),
                sceneName => calls.Add("scene:" + sceneName));

            lifecycle.Exit(
                () => calls.Add("lifetime"),
                () => calls.Add("controller"),
                () => calls.Add("selection"),
                "  Starter  ");

            Assert.That(calls, Is.EqualTo(new[]
            {
                "lifetime",
                "controller",
                "selection",
                "destroy",
                "scene:Starter"
            }));

            calls.Clear();
            lifecycle.Exit(() => calls.Add("lifetime"), null, null, null);
            Assert.That(calls, Is.EqualTo(new[]
            {
                "lifetime",
                "destroy",
                "scene:MultiplayerStarterScene"
            }));
        }

        [UnityTest]
        public IEnumerator RoomDirectoryRuntime_DetachRejectsLateRefreshPublication()
        {
            var directory = new DeferredRoomDirectory();
            var runtime = new LobbyRoomDirectoryRuntime();
            runtime.Attach();
            var refresh = runtime.RefreshAsync(
                directory,
                new DemoRoomDirectoryQuery("session", "region", "server", "moba"),
                timeout: null,
                new LobbyOperationContext(1, 1, CancellationToken.None),
                _ => true);

            Assert.That(runtime.IsBusy, Is.True);
            runtime.Detach();
            runtime.Attach();
            directory.Complete(new DemoRoomDirectoryResult(
                success: true,
                new[]
                {
                    new DemoRoomSummary(
                        "region",
                        "server",
                        "stale-room",
                        "moba",
                        "Stale Room",
                        isPublic: true,
                        maxPlayers: 2,
                        playerCount: 1,
                        "owner",
                        createdAtUnixMs: 1L)
                },
                nextOffset: 0,
                message: string.Empty));
            for (var i = 0; i < 20 && !refresh.IsCompleted; i++) yield return null;

            Assert.That(refresh.IsCompleted, Is.True);
            Assert.That(runtime.IsLoaded, Is.False);
            Assert.That(runtime.Rooms, Is.Empty);
            Assert.That(runtime.IsBusy, Is.False);
        }

        private static MultiplayerRoomSnapshot NewBattleSnapshot()
        {
            return new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                BattleId = "battle-a",
                NumericRoomId = 10UL,
                WorldId = 20UL,
                Phase = MultiplayerRoomPhase.InBattle,
                SyncCapabilities = NewStateSyncCapabilities()
            };
        }

        private static RoomGatewayNetworkSyncCapabilities NewStateSyncCapabilities()
        {
            var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
            return RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(
                new WireNetworkSyncCapabilities
                {
                    MetadataVersion = RoomGatewayNetworkSyncCapabilitiesConverter.CurrentMetadataVersion,
                    ProfileName = "Moba.AuthoritativeRemoteInterpolation",
                    MinimumSchemaVersion = 0,
                    MaximumSchemaVersion = 1,
                    ClientPlayback = (int)profile.ClientPlayback,
                    Input = (int)profile.Input,
                    Snapshot = (int)profile.Snapshot,
                    Interest = (int)profile.Interest,
                    Recovery = (int)profile.Recovery,
                    ServerValidation = (int)profile.ServerValidation,
                    ReliableEvent = (int)profile.ReliableEvent
                });
        }

        private static ClientRoomSnapshot NewClientSnapshot(long revision)
        {
            return new ClientRoomSnapshot
            {
                RoomId = "room-a",
                Phase = ClientRoomPhase.Lobby,
                RoomRevision = revision,
                LastEventSequence = revision
            };
        }

        private sealed class DeferredRoomDirectory : IDemoRoomDirectoryClient
        {
            private readonly TaskCompletionSource<DemoRoomDirectoryResult> _completion =
                new TaskCompletionSource<DemoRoomDirectoryResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<DemoRoomDirectoryResult> ListRoomsAsync(
                DemoRoomDirectoryQuery query,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                return _completion.Task;
            }

            public void Complete(DemoRoomDirectoryResult result)
            {
                _completion.TrySetResult(result);
            }
        }
    }
}
