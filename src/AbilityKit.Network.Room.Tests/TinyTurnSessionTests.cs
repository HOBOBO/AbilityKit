using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.Turn;
using AbilityKit.Demo.Tiny.Turn.View;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

public sealed class TinyTurnSessionTests
{
    [Fact]
    public async Task LateCreateFromOldConnectionCannotJoinRoom()
    {
        var rooms = new Rooms();
        using var gateway = new Gateway(rooms);
        using var session = NewSession(gateway);
        var pending = new TaskCompletionSource<RoomGatewayCreateResult>();
        rooms.PendingCreate = pending;
        var create = session.CreateRoomAsync(CancellationToken.None);
        gateway.Generation++;
        pending.SetResult(new RoomGatewayCreateResult(true, "old-room", 1, ""));
        await create;
        Assert.Equal(0, rooms.JoinCalls);
        Assert.Equal("", session.RoomId);
    }

    [Fact]
    public async Task FailedJoinRetriesOriginalCreateCommandId()
    {
        var rooms = new Rooms { FailJoinOnce = true };
        using var session = NewSession(new Gateway(rooms));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.CreateRoomAsync(CancellationToken.None));
        await session.CreateRoomAsync(CancellationToken.None);
        Assert.Equal(2, rooms.CreateIds.Count);
        Assert.Equal(rooms.CreateIds[0], rooms.CreateIds[1]);
        Assert.Equal("room-1", session.RoomId);
    }

    [Fact]
    public async Task TimedOutCreateRetriesOriginalCommandId()
    {
        var rooms = new Rooms();
        using var session = NewSession(new Gateway(rooms));
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
    public async Task RejectedCreateStartsFreshCommand()
    {
        var rooms = new Rooms { RejectCreateOnce = true };
        using var session = NewSession(new Gateway(rooms));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.CreateRoomAsync(CancellationToken.None));
        await session.CreateRoomAsync(CancellationToken.None);
        Assert.NotEqual(rooms.CreateIds[0], rooms.CreateIds[1]);
    }

    [Fact]
    public async Task LateLeaveCannotClearReboundRoom()
    {
        var rooms = new Rooms();
        using var gateway = new Gateway(rooms);
        using var session = NewSession(gateway);
        await session.JoinRoomAsync("room-1", CancellationToken.None);
        var pending = new TaskCompletionSource<RoomGatewayLeaveResult>();
        rooms.PendingLeave = pending;
        var leave = session.LeaveLobbyAsync(CancellationToken.None);
        gateway.Generation++;
        rooms.PendingLeave = null;
        await session.JoinRoomAsync("room-2", CancellationToken.None);
        pending.SetResult(new RoomGatewayLeaveResult(true, true, 0, "", 1, null));
        await leave;
        Assert.Equal("room-2", session.RoomId);
    }

    [Fact]
    public async Task OlderPollCannotReplaceNewerRoomSnapshot()
    {
        var rooms = new Rooms();
        using var session = NewSession(new Gateway(rooms));
        await session.JoinRoomAsync("room-1", CancellationToken.None);
        var pending = new TaskCompletionSource<RoomGatewayGetSnapshotResult>();
        rooms.PendingSnapshot = pending;
        var oldPoll = session.PollAsync(CancellationToken.None);
        rooms.PendingSnapshot = null;
        rooms.Owner = "new-owner";
        await session.PollAsync(CancellationToken.None);
        pending.SetResult(new RoomGatewayGetSnapshotResult(true, "room-1", 1,
            Snapshot("old-owner"), ""));
        await oldPoll;
        Assert.Equal("new-owner", session.Room!.OwnerAccountId);
    }

    [Fact]
    public async Task RoomCommandsUseLaunchTimeout()
    {
        var rooms = new Rooms();
        using var session = NewSession(new Gateway(rooms));
        await session.CreateRoomAsync(CancellationToken.None);
        await session.SetReadyAsync(CancellationToken.None);
        await session.BeginLoadingAsync(CancellationToken.None);
        await session.PollAsync(CancellationToken.None);
        await session.LeaveLobbyAsync(CancellationToken.None);
        Assert.NotEmpty(rooms.Timeouts);
        Assert.All(rooms.Timeouts, timeout => Assert.Equal(TimeSpan.FromSeconds(5), timeout));
    }

    [Fact]
    public async Task OldSubscriptionCannotReplaceNewBattleInSameRoom()
    {
        var rooms = new Rooms();
        using var gateway = new Gateway(rooms);
        using var session = NewSession(gateway);
        await session.JoinRoomAsync("room-1", CancellationToken.None);
        var pending = new TaskCompletionSource<RoomGatewayStateSyncSubscriptionResult>();
        rooms.NextSnapshot = BattleSnapshot("battle-1", 1);
        rooms.PendingSubscription = pending;
        var oldPoll = session.PollAsync(CancellationToken.None);
        rooms.NextSnapshot = BattleSnapshot("battle-2", 2);
        rooms.PendingSubscription = null;
        await session.PollAsync(CancellationToken.None);
        pending.SetResult(new RoomGatewayStateSyncSubscriptionResult(true, ""));
        await oldPoll;
        Assert.Equal("battle-2", session.BattleId);
        Assert.Equal(1, gateway.FullSnapshotRequests);
        Assert.True(session.NeedsFullSnapshot);
        await session.RequestFullSnapshotAsync("missing baseline", CancellationToken.None);
        Assert.Equal(2, gateway.FullSnapshotRequests);
        Assert.All(gateway.FullSnapshotTimeouts,
            timeout => Assert.Equal(TimeSpan.FromSeconds(5), timeout));
    }

    [Fact]
    public async Task IncompatibleRulesRejectBeforeBattleSubscription()
    {
        var rooms = new Rooms { NextSnapshot = BattleSnapshot("battle-1", 1) };
        rooms.NextSnapshot.LaunchManifestHash = RoomGatewayLaunchManifestCompatibility.ComputeHash(
            new[] { TinyTurnBattle.AssetKey, "tiny:turn.rules.v2" },
            new Dictionary<string, string> { ["players"] = "1" });
        using var gateway = new Gateway(rooms);
        using var session = NewSession(gateway);
        await session.JoinRoomAsync("room-1", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.PollAsync(CancellationToken.None));
        Assert.Equal(0, gateway.FullSnapshotRequests);
        Assert.Empty(session.BattleId);
    }

    [Fact]
    public async Task LateActionAckCannotChangeNewBattleStatus()
    {
        var rooms = new Rooms { NextSnapshot = BattleSnapshot("battle-1", 1) };
        using var gateway = new Gateway(rooms);
        using var session = NewSession(gateway);
        await session.JoinRoomAsync("room-1", CancellationToken.None);
        await session.PollAsync(CancellationToken.None);
        gateway.Push(new WireStateSyncSnapshotPush
        {
            WorldId = 1, Frame = 1, IsFullSnapshot = true,
            PayloadOpCode = TinyTurnBattle.SnapshotOpCode,
            Payload = TinyTurnStateCodec.Encode(new TinyTurnState(1, 0, 1, 2, 2, 0))
        });
        session.Tick(0.01f);
        Assert.True(session.CanAct);
        var pending = new TaskCompletionSource<WireSubmitBattleInputRes>();
        gateway.PendingAction = pending;
        var action = session.SubmitActionAsync(CancellationToken.None);
        rooms.NextSnapshot = BattleSnapshot("battle-2", 2);
        await session.PollAsync(CancellationToken.None);
        pending.SetResult(new WireSubmitBattleInputRes { Success = true, Status = "Queued" });
        await action;
        Assert.Equal("battle-2", session.BattleId);
        Assert.NotEqual("Action queued", session.Status);
    }

    private static TinyTurnSession NewSession(Gateway gateway) => new(
        new DemoMultiplayerLaunchRequest("localhost", 4057, "local", "dev",
            "owner", "token", TimeSpan.FromSeconds(5)), gateway);

    private static RoomGatewaySnapshot Snapshot(string owner) => new()
    {
        RoomId = "room-1", OwnerAccountId = owner,
        Phase = RoomGatewaySessionPhase.Lobby, CanStart = true
    };

    private static RoomGatewaySnapshot BattleSnapshot(string battleId, ulong worldId)
    {
        var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
        var capabilities = NetworkSyncCapabilities.FromProfile(in profile, 1, 1);
        return new RoomGatewaySnapshot
        {
            RoomId = "room-1", BattleId = battleId, WorldId = worldId,
            Phase = RoomGatewaySessionPhase.InBattle,
            LaunchManifestVersion = 1,
            LaunchManifestHash = RoomGatewayLaunchManifestCompatibility.ComputeHash(
                new[] { TinyTurnBattle.AssetKey, TinyTurnBattle.RulesKey },
                new Dictionary<string, string> { ["players"] = "1" }),
            Players = [new RoomGatewayPlayerSnapshot { AccountId = "owner", PlayerId = 1 }],
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

    private sealed class Gateway(Rooms rooms) : ITinyTurnGateway
    {
        public IRoomGatewaySessionClient Rooms => rooms;
        public RoomGatewayConnectionState ConnectionState { get; set; } = RoomGatewayConnectionState.Connected;
        public long ConnectionGeneration => Generation;
        public long Generation;
        public int FullSnapshotRequests;
        public readonly List<TimeSpan> FullSnapshotTimeouts = [];
        public TaskCompletionSource<WireSubmitBattleInputRes>? PendingAction;
        public event Action<WireStateSyncSnapshotPush>? SnapshotReceived;
        public void Push(WireStateSyncSnapshotPush snapshot) => SnapshotReceived?.Invoke(snapshot);
        public void Tick(float deltaTime) { }
        public bool CompleteRestore(long generation) => true;
        public Task<WireSubmitBattleInputRes> SubmitActionAsync(WireSubmitBattleInputReq request,
            TimeSpan timeout, CancellationToken token) => PendingAction?.Task ??
            throw new NotImplementedException();
        public Task<WireRequestFullStateSyncRes> RequestFullSnapshotAsync(WireRequestFullStateSyncReq request,
            TimeSpan timeout, CancellationToken token)
        {
            FullSnapshotRequests++;
            FullSnapshotTimeouts.Add(timeout);
            return Task.FromResult(new WireRequestFullStateSyncRes { Success = true, Accepted = true });
        }
        public void Dispose() { }
    }

    private sealed class Rooms : IRoomGatewaySessionClient
    {
        public string Owner = "owner";
        public bool FailJoinOnce;
        public bool RejectCreateOnce;
        public int JoinCalls;
        public readonly List<string> CreateIds = [];
        public readonly List<TimeSpan?> Timeouts = [];
        public TaskCompletionSource<RoomGatewayCreateResult>? PendingCreate;
        public TaskCompletionSource<RoomGatewayGetSnapshotResult>? PendingSnapshot;
        public TaskCompletionSource<RoomGatewayLeaveResult>? PendingLeave;
        public TaskCompletionSource<RoomGatewayStateSyncSubscriptionResult>? PendingSubscription;
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
            var fail = FailJoinOnce;
            FailJoinOnce = false;
            return Task.FromResult(new RoomGatewayJoinResult(!fail, request.RoomId, 1,
                default, fail ? "retry" : "", "", true, default, 0, 0));
        }

        public Task<RoomGatewayLeaveResult> LeaveRoomAsync(RoomGatewayLeaveRequest request,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            Timeouts.Add(timeout);
            return PendingLeave?.Task ?? Task.FromResult(
                new RoomGatewayLeaveResult(true, true, 0, "", 1, null));
        }

        public Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            Timeouts.Add(timeout);
            return Task.FromResult(new RoomGatewayReadyResult(true, "", true, ""));
        }

        public Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(RoomGatewayRestoreRoomRequest request,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(RoomGatewayGetSnapshotRequest request,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            Timeouts.Add(timeout);
            return PendingSnapshot?.Task ?? Task.FromResult(new RoomGatewayGetSnapshotResult(
                true, request.RoomId, 1, NextSnapshot ?? Snapshot(Owner), ""));
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
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(RoomGatewayCancelLoadingRequest request,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<RoomGatewayStartBattleResult> StartBattleAsync(RoomGatewayStartBattleRequest request,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            RoomGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            PendingSubscription?.Task ?? Task.FromResult(
                new RoomGatewayStateSyncSubscriptionResult(true, ""));
    }
}
