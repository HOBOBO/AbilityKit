using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests
{
    /// <summary>
    /// <see cref="RoomGatewayBattleSessionBase"/> 的契约测试：房间生命周期、命令 id 台账、
    /// generation/revision 守卫、基线等待与全量快照请求。会话语义（回合/帧/预测）不在覆盖范围。
    /// </summary>
    public sealed class RoomGatewayBattleSessionBaseTests
    {
        [Fact]
        public void Tick_ThrowsAfterDispose()
        {
            using var session = NewSession(new Connection(new Rooms()));

            session.Dispose();

            Assert.Throws<ObjectDisposedException>(() => session.Tick(0.016f));
        }

        [Fact]
        public async Task Dispose_InvalidatesInFlightCreateSoLateResultCannotJoin()
        {
            var rooms = new Rooms();
            using var connection = new Connection(rooms);
            using var session = NewSession(connection);
            var pending = new TaskCompletionSource<RoomGatewayCreateResult>();
            rooms.PendingCreate = pending;
            var create = session.CreateRoomAsync(CancellationToken.None);
            session.Dispose();
            pending.SetResult(new RoomGatewayCreateResult(true, "old-room", 1, ""));
            await create;

            Assert.Equal(0, rooms.JoinCalls);
            Assert.Equal(string.Empty, session.RoomId);
        }

        [Fact]
        public async Task RejectedCreate_UsesFreshCommandIdOnRetry()
        {
            var rooms = new Rooms { RejectCreateOnce = true };
            using var session = NewSession(new Connection(rooms));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.CreateRoomAsync(CancellationToken.None));
            await session.CreateRoomAsync(CancellationToken.None);

            Assert.Equal(2, rooms.CreateIds.Count);
            Assert.NotEqual(rooms.CreateIds[0], rooms.CreateIds[1]);
            Assert.Equal("room-1", session.RoomId);
        }

        [Fact]
        public async Task TimedOutCreate_RetriesOriginalCommandId()
        {
            var rooms = new Rooms();
            using var session = NewSession(new Connection(rooms));
            var pending = new TaskCompletionSource<RoomGatewayCreateResult>();
            rooms.PendingCreate = pending;
            var first = session.CreateRoomAsync(CancellationToken.None);
            pending.SetException(new TimeoutException("request deadline"));
            await Assert.ThrowsAsync<TimeoutException>(() => first);
            rooms.PendingCreate = null;

            await session.CreateRoomAsync(CancellationToken.None);

            Assert.Equal(rooms.CreateIds[0], rooms.CreateIds[1]);
        }

        [Fact]
        public async Task LateCreateFromOldConnectionGeneration_CannotJoinRoom()
        {
            var rooms = new Rooms();
            using var connection = new Connection(rooms);
            using var session = NewSession(connection);
            var pending = new TaskCompletionSource<RoomGatewayCreateResult>();
            rooms.PendingCreate = pending;
            var create = session.CreateRoomAsync(CancellationToken.None);
            connection.Generation++;
            pending.SetResult(new RoomGatewayCreateResult(true, "old-room", 1, ""));
            await create;

            Assert.Equal(0, rooms.JoinCalls);
            Assert.Equal(string.Empty, session.RoomId);
        }

        [Fact]
        public async Task OlderPollCannotReplaceNewerRoomSnapshot()
        {
            var rooms = new Rooms();
            using var session = NewSession(new Connection(rooms));
            await session.JoinRoomAsync("room-1", CancellationToken.None);
            var pending = new TaskCompletionSource<RoomGatewayGetSnapshotResult>();
            rooms.PendingSnapshot = pending;
            var oldPoll = session.PollAsync(CancellationToken.None);
            rooms.PendingSnapshot = null;
            rooms.Owner = "new-owner";
            await session.PollAsync(CancellationToken.None);
            pending.SetResult(new RoomGatewayGetSnapshotResult(true, "room-1", 1,
                LobbySnapshot("old-owner"), ""));
            await oldPoll;

            Assert.Equal("new-owner", session.Room!.OwnerAccountId);
        }

        [Fact]
        public async Task PollIntoBattle_BindsBattleWaitsForBaselineAndRequestsFullSnapshot()
        {
            var rooms = new Rooms { NextSnapshot = BattleSnapshot("battle-1", 7) };
            using var connection = new Connection(rooms);
            using var session = NewSession(connection);
            await session.JoinRoomAsync("room-1", CancellationToken.None);

            await session.PollAsync(CancellationToken.None);

            Assert.Equal("battle-1", session.BattleId);
            Assert.Equal(7UL, session.TestWorldId);
            Assert.Equal(1u, session.PlayerId);
            Assert.True(session.TestWaitingForBaseline);
            Assert.Equal(1, connection.FullSnapshotRequests.Count);
            Assert.Equal("Subscription baseline", connection.FullSnapshotRequests[0].Reason);
            Assert.All(connection.FullSnapshotTimeouts,
                timeout => Assert.Equal(TimeSpan.FromSeconds(5), timeout));
        }

        [Fact]
        public async Task PollIntoBattle_WithMissingPlayerSlot_ThrowsBeforeSubscription()
        {
            var snapshot = BattleSnapshot("battle-1", 7);
            snapshot.Players = new List<RoomGatewayPlayerSnapshot>();
            var rooms = new Rooms { NextSnapshot = snapshot };
            using var session = NewSession(new Connection(rooms));
            await session.JoinRoomAsync("room-1", CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.PollAsync(CancellationToken.None));

            Assert.Equal(0, rooms.Subscriptions);
            Assert.Equal(string.Empty, session.BattleId);
        }

        [Fact]
        public async Task PollIntoBattle_WithUnnegotiatedProfile_ThrowsBeforeSubscription()
        {
            var snapshot = BattleSnapshot("battle-1", 7);
            snapshot.SyncCapabilities = Capabilities(nameof(NetworkSyncModel.PredictRollback), 1, 1);
            var rooms = new Rooms { NextSnapshot = snapshot };
            using var session = NewSession(new Connection(rooms));
            await session.JoinRoomAsync("room-1", CancellationToken.None);

            await Assert.ThrowsAsync<RoomGatewaySyncCapabilityException>(
                () => session.PollAsync(CancellationToken.None));

            Assert.Equal(0, rooms.Subscriptions);
            Assert.Equal(string.Empty, session.BattleId);
        }

        [Fact]
        public async Task RequestFullSnapshot_SendsIdentityBattleAndCursorFrame()
        {
            var rooms = new Rooms { NextSnapshot = BattleSnapshot("battle-1", 7) };
            using var connection = new Connection(rooms);
            using var session = NewSession(connection);
            await session.JoinRoomAsync("room-1", CancellationToken.None);
            await session.PollAsync(CancellationToken.None);

            await session.RequestSnapshot("manual", CancellationToken.None);

            var request = connection.FullSnapshotRequests[^1];
            Assert.Equal("token", request.SessionToken);
            Assert.Equal("battle-1", request.BattleId);
            Assert.Equal("room-1", request.RoomId);
            Assert.Equal(7UL, request.WorldId);
            Assert.Equal("manual", request.Reason);
            Assert.Equal(session.SnapshotCursor.LastFrame, request.ClientFrame);
            Assert.Equal(session.SnapshotCursor.LastFrame, request.LastAuthoritativeFrame);
        }

        [Fact]
        public async Task RequestFullSnapshot_WithoutBattle_Throws()
        {
            using var session = NewSession(new Connection(new Rooms()));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.RequestSnapshot("manual", CancellationToken.None));
        }

        [Fact]
        public async Task JoiningAnotherRoom_ClearsSnapshotCursorAndLedger()
        {
            var rooms = new Rooms();
            using var session = NewSession(new Connection(rooms));
            await session.JoinRoomAsync("room-1", CancellationToken.None);
            session.SnapshotCursor.Reset(9);

            await session.JoinRoomAsync("room-2", CancellationToken.None);

            Assert.Equal("room-2", session.RoomId);
            Assert.Null(session.Room);
            Assert.NotEqual(9UL, session.SnapshotCursor.WorldId);
        }

        [Fact]
        public async Task RoomCommands_UseIdentityRequestTimeout()
        {
            var rooms = new Rooms();
            using var session = NewSession(new Connection(rooms));

            await session.CreateRoomAsync(CancellationToken.None);
            await session.SetReadyAsync(CancellationToken.None);
            await session.BeginLoadingAsync(CancellationToken.None);
            await session.PollAsync(CancellationToken.None);
            await session.LeaveLobbyAsync(CancellationToken.None);

            Assert.NotEmpty(rooms.Timeouts);
            Assert.All(rooms.Timeouts, timeout => Assert.Equal(TimeSpan.FromSeconds(5), timeout));
        }

        [Fact]
        public async Task Restore_WithNoActiveRoom_UnbindsLocalRoom()
        {
            var rooms = new Rooms { RestoreHasActiveRoom = false };
            using var session = NewSession(new Connection(rooms));
            await session.JoinRoomAsync("room-1", CancellationToken.None);

            var restored = await session.TestRestoreAsync(CancellationToken.None);

            Assert.True(restored);
            Assert.Equal(string.Empty, session.RoomId);
            Assert.Null(session.Room);
            // 基类默认用恢复状态名；玩法可覆写（Tiny Turn 覆写为 "Connected"）。
            Assert.Equal(RoomGatewaySessionRestoreStatus.Restored.ToString(), session.Status);
        }

        private static TestSession NewSession(Connection connection) => new(connection);

        private static RoomGatewaySnapshot LobbySnapshot(string owner) => new()
        {
            RoomId = "room-1", OwnerAccountId = owner,
            Phase = RoomGatewaySessionPhase.Lobby, CanStart = true
        };

        private static RoomGatewayNetworkSyncCapabilities Capabilities(
            string profileName, int minimumSchema, int maximumSchema)
        {
            var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
            var capabilities = NetworkSyncCapabilities.FromProfile(in profile, minimumSchema, maximumSchema);
            return RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(
                new WireNetworkSyncCapabilities
                {
                    MetadataVersion = 1,
                    ProfileName = profileName,
                    MinimumSchemaVersion = capabilities.MinimumSchemaVersion,
                    MaximumSchemaVersion = capabilities.MaximumSchemaVersion,
                    ClientPlayback = (int)capabilities.ClientPlayback,
                    Input = (int)capabilities.Input,
                    Snapshot = (int)capabilities.Snapshot,
                    Interest = (int)capabilities.Interest,
                    Recovery = (int)capabilities.Recovery,
                    ServerValidation = (int)capabilities.ServerValidation,
                    ReliableEvent = (int)capabilities.ReliableEvent
                });
        }

        private static RoomGatewaySnapshot BattleSnapshot(string battleId, ulong worldId) => new()
        {
            RoomId = "room-1", BattleId = battleId, WorldId = worldId,
            Phase = RoomGatewaySessionPhase.InBattle,
            LaunchManifestVersion = 1,
            LaunchManifestHash = "any",
            Players = new List<RoomGatewayPlayerSnapshot>
            {
                new() { AccountId = "owner", PlayerId = 1 }
            },
            SyncCapabilities = Capabilities(
                nameof(NetworkSyncModel.AuthoritativeInterpolation), 1, 1)
        };

        private sealed class TestSession : RoomGatewayBattleSessionBase
        {
            private readonly Connection _connection;

            public TestSession(Connection connection)
                : base(new RoomGatewaySessionIdentity(
                    "token", "local", "dev", "owner", TimeSpan.FromSeconds(5)), connection)
            {
                _connection = connection;
            }

            public ulong TestWorldId => BattleWorldId;

            public bool TestWaitingForBaseline => WaitingForBaseline;

            public Task<bool> TestRestoreAsync(CancellationToken token) => RestoreCoreAsync(token);

            public Task RequestSnapshot(string reason, CancellationToken token) =>
                RequestFullSnapshotCoreAsync(reason, token);

            public Task CreateRoomAsync(CancellationToken token) => CreateRoomCoreAsync(token);

            protected override void OnTick(float deltaTime)
            {
            }

            protected override RoomGatewayCreateRequest CreateRoomRequest(string commandId) => new(
                Identity.SessionToken, Identity.Region, Identity.ServerId,
                "test", "Test Battle", false, 2, commandId: commandId);

            protected override void ValidateLaunchManifest(RoomGatewaySnapshot room)
            {
            }

            protected override string ResolveSubscriptionModelName(RoomGatewaySnapshot room) =>
                nameof(NetworkSyncModel.AuthoritativeInterpolation);

            protected override NetworkSyncProfile ResolveSubscriptionProfile(RoomGatewaySnapshot room) =>
                NetworkSyncProfiles.AuthoritativeInterpolation;

            protected override Task<WireRequestFullStateSyncRes> SendFullSnapshotRequestAsync(
                WireRequestFullStateSyncReq request, CancellationToken cancellationToken) =>
                _connection.SendFullSnapshotAsync(request, RequestTimeout, cancellationToken);
        }

        private sealed class Connection : IRoomGatewayBattleConnection
        {
            private readonly Rooms _rooms;

            public Connection(Rooms rooms) => _rooms = rooms;

            public IRoomGatewaySessionClient Rooms => _rooms;
            public RoomGatewayConnectionState ConnectionState { get; set; } = RoomGatewayConnectionState.Connected;
            public long ConnectionGeneration => Generation;
            public long Generation { get; set; }
            public readonly List<WireRequestFullStateSyncReq> FullSnapshotRequests = new();
            public readonly List<TimeSpan> FullSnapshotTimeouts = new();

            public Task<WireRequestFullStateSyncRes> SendFullSnapshotAsync(
                WireRequestFullStateSyncReq request, TimeSpan timeout, CancellationToken token)
            {
                FullSnapshotRequests.Add(request);
                FullSnapshotTimeouts.Add(timeout);
                return Task.FromResult(new WireRequestFullStateSyncRes { Success = true, Accepted = true });
            }

            public void Tick(float deltaTime) { }
            public bool CompleteRestore(long generation) => true;
            public void Dispose() { }
        }

        private sealed class Rooms : IRoomGatewaySessionClient
        {
            public string Owner = "owner";
            public bool RejectCreateOnce;
            public bool RestoreHasActiveRoom = true;
            public int JoinCalls;
            public int Subscriptions;
            public readonly List<string> CreateIds = new();
            public readonly List<TimeSpan?> Timeouts = new();
            public TaskCompletionSource<RoomGatewayCreateResult>? PendingCreate;
            public TaskCompletionSource<RoomGatewayGetSnapshotResult>? PendingSnapshot;
            public RoomGatewaySnapshot? NextSnapshot;

            public Task<RoomGatewayCreateResult> CreateRoomAsync(RoomGatewayCreateRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                CreateIds.Add(request.CommandId);
                Timeouts.Add(timeout);
                if (RejectCreateOnce)
                {
                    RejectCreateOnce = false;
                    return Task.FromResult(new RoomGatewayCreateResult(false, "", 0, "rejected"));
                }
                return PendingCreate?.Task ?? Task.FromResult(new RoomGatewayCreateResult(true,
                    "room-1", 1, ""));
            }

            public Task<RoomGatewayJoinResult> JoinRoomAsync(RoomGatewayJoinRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                JoinCalls++;
                Timeouts.Add(timeout);
                return Task.FromResult(new RoomGatewayJoinResult(true, request.RoomId, 1,
                    default, "", "", true, default, 0, 0));
            }

            public Task<RoomGatewayLeaveResult> LeaveRoomAsync(RoomGatewayLeaveRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                Timeouts.Add(timeout);
                return Task.FromResult(new RoomGatewayLeaveResult(true, true, 0, "", 1, null));
            }

            public Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                Timeouts.Add(timeout);
                return Task.FromResult(new RoomGatewayReadyResult(true, "", true, ""));
            }

            public Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(RoomGatewayRestoreRoomRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                Timeouts.Add(timeout);
                return Task.FromResult(new RoomGatewayRestoreRoomResult(
                    true, RestoreHasActiveRoom, false, "", 0, default, "", "", false,
                    default, 0, 0, default, default));
            }

            public Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(RoomGatewayGetSnapshotRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                Timeouts.Add(timeout);
                return PendingSnapshot?.Task ?? Task.FromResult(new RoomGatewayGetSnapshotResult(
                    true, request.RoomId, 1, NextSnapshot ?? LobbySnapshot(Owner), ""));
            }

            public Task<RoomGatewayPickHeroResult> PickHeroAsync(RoomGatewayPickHeroRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();

            public Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(RoomGatewayBeginLoadingRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                Timeouts.Add(timeout);
                return Task.FromResult(new RoomGatewayBeginLoadingResult(true, true, 0, "", 1, null));
            }

            public Task<RoomGatewayReportLoadingProgressResult> ReportLoadingProgressAsync(
                RoomGatewayReportLoadingProgressRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(
                RoomGatewayReportAssetsLoadedRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) => Task.FromResult(
                new RoomGatewayReportAssetsLoadedResult(true, true, 0, "", 1, null));

            public Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(RoomGatewayCancelLoadingRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();

            public Task<RoomGatewayStartBattleResult> StartBattleAsync(RoomGatewayStartBattleRequest request,
                TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();

            public Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
                RoomGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                Subscriptions++;
                Timeouts.Add(timeout);
                return Task.FromResult(new RoomGatewayStateSyncSubscriptionResult(true, ""));
            }
        }
    }
}
