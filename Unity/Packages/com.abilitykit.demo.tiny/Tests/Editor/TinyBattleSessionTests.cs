using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyBattleSessionTests
    {
        [Test]
        public void RestoredLobbyUsesServerOwnershipAndSnapshot()
        {
            var roomClient = new RoomClientStub();
            var launch = new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
                "owner", "token", TimeSpan.FromSeconds(5));
            using (var session = new TinyBattleSession(launch, new GatewayStub(roomClient)))
            {
                session.RestoreAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(session.RoomId, Is.EqualTo("room-1"));
                Assert.That(session.CanStart, Is.True);
                Assert.That(session.Status, Is.EqualTo("Lobby"));
                session.PollAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(session.CanStart, Is.True);
                Assert.That(session.Status, Is.EqualTo("Lobby"));

                roomClient.OwnerAccountId = "someone-else";
                session.PollAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(session.CanStart, Is.False);
                Assert.That(session.BattleId, Is.Empty);
            }
        }

        [Test]
        public void ReconnectRestoresRoomOncePerConnectionGeneration()
        {
            var roomClient = new RoomClientStub();
            var gateway = new GatewayStub(roomClient);
            var launch = new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
                "owner", "token", TimeSpan.FromSeconds(5));
            using (var session = new TinyBattleSession(launch, gateway))
            {
                session.RestoreAsync(CancellationToken.None).GetAwaiter().GetResult();
                gateway.ConnectionState = RoomGatewayConnectionState.Reconnecting;
                session.Tick(0.02f);
                Assert.That(session.ConnectionUnavailable, Is.True);
                Assert.That(session.Status, Is.EqualTo("Reconnecting"));
                gateway.ConnectionState = RoomGatewayConnectionState.RestoreRequired;
                gateway.ConnectionGeneration = 1;
                Assert.That(session.NeedsConnectionRestore, Is.True);
                session.RecoverConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(roomClient.RestoreCalls, Is.EqualTo(2));
                Assert.That(session.NeedsConnectionRestore, Is.False);
                Assert.That(session.ConnectionUnavailable, Is.False);
                Assert.That(gateway.TickCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public void OldConnectionPollCannotReplaceCurrentRoomSnapshot() => RunAsync(async () =>
        {
            var roomClient = new RoomClientStub();
            var gateway = new GatewayStub(roomClient);
            using (var session = NewSession(gateway))
            {
                await session.RestoreAsync(CancellationToken.None);
                roomClient.PendingSnapshot = new TaskCompletionSource<RoomGatewayGetSnapshotResult>();
                var pending = roomClient.PendingSnapshot;
                var oldPoll = session.PollAsync(CancellationToken.None);
                gateway.ConnectionGeneration++;
                roomClient.OwnerAccountId = "someone-else";
                roomClient.PendingSnapshot = null;
                await session.PollAsync(CancellationToken.None);
                Assert.That(session.CanStart, Is.False);

                pending.SetResult(new RoomGatewayGetSnapshotResult(true, "room-1", 1,
                    new RoomGatewaySnapshot
                    {
                        RoomId = "room-1", OwnerAccountId = "owner",
                        Phase = RoomGatewaySessionPhase.Lobby, CanStart = true
                    }, string.Empty));
                await oldPoll;
                Assert.That(session.CanStart, Is.False);
            }
        });

        [Test]
        public void PollResultDuringReconnectIsDiscardedBeforeGenerationChanges() => RunAsync(async () =>
        {
            var roomClient = new RoomClientStub();
            var gateway = new GatewayStub(roomClient);
            using (var session = NewSession(gateway))
            {
                await session.RestoreAsync(CancellationToken.None);
                var pending = new TaskCompletionSource<RoomGatewayGetSnapshotResult>();
                roomClient.PendingSnapshot = pending;
                var poll = session.PollAsync(CancellationToken.None);
                gateway.ConnectionState = RoomGatewayConnectionState.Reconnecting;
                pending.SetResult(new RoomGatewayGetSnapshotResult(true, "room-1", 1,
                    new RoomGatewaySnapshot
                    {
                        RoomId = "room-1", OwnerAccountId = "someone-else",
                        Phase = RoomGatewaySessionPhase.Lobby
                    }, string.Empty));
                await poll;
                Assert.That(session.CanStart, Is.True);
            }
        });

        [Test]
        public void OlderPollCannotReplaceNewBattleInSameRoom() => RunAsync(async () =>
        {
            var roomClient = new RoomClientStub();
            using (var session = NewSession(new GatewayStub(roomClient)))
            {
                await session.RestoreAsync(CancellationToken.None);
                var pending = new TaskCompletionSource<RoomGatewayGetSnapshotResult>();
                roomClient.PendingSnapshot = pending;
                var oldPoll = session.PollAsync(CancellationToken.None);
                roomClient.PendingSnapshot = null;
                roomClient.NextBattleId = "battle-2";
                await session.PollAsync(CancellationToken.None);
                pending.SetResult(new RoomGatewayGetSnapshotResult(true, "room-1", 1,
                    new RoomGatewaySnapshot
                    {
                        RoomId = "room-1", BattleId = "battle-1", WorldId = 1,
                        Phase = RoomGatewaySessionPhase.Lobby
                    }, string.Empty));
                await oldPoll;
                Assert.That(session.Room.BattleId, Is.EqualTo("battle-2"));
            }
        });

        [Test]
        public void OldBattleSubscriptionCannotReplaceNewBattle() => RunAsync(async () =>
        {
            var roomClient = new RoomClientStub();
            var gateway = new GatewayStub(roomClient);
            using (var session = NewSession(gateway))
            {
                await session.RestoreAsync(CancellationToken.None);
                var pending = new TaskCompletionSource<RoomGatewayStateSyncSubscriptionResult>();
                roomClient.NextSnapshot = BattleSnapshot("battle-1", 1);
                roomClient.PendingSubscription = pending;
                var oldPoll = session.PollAsync(CancellationToken.None);
                roomClient.NextSnapshot = BattleSnapshot("battle-2", 2);
                roomClient.PendingSubscription = null;
                await session.PollAsync(CancellationToken.None);
                pending.SetResult(new RoomGatewayStateSyncSubscriptionResult(true, string.Empty));
                await oldPoll;

                Assert.That(session.BattleId, Is.EqualTo("battle-2"));
                Assert.That(gateway.Requests, Is.EqualTo(1));
                Assert.That(gateway.LastRequest.WorldId, Is.EqualTo(2));
            }
        });

        [Test]
        public void StateBaselineRetriesAfterRejectionAndMissingPush() => RunAsync(async () =>
        {
            var roomClient = new RoomClientStub();
            var gateway = new GatewayStub(roomClient);
            using (var session = NewSession(gateway))
            {
                await session.RestoreAsync(CancellationToken.None);
                roomClient.NextSnapshot = BattleSnapshot("battle-1", 7);
                gateway.FullSnapshotResponses.Enqueue(new WireRequestFullStateSyncRes
                {
                    Success = false, Accepted = false, Message = "retry"
                });
                try
                {
                    await session.PollAsync(CancellationToken.None);
                    Assert.Fail("The rejected baseline request should fail the poll.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.That(exception.Message, Is.EqualTo("retry"));
                }
                Assert.That(session.AwaitingBaseline, Is.True);
                Assert.That(session.NeedsFullSnapshot, Is.True);
                Assert.That(session.CanSubmitInput, Is.False);
                Assert.That(session.SnapshotRequestCount, Is.EqualTo(1));

                await session.RequestFullSnapshotAsync("Baseline retry", CancellationToken.None);
                Assert.That(gateway.Requests, Is.EqualTo(2));
                Assert.That(session.AwaitingBaseline, Is.True);

                gateway.Latest = StateSnapshot(8);
                Assert.That(session.TryGetNewSnapshot(out _), Is.False);
                gateway.Latest = new WireStateSyncSnapshotPush
                {
                    WorldId = 7, Frame = 0, IsFullSnapshot = true
                };
                Assert.That(session.TryGetNewSnapshot(out _), Is.False);
                gateway.Latest = StateSnapshot(7);
                Assert.That(session.TryGetNewSnapshot(out _), Is.True);
                Assert.That(session.AwaitingBaseline, Is.False);
                Assert.That(session.NeedsFullSnapshot, Is.False);
                Assert.That(session.CanSubmitInput, Is.True);
            }
        });

        [TestCase(TinySyncMode.Frame)]
        [TestCase(TinySyncMode.Hybrid)]
        public void FrameBaselineWaitsForMatchingWorld(TinySyncMode mode)
        {
            var gateway = new GatewayStub(new RoomClientStub());
            using (var session = FrameSession(gateway, mode))
            {
                SetPrivate(session, "_frameReplication", new TinyFrameReplication());
                SetPrivate(session, "_awaitingBaseline", true);
                var cursor = (RoomGatewayFullSnapshotCursor)typeof(TinyBattleSession)
                    .GetField("_snapshotCursor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(session);
                cursor.Reset(7);
                Assert.That(session.CanSubmitInput, Is.False);
                Assert.That(session.NeedsFullSnapshot, Is.True);

                session.RequestFullSnapshotAsync("Baseline retry", CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert.That(session.AwaitingBaseline, Is.True);
                gateway.Latest = Snapshot(NewBattle(), 8);
                session.Tick(0.02f);
                Assert.That(session.AwaitingBaseline, Is.True);
                gateway.Latest = Snapshot(NewBattle(), 7);
                session.Tick(0.02f);
                Assert.That(session.AwaitingBaseline, Is.False);
                Assert.That(session.CanSubmitInput, Is.True);
            }
        }

        [Test]
        public void GameplayRootRetriesMissingBaselineWithoutRequestStorm()
        {
            var gateway = new GatewayStub(new RoomClientStub());
            var session = FrameSession(gateway, TinySyncMode.Frame);
            SetPrivate(session, "_frameReplication", new TinyFrameReplication());
            SetPrivate(session, "_awaitingBaseline", true);
            var rootObject = new GameObject("Tiny baseline retry test");
            var root = rootObject.AddComponent<TinyGameplayRoot>();
            var operation = typeof(TinyGameplayRoot).GetField("_operation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                SetRootPrivate(root, "_session", session);
                SetRootPrivate(root, "_lifetime", new CancellationTokenSource());
                SetRootPrivate(root, "_nextPoll", float.PositiveInfinity);
                SetRootPrivate(root, "_nextSnapshotRequest", -1f);
                operation.SetValue(root, Enum.Parse(operation.FieldType, "None"));

                TickRoot(root);
                Assert.That(gateway.Requests, Is.EqualTo(1));
                TickRoot(root);
                Assert.That(gateway.Requests, Is.EqualTo(1));
                Assert.That(session.AwaitingBaseline, Is.True);

                SetRootPrivate(root, "_nextSnapshotRequest", -1f);
                TickRoot(root);
                Assert.That(gateway.Requests, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static void SetRootPrivate(TinyGameplayRoot root, string name, object value) =>
            typeof(TinyGameplayRoot).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(root, value);

        private static void TickRoot(TinyGameplayRoot root) =>
            typeof(TinyGameplayRoot).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(root, null);

        private static WireStateSyncSnapshotPush StateSnapshot(ulong worldId) =>
            new WireStateSyncSnapshotPush
            {
                WorldId = worldId, Frame = 0, IsFullSnapshot = true,
                Actors = new System.Collections.Generic.List<WireStateSyncActorSnapshot>
                {
                    new WireStateSyncActorSnapshot { ActorId = 1, Hp = 100 }
                }
            };

        private static RoomGatewaySnapshot BattleSnapshot(string battleId, ulong worldId)
        {
            var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
            var capabilities = NetworkSyncCapabilities.FromProfile(in profile, 1, 1);
            return new RoomGatewaySnapshot
            {
                RoomId = "room-1", BattleId = battleId, WorldId = worldId,
                Phase = RoomGatewaySessionPhase.InBattle,
                Players = new[] { new RoomGatewayPlayerSnapshot { AccountId = "owner", PlayerId = 1 } },
                SyncCapabilities = RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(
                    new WireNetworkSyncCapabilities
                    {
                        MetadataVersion = 1,
                        ProfileName = nameof(NetworkSyncModel.AuthoritativeInterpolation),
                        MinimumSchemaVersion = capabilities.MinimumSchemaVersion,
                        MaximumSchemaVersion = capabilities.MaximumSchemaVersion,
                        ClientPlayback = (int)capabilities.ClientPlayback,
                        Input = (int)capabilities.Input,
                        Snapshot = (int)capabilities.Snapshot,
                        Interest = (int)capabilities.Interest,
                        Recovery = (int)capabilities.Recovery,
                        ServerValidation = (int)capabilities.ServerValidation,
                        ReliableEvent = (int)capabilities.ReliableEvent
                    })
            };
        }

        [Test]
        public void OldRestoreCannotCompleteNewConnectionGeneration() => RunAsync(async () =>
        {
            var roomClient = new RoomClientStub();
            var gateway = new GatewayStub(roomClient);
            using (var session = NewSession(gateway))
            {
                await session.RestoreAsync(CancellationToken.None);
                gateway.ConnectionState = RoomGatewayConnectionState.RestoreRequired;
                gateway.ConnectionGeneration = 1;
                var pending = new TaskCompletionSource<RoomGatewayRestoreRoomResult>();
                roomClient.PendingRestore = pending;
                var oldRestore = session.RecoverConnectionAsync(CancellationToken.None);
                gateway.ConnectionGeneration = 2;
                roomClient.PendingRestore = null;
                pending.SetResult(roomClient.RestoreResult());
                await oldRestore;
                Assert.That(gateway.ConnectionState, Is.EqualTo(RoomGatewayConnectionState.RestoreRequired));
                Assert.That(session.NeedsConnectionRestore, Is.True);

                await session.RecoverConnectionAsync(CancellationToken.None);
                Assert.That(gateway.ConnectionState, Is.EqualTo(RoomGatewayConnectionState.Connected));
                Assert.That(session.NeedsConnectionRestore, Is.False);
            }
        });

        [Test]
        public void DisposedSessionIgnoresDelayedPoll() => RunAsync(async () =>
        {
            var roomClient = new RoomClientStub();
            var session = NewSession(new GatewayStub(roomClient));
            await session.RestoreAsync(CancellationToken.None);
            roomClient.PendingSnapshot = new TaskCompletionSource<RoomGatewayGetSnapshotResult>();
            var pending = roomClient.PendingSnapshot;
            var poll = session.PollAsync(CancellationToken.None);
            session.Dispose();
            pending.SetResult(new RoomGatewayGetSnapshotResult(true, "room-1", 1,
                new RoomGatewaySnapshot { RoomId = "room-1", Phase = RoomGatewaySessionPhase.Lobby },
                string.Empty));
            await poll;
            Assert.That(session.Status, Is.EqualTo("Lobby"));
            Assert.That(session.CanStart, Is.True);
        });

        private static void RunAsync(Func<Task> action) =>
            Task.Run(action).GetAwaiter().GetResult();

        private static TinyBattleSession NewSession(GatewayStub gateway) =>
            new TinyBattleSession(new DemoMultiplayerLaunchRequest("localhost", 4057,
                "local", "dev", "owner", "token", TimeSpan.FromSeconds(5)), gateway);

        [Test]
        public void FrameOverflowPausesInputUntilCoveringValidSnapshot()
        {
            const ulong worldId = 7;
            var gateway = new GatewayStub(new RoomClientStub());
            var launch = new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
                "owner", "token", TimeSpan.FromSeconds(5));
            using (var session = new TinyBattleSession(launch, gateway))
            {
                SetPrivate(session, "_roomId", "room-1");
                SetPrivate(session, "_battleId", "battle-1");
                SetPrivate(session, "_worldId", worldId);
                SetPrivate(session, "_playerId", 1u);
                SetPrivate(session, "_frameReplication", new TinyFrameReplication());
                var cursor = (RoomGatewayFullSnapshotCursor)typeof(TinyBattleSession)
                    .GetField("_snapshotCursor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(session);
                cursor.Reset(worldId);

                var authority = new TinyBattle();
                authority.AddPlayer(1, -1, 0);
                authority.AddPlayer(2, 1, 0);
                gateway.Latest = Snapshot(authority, worldId);
                session.Tick(0.02f);
                Assert.That(session.CanSubmitInput, Is.True);

                gateway.OverflowMinimumFrame = 3;
                session.Tick(0.02f);
                Assert.That(session.NeedsFullSnapshot, Is.True);
                Assert.That(session.CanSubmitInput, Is.False);
                Assert.That(session.Telemetry.RecoveryRequests, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => session.SubmitInputAsync(
                    new TinyInput(1, 0, false), CancellationToken.None).GetAwaiter().GetResult());
                session.RequestFullSnapshotAsync("Frame overflow", CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert.That(gateway.Requests, Is.EqualTo(1));
                Assert.That(gateway.LastRequest.WorldId, Is.EqualTo(worldId));
                Assert.That(gateway.LastRequest.ClientFrame, Is.EqualTo(0));

                authority.Tick();
                authority.Tick();
                var stale = Snapshot(authority, worldId);
                gateway.Latest = stale;
                session.Tick(0.02f);
                Assert.That(cursor.LastFrame, Is.EqualTo(0));
                Assert.That(session.CanSubmitInput, Is.False);

                authority.Tick();
                var invalid = Snapshot(authority, worldId);
                invalid.Payload = new byte[] { 1 };
                gateway.Latest = invalid;
                session.Tick(0.02f);
                Assert.That(cursor.LastFrame, Is.EqualTo(0));
                Assert.That(session.CanSubmitInput, Is.False);

                gateway.Latest = Snapshot(authority, worldId);
                session.Tick(0.02f);
                Assert.That(cursor.LastFrame, Is.EqualTo(3));
                Assert.That(session.CanSubmitInput, Is.True);
                Assert.That(gateway.ResumeCalls, Is.EqualTo(2));
                session.SubmitInputAsync(new TinyInput(1, 0, false), CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert.That(gateway.Submissions, Is.EqualTo(1));
            }
        }

        private static WireStateSyncSnapshotPush Snapshot(TinyBattle battle, ulong worldId) =>
            new WireStateSyncSnapshotPush
            {
                WorldId = worldId,
                Frame = battle.Frame,
                IsFullSnapshot = true,
                PayloadOpCode = TinyBattleStateCodec.PayloadOpCode,
                Payload = TinyBattleStateCodec.Encode(battle.CaptureState())
            };

        private static void SetPrivate(TinyBattleSession session, string name, object value) =>
            typeof(TinyBattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, value);

        [Test]
        public void BeginLoadingRetryKeepsCommandId()
        {
            var roomClient = new RoomClientStub { FailFirstBeginLoading = true };
            var launch = new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
                "owner", "token", TimeSpan.FromSeconds(5));
            using (var session = new TinyBattleSession(launch, new GatewayStub(roomClient)))
            {
                session.RestoreAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.Throws<InvalidOperationException>(() =>
                    session.BeginLoadingAsync(CancellationToken.None).GetAwaiter().GetResult());
                session.BeginLoadingAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(roomClient.BeginLoadingIds.Count, Is.EqualTo(2));
                Assert.That(roomClient.BeginLoadingIds[0], Is.EqualTo(roomClient.BeginLoadingIds[1]));
            }
        }

        [Test]
        public void LateCreateDoesNotJoinAfterRestore() => RunAsync(async () =>
        {
            var rooms = new RoomClientStub();
            var gateway = new GatewayStub(rooms);
            using (var session = NewSession(gateway))
            {
                var pending = new TaskCompletionSource<RoomGatewayCreateResult>();
                rooms.PendingCreate = pending;
                var create = session.CreateRoomAsync(CancellationToken.None);
                gateway.ConnectionGeneration = 1;
                gateway.ConnectionState = RoomGatewayConnectionState.RestoreRequired;
                rooms.PendingCreate = null;
                await session.RecoverConnectionAsync(CancellationToken.None);
                pending.SetResult(new RoomGatewayCreateResult(true, "room-2", 2, string.Empty));
                await create;
                Assert.That(session.RoomId, Is.EqualTo("room-1"));
                Assert.That(rooms.JoinCalls, Is.Zero);
            }
        });

        [Test]
        public void CreateRetryAfterTimeoutAndEmptyRestoreKeepsCommandId() => RunAsync(async () =>
        {
            var rooms = new RoomClientStub { HasActiveRoom = false };
            var gateway = new GatewayStub(rooms);
            using (var session = NewSession(gateway))
            {
                var pending = new TaskCompletionSource<RoomGatewayCreateResult>();
                rooms.PendingCreate = pending;
                var create = session.CreateRoomAsync(TinySyncMode.Frame, CancellationToken.None);
                pending.SetException(new TimeoutException("No response"));
                try { await create; Assert.Fail("The create request must time out."); }
                catch (TimeoutException) { }

                gateway.ConnectionGeneration = 1;
                gateway.ConnectionState = RoomGatewayConnectionState.RestoreRequired;
                await session.RecoverConnectionAsync(CancellationToken.None);
                Assert.That(session.RoomId, Is.Empty);
                Assert.That(gateway.ConnectionState, Is.EqualTo(RoomGatewayConnectionState.Connected));
                try
                {
                    await session.CreateRoomAsync(TinySyncMode.State, CancellationToken.None);
                    Assert.Fail("A pending create cannot switch sync mode.");
                }
                catch (InvalidOperationException) { }

                rooms.PendingCreate = null;
                await session.CreateRoomAsync(TinySyncMode.Frame, CancellationToken.None);
                Assert.That(rooms.CreateCommandIds, Has.Count.EqualTo(2));
                Assert.That(rooms.CreateCommandIds[0], Is.EqualTo(rooms.CreateCommandIds[1]));
                Assert.That(rooms.JoinCalls, Is.EqualTo(1));
                Assert.That(session.RoomId, Is.EqualTo("room-2"));
            }
        });

        [Test]
        public void LateJoinCannotReplaceRestoredRoom() => RunAsync(async () =>
        {
            var rooms = new RoomClientStub();
            var gateway = new GatewayStub(rooms);
            using (var session = NewSession(gateway))
            {
                var pending = new TaskCompletionSource<RoomGatewayJoinResult>();
                rooms.PendingJoin = pending;
                var join = session.JoinRoomAsync("room-2", CancellationToken.None);
                gateway.ConnectionGeneration = 1;
                gateway.ConnectionState = RoomGatewayConnectionState.RestoreRequired;
                rooms.PendingJoin = null;
                await session.RecoverConnectionAsync(CancellationToken.None);
                pending.SetResult(new RoomGatewayJoinResult(true, "room-2", 2, default,
                    string.Empty, string.Empty, true, default, 0, 0));
                await join;
                Assert.That(session.RoomId, Is.EqualTo("room-1"));
                Assert.That(session.Status, Is.EqualTo("Lobby"));
            }
        });

        [Test]
        public void LateReadyAndLoadingCannotOverrideNewerSnapshot() => RunAsync(async () =>
        {
            var rooms = new RoomClientStub();
            using (var session = NewSession(new GatewayStub(rooms)))
            {
                await session.RestoreAsync(CancellationToken.None);
                var readyResult = new TaskCompletionSource<RoomGatewayReadyResult>();
                rooms.PendingReady = readyResult;
                var ready = session.SetReadyAsync(CancellationToken.None);
                rooms.PendingReady = null;
                await session.PollAsync(CancellationToken.None);
                readyResult.SetResult(new RoomGatewayReadyResult(true, string.Empty, true, string.Empty));
                await ready;
                Assert.That(session.Status, Is.EqualTo("Lobby"));

                var loadingResult = new TaskCompletionSource<RoomGatewayBeginLoadingResult>();
                rooms.PendingBeginLoading = loadingResult;
                var loading = session.BeginLoadingAsync(CancellationToken.None);
                var firstCommandId = rooms.BeginLoadingIds[0];
                rooms.PendingBeginLoading = null;
                await session.PollAsync(CancellationToken.None);
                loadingResult.SetResult(new RoomGatewayBeginLoadingResult(true, true, 0,
                    string.Empty, 2, null));
                await loading;
                Assert.That(session.Status, Is.EqualTo("Lobby"));
                await session.BeginLoadingAsync(CancellationToken.None);
                Assert.That(rooms.BeginLoadingIds[1], Is.EqualTo(firstCommandId));
                Assert.That(session.Status, Is.EqualTo("Loading"));
            }
        });

        [Test]
        public void LateLeaveCannotClearNewerRoomSnapshot() => RunAsync(async () =>
        {
            var rooms = new RoomClientStub();
            using (var session = NewSession(new GatewayStub(rooms)))
            {
                await session.RestoreAsync(CancellationToken.None);
                var pending = new TaskCompletionSource<RoomGatewayLeaveResult>();
                rooms.PendingLeave = pending;
                var leave = session.LeaveLobbyAsync(CancellationToken.None);
                rooms.PendingLeave = null;
                await session.PollAsync(CancellationToken.None);
                pending.SetResult(new RoomGatewayLeaveResult(true, true, 0, string.Empty, 2, null));
                await leave;
                Assert.That(session.RoomId, Is.EqualTo("room-1"));
                Assert.That(session.Room, Is.Not.Null);
                await session.LeaveLobbyAsync(CancellationToken.None);
                Assert.That(session.RoomId, Is.Empty);
            }
        });

        [Test]
        public void RoomRequestsUseLaunchTimeout()
        {
            var rooms = new RoomClientStub();
            using (var session = NewSession(new GatewayStub(rooms)))
            {
                session.RestoreAsync(CancellationToken.None).GetAwaiter().GetResult();
                session.PollAsync(CancellationToken.None).GetAwaiter().GetResult();
                session.SetReadyAsync(CancellationToken.None).GetAwaiter().GetResult();
                session.BeginLoadingAsync(CancellationToken.None).GetAwaiter().GetResult();
                rooms.NextSnapshot = new RoomGatewaySnapshot
                {
                    RoomId = "room-1", Phase = RoomGatewaySessionPhase.Loading,
                    LaunchGeneration = 1, LaunchManifestVersion = 1,
                    LaunchManifestHash = "test-manifest"
                };
                session.PollAsync(CancellationToken.None).GetAwaiter().GetResult();
                session.LeaveLobbyAsync(CancellationToken.None).GetAwaiter().GetResult();
                session.CreateRoomAsync(CancellationToken.None).GetAwaiter().GetResult();

                Assert.That(rooms.RequestTimeouts.Count, Is.GreaterThanOrEqualTo(9));
                Assert.That(rooms.RequestTimeouts, Has.All.EqualTo(TimeSpan.FromSeconds(5)));
            }
        }

        [Test]
        public void UnansweredSnapshotRequestReleasesItsDeadline() => RunAsync(async () =>
        {
            var gateway = new GatewayStub(new RoomClientStub());
            gateway.PendingFullSnapshot = new TaskCompletionSource<WireRequestFullStateSyncRes>();
            var launch = new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
                "owner", "token", TimeSpan.FromMilliseconds(50));
            using (var session = new TinyBattleSession(launch, gateway))
            {
                SetPrivate(session, "_roomId", "room-1");
                SetPrivate(session, "_battleId", "battle-1");
                SetPrivate(session, "_worldId", 7ul);
                try
                {
                    await session.RequestFullSnapshotAsync("Deadline test", CancellationToken.None);
                    Assert.Fail("An unanswered snapshot request must time out.");
                }
                catch (TimeoutException) { }
                Assert.That(session.SnapshotRequestCount, Is.EqualTo(1));
            }
        });

        [UnityTest]
        public IEnumerator ConnectionRecoveryPreemptsPendingCommand()
        {
            var rooms = new RoomClientStub();
            var gateway = new GatewayStub(rooms);
            var session = NewSession(gateway);
            var rootObject = new GameObject("Tiny recovery preemption test");
            var root = rootObject.AddComponent<TinyGameplayRoot>();
            root.enabled = false;
            var operation = typeof(TinyGameplayRoot).GetField("_operation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                SetRootPrivate(root, "_session", session);
                SetRootPrivate(root, "_lifetime", new CancellationTokenSource());
                operation.SetValue(root, Enum.Parse(operation.FieldType, "None"));
                var pendingCreate = new TaskCompletionSource<RoomGatewayCreateResult>();
                rooms.PendingCreate = pendingCreate;
                typeof(TinyGameplayRoot).GetMethod("CreateRoom",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(root, new object[] { TinySyncMode.State });
                Assert.That(operation.GetValue(root).ToString(), Is.EqualTo("Command"));

                var pendingRestore = new TaskCompletionSource<RoomGatewayRestoreRoomResult>();
                rooms.PendingRestore = pendingRestore;
                gateway.ConnectionGeneration = 1;
                gateway.ConnectionState = RoomGatewayConnectionState.RestoreRequired;
                TickRoot(root);
                Assert.That(rooms.LastCreateCancellationToken.IsCancellationRequested, Is.True);
                Assert.That(rooms.RestoreCalls, Is.EqualTo(1));
                Assert.That(operation.GetValue(root).ToString(), Is.EqualTo("Recover"));

                pendingCreate.SetResult(new RoomGatewayCreateResult(true, "room-2", 2, string.Empty));
                yield return null;
                Assert.That(operation.GetValue(root).ToString(), Is.EqualTo("Recover"));
                Assert.That(rooms.JoinCalls, Is.Zero);

                pendingRestore.SetCanceled();
                yield return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(rootObject); }
        }

        [TestCase(TinySyncMode.State, TinySyncTemplates.State)]
        [TestCase(TinySyncMode.Frame, TinySyncTemplates.Frame)]
        [TestCase(TinySyncMode.Hybrid, TinySyncTemplates.Hybrid)]
        public void CreateRoomSelectsServerSyncTemplate(TinySyncMode mode, string template)
        {
            var roomClient = new RoomClientStub();
            var launch = new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
                "owner", "token", TimeSpan.FromSeconds(5));
            using (var session = new TinyBattleSession(launch, new GatewayStub(roomClient)))
            {
                session.CreateRoomAsync(mode, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(roomClient.CreateTags, Is.Not.Null);
                Assert.That(roomClient.CreateTags[RoomGatewaySyncTagKeys.SyncTemplateId], Is.EqualTo(template));
            }
        }

        [Test]
        public void FrameInputRetriesProcessedFrameUsingServerFrame()
        {
            var gateway = new GatewayStub(new RoomClientStub());
            gateway.FrameResponses.Enqueue(new WireRoomSubmitFrameInputRes
            {
                Accepted = false,
                ServerFrame = 25,
                Reason = (int)TinyFrameInputSubmitReason.FrameAlreadyProcessed
            });
            gateway.FrameResponses.Enqueue(new WireRoomSubmitFrameInputRes
            {
                Accepted = true,
                ServerFrame = 30
            });
            using (var session = FrameSession(gateway, TinySyncMode.Frame))
            {
                session.SubmitInputAsync(new TinyInput(1, 0, true), CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert.That(gateway.SubmittedFrames, Has.Count.EqualTo(2));
                Assert.That(gateway.SubmittedFrames[1], Is.GreaterThanOrEqualTo(
                    25 + TinySyncSettings.SubmissionLeadFrames));
                session.SubmitInputAsync(new TinyInput(1, 0, false), CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert.That(gateway.SubmittedFrames[2], Is.GreaterThanOrEqualTo(
                    30 + TinySyncSettings.SubmissionLeadFrames));
            }
        }

        [Test]
        public void HybridRejectedPredictionRequestsSnapshotWithoutRetry()
        {
            var gateway = new GatewayStub(new RoomClientStub());
            gateway.FrameResponses.Enqueue(new WireRoomSubmitFrameInputRes
            {
                Accepted = false,
                ServerFrame = 25,
                Reason = (int)TinyFrameInputSubmitReason.FrameAlreadyProcessed
            });
            using (var session = FrameSession(gateway, TinySyncMode.Hybrid))
            {
                var error = Assert.Throws<InvalidOperationException>(() => session.SubmitInputAsync(
                    new TinyInput(1, 0, true), CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(error.Message, Does.Contain("FrameAlreadyProcessed"));
                Assert.That(gateway.SubmittedFrames, Has.Count.EqualTo(1));
                Assert.That(session.Telemetry.LocalPredictions, Is.EqualTo(1));
                Assert.That(session.NeedsFullSnapshot, Is.True);
                Assert.That(session.CanSubmitInput, Is.False);
            }
        }

        [Test]
        public void UnansweredHybridInputRequestsFullRecovery() => RunAsync(async () =>
        {
            var gateway = new GatewayStub(new RoomClientStub())
            {
                PendingFrameInput = new TaskCompletionSource<WireRoomSubmitFrameInputRes>()
            };
            using (var session = FrameSession(gateway, TinySyncMode.Hybrid,
                TimeSpan.FromMilliseconds(50)))
            {
                try
                {
                    await session.SubmitInputAsync(new TinyInput(1, 0, false),
                        CancellationToken.None);
                    Assert.Fail("An unanswered frame input must time out.");
                }
                catch (TimeoutException) { }
                Assert.That(session.NeedsFullSnapshot, Is.True);
                Assert.That(session.CanSubmitInput, Is.False);
                Assert.That(gateway.Submissions, Is.EqualTo(1));
            }
        });

        [Test]
        public void FrameInputStopsAfterThreeProcessedFrameResponses()
        {
            var gateway = new GatewayStub(new RoomClientStub());
            for (var frame = 25; frame < 28; frame++)
                gateway.FrameResponses.Enqueue(new WireRoomSubmitFrameInputRes
                {
                    ServerFrame = frame,
                    Reason = (int)TinyFrameInputSubmitReason.FrameAlreadyProcessed
                });
            using (var session = FrameSession(gateway, TinySyncMode.Frame))
            {
                var error = Assert.Throws<InvalidOperationException>(() => session.SubmitInputAsync(
                    new TinyInput(1, 0, true), CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(error.Message, Does.Contain("FrameAlreadyProcessed"));
                Assert.That(gateway.SubmittedFrames, Has.Count.EqualTo(3));
                Assert.That(session.NeedsFullSnapshot, Is.False);
            }
        }

        [Test]
        public void TooFarAheadRequiresSnapshotButRateLimitDoesNot()
        {
            var gateway = new GatewayStub(new RoomClientStub());
            gateway.FrameResponses.Enqueue(new WireRoomSubmitFrameInputRes
            {
                Reason = (int)TinyFrameInputSubmitReason.RateLimited,
                ServerFrame = 8
            });
            gateway.FrameResponses.Enqueue(new WireRoomSubmitFrameInputRes
            {
                Reason = (int)TinyFrameInputSubmitReason.FrameTooFarAhead,
                ServerFrame = 1
            });
            using (var session = FrameSession(gateway, TinySyncMode.Frame))
            {
                Assert.Throws<InvalidOperationException>(() => session.SubmitInputAsync(
                    new TinyInput(1, 0, false), CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(session.NeedsFullSnapshot, Is.False);
                Assert.Throws<InvalidOperationException>(() => session.SubmitInputAsync(
                    new TinyInput(1, 0, false), CancellationToken.None).GetAwaiter().GetResult());
                Assert.That(gateway.SubmittedFrames, Has.Count.EqualTo(2));
                Assert.That(gateway.SubmittedFrames[1], Is.EqualTo(gateway.SubmittedFrames[0] + 1));
                Assert.That(session.NeedsFullSnapshot, Is.True);
            }
        }

        private static TinyBattleSession FrameSession(GatewayStub gateway, TinySyncMode mode,
            TimeSpan? timeout = null)
        {
            var launch = new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
                "owner", "token", timeout ?? TimeSpan.FromSeconds(5));
            var session = new TinyBattleSession(launch, gateway);
            SetPrivate(session, "_roomId", "room-1");
            SetPrivate(session, "_battleId", "battle-1");
            SetPrivate(session, "_worldId", 7ul);
            SetPrivate(session, "_playerId", 1u);
            SetPrivate(session, "_syncMode", mode);
            var replication = new TinyFrameReplication();
            var baseline = Snapshot(NewBattle(), 7);
            replication.ApplyFullSnapshot(in baseline);
            SetPrivate(session, "_frameReplication", replication);
            return session;
        }

        private static TinyBattle NewBattle()
        {
            var battle = new TinyBattle();
            battle.AddPlayer(1, -1, 0);
            battle.AddPlayer(2, 1, 0);
            return battle;
        }

        private sealed class GatewayStub : ITinyBattleGateway
        {
            public GatewayStub(IRoomGatewaySessionClient rooms) { Rooms = rooms; }
            public IRoomGatewaySessionClient Rooms { get; }
            public RoomGatewayConnectionState ConnectionState { get; set; } = RoomGatewayConnectionState.Connected;
            public long ConnectionGeneration { get; set; }
            public int FrameOverflowCount { get; set; }
            public int OverflowMinimumFrame { get; set; }
            public WireStateSyncSnapshotPush? Latest { get; set; }
            public WireRequestFullStateSyncReq LastRequest { get; private set; }
            public int Requests { get; private set; }
            public int ResumeCalls { get; private set; }
            public int Submissions { get; private set; }
            public readonly System.Collections.Generic.Queue<WireRoomSubmitFrameInputRes> FrameResponses =
                new System.Collections.Generic.Queue<WireRoomSubmitFrameInputRes>();
            public readonly System.Collections.Generic.Queue<WireRequestFullStateSyncRes> FullSnapshotResponses =
                new System.Collections.Generic.Queue<WireRequestFullStateSyncRes>();
            public TaskCompletionSource<WireRequestFullStateSyncRes> PendingFullSnapshot;
            public TaskCompletionSource<WireRoomSubmitFrameInputRes> PendingFrameInput;
            public readonly System.Collections.Generic.List<int> SubmittedFrames =
                new System.Collections.Generic.List<int>();
            public int TickCalls { get; private set; }
            public void Tick(float deltaTime) { TickCalls++; }
            public bool CompleteRestore(long generation)
            {
                if (generation != ConnectionGeneration) return false;
                ConnectionState = RoomGatewayConnectionState.Connected;
                return true;
            }
            public Task<WireRequestFullStateSyncRes> RequestFullSnapshotAsync(
                WireRequestFullStateSyncReq request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                Requests++;
                if (PendingFullSnapshot != null)
                {
                    var pending = PendingFullSnapshot;
                    cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
                    return pending.Task;
                }
                return Task.FromResult(FullSnapshotResponses.Count > 0 ? FullSnapshotResponses.Dequeue() :
                    new WireRequestFullStateSyncRes { Success = true, Accepted = true });
            }
            public bool TryDequeueFrame(out WireRoomFramePush frame)
            {
                frame = default;
                return false;
            }
            public bool TryGetFrameOverflow(out int minimumRecoveryFrame)
            {
                minimumRecoveryFrame = OverflowMinimumFrame;
                return OverflowMinimumFrame > 0;
            }
            public void PrepareFullSnapshotRecovery() { }
            public void ResumeFrameStream() { ResumeCalls++; OverflowMinimumFrame = 0; }
            public Task<WireRoomSubscribeFrameSyncRes> SubscribeFrameSyncAsync(
                WireRoomSubscribeFrameSyncReq request, CancellationToken cancellationToken) =>
                throw new NotImplementedException();
            public Task<WireRoomSubmitFrameInputRes> SubmitFrameInputAsync(
                WireRoomSubmitFrameInputReq request, CancellationToken cancellationToken)
            {
                Submissions++;
                SubmittedFrames.Add(request.Frame);
                if (PendingFrameInput != null)
                {
                    var pending = PendingFrameInput;
                    cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
                    return pending.Task;
                }
                return Task.FromResult(FrameResponses.Count > 0 ? FrameResponses.Dequeue() :
                    new WireRoomSubmitFrameInputRes { Accepted = true });
            }
            public bool TryGetLatest(out WireStateSyncSnapshotPush snapshot)
            {
                if (Latest.HasValue)
                {
                    snapshot = Latest.Value;
                    Latest = null;
                    return true;
                }
                snapshot = default;
                return false;
            }
            public Task<WireSubmitBattleInputRes> SubmitAsync(string sessionToken, string battleId,
                ulong worldId, uint playerId, TinyInput input, ulong sequence,
                CancellationToken cancellationToken) => throw new NotImplementedException();
            public void Dispose() { }
        }

        private sealed class RoomClientStub : IRoomGatewaySessionClient
        {
            public string OwnerAccountId = "owner";
            public string NextBattleId = string.Empty;
            public bool HasActiveRoom = true;
            public RoomGatewaySnapshot NextSnapshot;
            public int RestoreCalls;
            public bool FailFirstBeginLoading;
            public readonly System.Collections.Generic.List<string> BeginLoadingIds =
                new System.Collections.Generic.List<string>();
            public readonly System.Collections.Generic.List<string> CreateCommandIds =
                new System.Collections.Generic.List<string>();
            public System.Collections.Generic.IReadOnlyDictionary<string, string> CreateTags;
            public TaskCompletionSource<RoomGatewayRestoreRoomResult> PendingRestore;
            public TaskCompletionSource<RoomGatewayGetSnapshotResult> PendingSnapshot;
            public TaskCompletionSource<RoomGatewayStateSyncSubscriptionResult> PendingSubscription;
            public TaskCompletionSource<RoomGatewayCreateResult> PendingCreate;
            public TaskCompletionSource<RoomGatewayJoinResult> PendingJoin;
            public TaskCompletionSource<RoomGatewayReadyResult> PendingReady;
            public TaskCompletionSource<RoomGatewayBeginLoadingResult> PendingBeginLoading;
            public TaskCompletionSource<RoomGatewayLeaveResult> PendingLeave;
            public int JoinCalls;
            public CancellationToken LastCreateCancellationToken;
            public readonly System.Collections.Generic.List<TimeSpan?> RequestTimeouts =
                new System.Collections.Generic.List<TimeSpan?>();

            public RoomGatewayRestoreRoomResult RestoreResult() =>
                new RoomGatewayRestoreRoomResult(true, HasActiveRoom, false,
                    HasActiveRoom ? "room-1" : string.Empty, HasActiveRoom ? 1ul : 0ul,
                    default, string.Empty, string.Empty, false, default, 0, 0,
                    default, default);

            public Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(
                RoomGatewayRestoreRoomRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                RestoreCalls++;
                RequestTimeouts.Add(timeout);
                return PendingRestore?.Task ?? Task.FromResult(RestoreResult());
            }

            public Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(
                RoomGatewayGetSnapshotRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                RequestTimeouts.Add(timeout);
                return PendingSnapshot?.Task ?? Task.FromResult(new RoomGatewayGetSnapshotResult(true, request.RoomId, 1,
                    NextSnapshot ?? new RoomGatewaySnapshot
                    {
                        RoomId = request.RoomId,
                        BattleId = NextBattleId,
                        WorldId = string.IsNullOrEmpty(NextBattleId) ? 0ul : 2ul,
                        OwnerAccountId = OwnerAccountId,
                        Phase = RoomGatewaySessionPhase.Lobby,
                        CanStart = true
                    }, string.Empty));
            }

            public Task<RoomGatewayCreateResult> CreateRoomAsync(RoomGatewayCreateRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                CreateTags = request.Tags;
                CreateCommandIds.Add(request.CommandId);
                LastCreateCancellationToken = cancellationToken;
                RequestTimeouts.Add(timeout);
                return PendingCreate?.Task ?? Task.FromResult(
                    new RoomGatewayCreateResult(true, "room-2", 2, string.Empty));
            }
            public Task<RoomGatewayJoinResult> JoinRoomAsync(RoomGatewayJoinRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                JoinCalls++;
                RequestTimeouts.Add(timeout);
                return PendingJoin?.Task ?? Task.FromResult(new RoomGatewayJoinResult(true,
                    request.RoomId, 2, default, string.Empty, string.Empty, true, default, 0, 0));
            }
            public Task<RoomGatewayLeaveResult> LeaveRoomAsync(RoomGatewayLeaveRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                RequestTimeouts.Add(timeout);
                return PendingLeave?.Task ?? Task.FromResult(new RoomGatewayLeaveResult(
                    true, true, 0, string.Empty, 2, null));
            }
            public Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                RequestTimeouts.Add(timeout);
                return PendingReady?.Task ?? Task.FromResult(new RoomGatewayReadyResult(
                    true, string.Empty, true, string.Empty));
            }
            public Task<RoomGatewayPickHeroResult> PickHeroAsync(RoomGatewayPickHeroRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(RoomGatewayBeginLoadingRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                BeginLoadingIds.Add(request.CommandId);
                RequestTimeouts.Add(timeout);
                if (PendingBeginLoading != null) return PendingBeginLoading.Task;
                var success = !FailFirstBeginLoading || BeginLoadingIds.Count > 1;
                return Task.FromResult(new RoomGatewayBeginLoadingResult(success, success, 0,
                    success ? string.Empty : "retry", 1, null));
            }
            public Task<RoomGatewayReportLoadingProgressResult> ReportLoadingProgressAsync(
                RoomGatewayReportLoadingProgressRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(
                RoomGatewayReportAssetsLoadedRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                RequestTimeouts.Add(timeout);
                return Task.FromResult(new RoomGatewayReportAssetsLoadedResult(
                    true, true, 0, string.Empty, 2, null));
            }
            public Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(RoomGatewayCancelLoadingRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<RoomGatewayStartBattleResult> StartBattleAsync(RoomGatewayStartBattleRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
                RoomGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => PendingSubscription?.Task ?? Task.FromResult(
                    new RoomGatewayStateSyncSubscriptionResult(true, string.Empty));
        }
    }
}
