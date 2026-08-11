using System;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

/// <summary>
/// Argument-validation contract for <see cref="GatewayMultiplayerSession.CreateAsync"/>.
/// These throw synchronously before any connection is opened, so no server harness is needed.
/// (The full connect→login→create→ready→start→subscribe flow needs an in-process gateway fixture;
/// see the integration guide's WIP section — adoption is gated on the room-flow staged-restore WIP.)
/// </summary>
public sealed class GatewayMultiplayerSessionTests
{
    private const string Host = "127.0.0.1";
    private const int Port = 1;
    private const string Account = "player-1";
    private static readonly RoomGatewayLaunchSpec Spec =
        new("region", "server", "test", "title", 2, 1, 1, 1, 1, "test-world", "client-1");

    [Fact]
    public Task CreateAsync_NullOrWhitespaceHost_Throws()
    {
        return Assert.ThrowsAsync<ArgumentException>(() =>
            GatewayMultiplayerSession.CreateAsync(null!, Port, Account, Spec));
    }

    [Fact]
    public Task CreateAsync_NonPositivePort_Throws()
    {
        return Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            GatewayMultiplayerSession.CreateAsync(Host, 0, Account, Spec));
    }

    [Fact]
    public Task CreateAsync_NullOrWhitespaceAccountId_Throws()
    {
        return Assert.ThrowsAsync<ArgumentException>(() =>
            GatewayMultiplayerSession.CreateAsync(Host, Port, "  ", Spec));
    }

    [Fact]
    public async Task RunRoomFlowAsync_SimpleFlowWithoutBattleStart_CompletesAndSubscribes()
    {
        // The injectable seam: drive the room-flow orchestration with a fake client (no network).
        var client = new FakeFlowClient();
        var flow = new RoomGatewaySessionFlow(client);

        var result = await GatewayMultiplayerSession.RunRoomFlowAsync(
            flow, "session-token", Spec, joinRoomId: null, waitForBattleStart: false, playerId: 1,
            timeout: TimeSpan.FromSeconds(5), cancellationToken: default);

        Assert.True(result.Subscribed);
        Assert.Equal("room-1", result.RoomId);
        Assert.Equal(1ul, result.NumericRoomId);
        Assert.True(client.Created && client.Joined && client.Readied && client.Subscribed);
    }

    [Fact]
    public async Task RunRoomFlowAsync_HeroPickHook_IsInvokedBetweenJoinAndReady()
    {
        var client = new FakeFlowClient();
        var flow = new RoomGatewaySessionFlow(client);
        var hookRan = false;
        var readiedAtHookTime = false;

        await GatewayMultiplayerSession.RunRoomFlowAsync(
            flow, "session-token", Spec, joinRoomId: null, waitForBattleStart: false, playerId: 1,
            timeout: TimeSpan.FromSeconds(5), cancellationToken: default,
            afterJoinAndBeforeReady: (f, token, roomId, timeout, ct) =>
            {
                hookRan = true;
                readiedAtHookTime = client.Readied; // hook must run before SetReady
                return Task.CompletedTask;
            });

        Assert.True(hookRan);
        Assert.False(readiedAtHookTime, "hero-pick/loading hook should run before SetReady");
    }

    [Fact]
    public async Task RunRoomFlowAsync_LoadingHook_IsInvokedBetweenReadyAndBattleStartWait()
    {
        var client = new FakeFlowClient();
        var flow = new RoomGatewaySessionFlow(client);
        var hookRan = false;
        var readiedAtHookTime = false;
        var snapshotPolledAtHookTime = false;

        var result = await GatewayMultiplayerSession.RunRoomFlowAsync(
            flow, "session-token", Spec, joinRoomId: null, waitForBattleStart: true, playerId: 1,
            timeout: TimeSpan.FromSeconds(5), cancellationToken: default,
            afterReadyAndBeforeBattleStart: (f, token, roomId, timeout, ct) =>
            {
                hookRan = true;
                readiedAtHookTime = client.Readied;           // hook must run after SetReady
                snapshotPolledAtHookTime = client.SnapshotPolled; // … and before the battle-start wait
                return Task.CompletedTask;
            });

        Assert.True(hookRan);
        Assert.True(readiedAtHookTime, "loading hook should run after SetReady");
        Assert.False(snapshotPolledAtHookTime, "loading hook should run before the battle-start wait polls");
        Assert.True(result.Started && result.Subscribed);
        Assert.Equal("battle-1", result.BattleId);
    }

    [Fact]
    public async Task RunRoomFlowAsync_JoinFallbackToCreate_FallsThroughOnJoinFailure()
    {
        var client = new FakeFlowClient { JoinSucceeds = false };
        var flow = new RoomGatewaySessionFlow(client);

        var result = await GatewayMultiplayerSession.RunRoomFlowAsync(
            flow, "session-token", Spec, joinRoomId: "room-missing", waitForBattleStart: false, playerId: 1,
            timeout: TimeSpan.FromSeconds(5), cancellationToken: default,
            joinFallbackToCreate: true);

        Assert.True(client.Created, "failed join should fall through to create");
        Assert.Equal("room-1", result.RoomId);
    }

    [Fact]
    public async Task RunRoomFlowAsync_JoinFailureWithoutFallback_Throws()
    {
        var client = new FakeFlowClient { JoinSucceeds = false };
        var flow = new RoomGatewaySessionFlow(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GatewayMultiplayerSession.RunRoomFlowAsync(
                flow, "session-token", Spec, joinRoomId: "room-missing", waitForBattleStart: false, playerId: 1,
                timeout: TimeSpan.FromSeconds(5), cancellationToken: default));
        Assert.False(client.Created);
    }

    [Fact]
    public async Task RunRoomFlowAsync_SubscribeSkipped_CompletesWithSubscribedFalse()
    {
        var client = new FakeFlowClient();
        var flow = new RoomGatewaySessionFlow(client);

        var result = await GatewayMultiplayerSession.RunRoomFlowAsync(
            flow, "session-token", Spec, joinRoomId: null, waitForBattleStart: false, playerId: 1,
            timeout: TimeSpan.FromSeconds(5), cancellationToken: default,
            subscribeStateSync: false);

        Assert.False(client.Subscribed, "subscribe must not be sent when subscribeStateSync: false");
        Assert.False(result.Subscribed);
        Assert.Equal("room-1", result.RoomId);
    }

    [Fact]
    public async Task RunRoomFlowAsync_JoinFallbackToCreate_AlsoFallsThroughOnJoinException()
    {
        // Wire-level join failures surface as thrown gateway errors (e.g. 409), not Success=false —
        // the fallback must cover both shapes.
        var client = new FakeFlowClient { JoinThrows = true };
        var flow = new RoomGatewaySessionFlow(client);

        var result = await GatewayMultiplayerSession.RunRoomFlowAsync(
            flow, "session-token", Spec, joinRoomId: "room-missing", waitForBattleStart: false, playerId: 1,
            timeout: TimeSpan.FromSeconds(5), cancellationToken: default,
            joinFallbackToCreate: true);

        Assert.True(client.Created);
        Assert.Equal("room-1", result.RoomId);
    }

    private sealed class FakeFlowClient : IRoomGatewaySessionClientBase, IRoomGatewayStateSyncSubscriptionCapability
    {
        public bool Created, Joined, Readied, Subscribed, SnapshotPolled;
        public bool JoinSucceeds = true;
        public bool JoinThrows;

        public Task<RoomGatewayCreateResult> CreateRoomAsync(RoomGatewayCreateRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        { Created = true; return Task.FromResult(new RoomGatewayCreateResult(true, "room-1", 1ul, string.Empty)); }

        public Task<RoomGatewayJoinResult> JoinRoomAsync(RoomGatewayJoinRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            Joined = true;
            if (JoinThrows && request.RoomId == "room-missing") throw new InvalidOperationException("Gateway response error. statusCode=409 message=Room not initialized.");
            if (!JoinSucceeds)
            {
                return Task.FromResult(new RoomGatewayJoinResult(false, string.Empty, 0ul, default, "Room not initialized.", string.Empty, false, RoomGatewaySessionEntryKind.TeamLobby, 0L, 0ul, 0u));
            }
            return Task.FromResult(new RoomGatewayJoinResult(true, "room-1", 1ul, new RoomGatewayWorldStartAnchor(0, 30, 0, 1.0 / 30.0), string.Empty, "battle-1", true, RoomGatewaySessionEntryKind.TeamLobby, 0L, 1ul, 1u));
        }

        public Task<RoomGatewayLeaveResult> LeaveRoomAsync(RoomGatewayLeaveRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        { Readied = true; return Task.FromResult(new RoomGatewayReadyResult(true, "battle-1", true, string.Empty)); }

        public Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(RoomGatewayRestoreRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(RoomGatewayGetSnapshotRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            SnapshotPolled = true;
            return Task.FromResult(new RoomGatewayGetSnapshotResult(
                true, "room-1", 1ul,
                new RoomGatewaySnapshot { RoomId = "room-1", Phase = RoomGatewaySessionPhase.InBattle, BattleId = "battle-1" },
                string.Empty));
        }

        public Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(RoomGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        { Subscribed = true; return Task.FromResult(new RoomGatewayStateSyncSubscriptionResult(true, string.Empty)); }
    }
}
