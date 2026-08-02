using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Orleans.Gateway.Handlers;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Protocol.Room;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Orleans.Hosting;
using Orleans.TestingHost;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

[CollectionDefinition(Name)]
public sealed class GatewayOrleansCollection : ICollectionFixture<GatewayOrleansFixture>
{
    public const string Name = "Gateway Orleans integration";
}

public sealed class GatewayOrleansFixture : IAsyncLifetime
{
    public GatewayOrleansFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloConfigurator>();
        Cluster = builder.Build();
    }

    public TestCluster Cluster { get; }

    public Task InitializeAsync() => Cluster.DeployAsync();

    public Task DisposeAsync() => Cluster.StopAllSilosAsync();

    private sealed class SiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<ISessionStateStore, InMemorySessionStateStore>();
                services.AddSingleton<IRoomStateStore, InMemoryRoomStateStore>();
            });
        }
    }
}

[Collection(GatewayOrleansCollection.Name)]
public sealed class RoomMembershipIntegrationTests
{
    private readonly IClusterClient _client;

    public RoomMembershipIntegrationTests(GatewayOrleansFixture fixture)
    {
        _client = fixture.Cluster.Client;
    }

    [Fact]
    public async Task LeaveHandler_WhenRequestedRoomDoesNotMatch_KeepsAuthoritativeMembership()
    {
        var accountId = NewId("account");
        var (token, roomId) = await CreateMappedRoomAsync(accountId);
        var requestedRoomId = NewId("other-room");
        var context = new GatewaySessionContext(1) { AccountId = accountId, RoomId = roomId };

        var response = await CreateLeaveHandler().HandleAsync(
            NewLeaveRequest(token, requestedRoomId), context, default);
        var wire = DeserializeResponse(response);

        Assert.False(wire.Success);
        Assert.Equal((int)RoomOperationErrorCode.NotMember, wire.ErrorCode);
        Assert.Equal(roomId, context.RoomId);
        Assert.Equal(roomId, await Mapping.TryGetAccountRoomAsync(accountId));
        Assert.Contains(accountId, (await _client.GetGrain<IRoomGrain>(roomId).GetSnapshotAsync()).Members);
    }

    [Fact]
    public async Task LeaveHandler_WhenRepeated_IsIdempotent()
    {
        var accountId = NewId("account");
        var (token, roomId) = await CreateMappedRoomAsync(accountId);
        var handler = CreateLeaveHandler();
        var context = new GatewaySessionContext(2) { AccountId = accountId, RoomId = roomId };
        var request = NewLeaveRequest(token, roomId);

        var first = DeserializeResponse(await handler.HandleAsync(request, context, default));
        var second = DeserializeResponse(await handler.HandleAsync(request, context, default));

        Assert.True(first.Success);
        Assert.True(first.Applied);
        Assert.True(second.Success);
        Assert.False(second.Applied);
        Assert.Null(await Mapping.TryGetAccountRoomAsync(accountId));
    }

    [Fact]
    public async Task LeaveHandler_WhenMemberAlreadyMissing_ClearsStaleMapping()
    {
        var ownerId = NewId("owner");
        var staleAccountId = NewId("stale");
        var (_, roomId) = await CreateMappedRoomAsync(ownerId);
        var staleSession = await CreateSessionAsync(staleAccountId);
        await Mapping.BindAccountRoomAsync(staleAccountId, roomId);
        var context = new GatewaySessionContext(3) { AccountId = staleAccountId, RoomId = roomId };

        var wire = DeserializeResponse(await CreateLeaveHandler().HandleAsync(
            NewLeaveRequest(staleSession, roomId), context, default));

        Assert.True(wire.Success);
        Assert.False(wire.Applied);
        Assert.Null(await Mapping.TryGetAccountRoomAsync(staleAccountId));
        Assert.Contains(ownerId, (await _client.GetGrain<IRoomGrain>(roomId).GetSnapshotAsync()).Members);
    }

    [Fact]
    public async Task ConnectionClosed_WhenOwnerIsOnlyMember_ClosesAndRemovesRoom()
    {
        var accountId = NewId("owner");
        var (_, roomId) = await CreateMappedRoomAsync(accountId);
        var transport = await CreateStartedTransportAsync(accountId, roomId, connectionId: 10);

        try
        {
            transport.Handler.OnClosed(10);
            await WaitUntilAsync(async () => await Mapping.TryGetAccountRoomAsync(accountId) == null);

            var rooms = await Directory.ListRoomsAsync(
                new AbilityKit.Orleans.Contracts.Rooms.ListRoomsRequest(
                    accountId,
                    "local",
                    "integration",
                    0,
                    100,
                    null));
            Assert.DoesNotContain(rooms.Rooms, room => room.RoomId == roomId);
        }
        finally
        {
            await transport.BackgroundTasks.StopAsync(default);
            transport.BackgroundTasks.Dispose();
        }
    }

    [Fact]
    public async Task ConnectionClosed_WhenOwnerHasPeer_TransfersOwnershipAndKeepsRoom()
    {
        var ownerId = NewId("owner");
        var peerId = NewId("peer");
        var (_, roomId) = await CreateMappedRoomAsync(ownerId);
        await _client.GetGrain<IRoomGrain>(roomId).JoinAsync(peerId);
        await Mapping.BindAccountRoomAsync(peerId, roomId);
        var transport = await CreateStartedTransportAsync(ownerId, roomId, connectionId: 11);

        try
        {
            transport.Handler.OnClosed(11);
            await WaitUntilAsync(async () => await Mapping.TryGetAccountRoomAsync(ownerId) == null);

            var snapshot = await _client.GetGrain<IRoomGrain>(roomId).GetSnapshotAsync();
            Assert.Equal(new[] { peerId }, snapshot.Members);
            Assert.Equal(peerId, snapshot.Summary.OwnerAccountId);
            Assert.Equal(RoomPhase.Lobby, snapshot.Phase);
            Assert.Equal(roomId, await Mapping.TryGetAccountRoomAsync(peerId));
        }
        finally
        {
            await transport.BackgroundTasks.StopAsync(default);
            transport.BackgroundTasks.Dispose();
        }
    }

    [Fact]
    public async Task ConnectionClosed_WhenAccountAlreadyRebound_DoesNotRemoveCurrentMembership()
    {
        var accountId = NewId("owner");
        var (_, roomId) = await CreateMappedRoomAsync(accountId);
        var registry = new GatewaySessionRegistry();
        var oldSession = new TestTransportSession(20, accountId, roomId);
        var newSession = new TestTransportSession(21, accountId, roomId);
        var transport = await CreateStartedTransportAsync(registry, oldSession);

        try
        {
            transport.Handler.RegisterSession(newSession);
            registry.BindAccount(accountId, newSession.ConnectionId);
            transport.Handler.OnClosed(oldSession.ConnectionId);
            await Task.Delay(100);

            Assert.Equal(roomId, await Mapping.TryGetAccountRoomAsync(accountId));
            Assert.Contains(accountId, (await _client.GetGrain<IRoomGrain>(roomId).GetSnapshotAsync()).Members);
        }
        finally
        {
            await transport.BackgroundTasks.StopAsync(default);
            transport.BackgroundTasks.Dispose();
        }
    }

    private IRoomIdMappingGrain Mapping =>
        _client.GetGrain<IRoomIdMappingGrain>(GatewayGrainKeys.Global);

    private IRoomDirectoryGrain Directory =>
        _client.GetGrain<IRoomDirectoryGrain>("local:integration");

    private LeaveRoomHandler CreateLeaveHandler()
    {
        var membership = new GatewayRoomMembershipService(
            _client,
            NullLogger<GatewayRoomMembershipService>.Instance);
        return new LeaveRoomHandler(_client, membership);
    }

    private async Task<(string Token, string RoomId)> CreateMappedRoomAsync(string accountId)
    {
        var token = await CreateSessionAsync(accountId);
        var created = await Directory.CreateRoomAsync(
            new AbilityKit.Orleans.Contracts.Rooms.CreateRoomRequest(
            accountId,
            "local",
            "integration",
            GameplayRoomTypes.Default,
            "Integration room",
            true,
            4,
            null));
        await Mapping.BindAccountRoomAsync(accountId, created.RoomId);
        return (token, created.RoomId);
    }

    private async Task<string> CreateSessionAsync(string accountId)
    {
        var response = await _client.GetGrain<ISessionGrain>(GatewayGrainKeys.Global)
            .CreateSessionForAccountAsync(new CreateSessionForAccountRequest(accountId, 3600, false));
        return response.SessionToken;
    }

    private async Task<TransportHarness> CreateStartedTransportAsync(
        string accountId,
        string roomId,
        long connectionId)
    {
        var registry = new GatewaySessionRegistry();
        var session = new TestTransportSession(connectionId, accountId, roomId);
        registry.BindAccount(accountId, connectionId);
        return await CreateStartedTransportAsync(registry, session);
    }

    private async Task<TransportHarness> CreateStartedTransportAsync(
        GatewaySessionRegistry registry,
        TestTransportSession session)
    {
        var backgroundTasks = new GatewayBackgroundTaskQueue(
            NullLogger<GatewayBackgroundTaskQueue>.Instance);
        await backgroundTasks.StartAsync(default);
        var membership = new GatewayRoomMembershipService(
            _client,
            NullLogger<GatewayRoomMembershipService>.Instance);
        var frameSubscriptions = new GatewayFrameSyncSubscriptionManager(
            _client,
            registry,
            backgroundTasks,
            NullLogger<GatewayFrameSyncSubscriptionManager>.Instance);
        var stateSubscriptions = new GatewayStateSyncPushSubscriptionManager(
            _client,
            registry,
            backgroundTasks,
            NullLogger<GatewayStateSyncPushSubscriptionManager>.Instance);
        var handler = new GatewayTransportHandler(
            registry,
            router: null!,
            membership,
            backgroundTasks,
            frameSubscriptions,
            stateSubscriptions,
            NullLogger<GatewayTransportHandler>.Instance);
        handler.RegisterSession(session);
        return new TransportHarness(handler, backgroundTasks);
    }

    private static GatewayRequest NewLeaveRequest(string token, string roomId)
    {
        var wire = new WireLeaveRoomReq
        {
            SessionToken = token,
            RoomId = roomId,
            CommandId = NewId("leave")
        };
        return new GatewayRequest(1, WireRoomGatewayBinary.Serialize(in wire).ToArray());
    }

    private static WireRoomOperationRes DeserializeResponse(GatewayResponse response)
    {
        Assert.Equal(GatewayStatusCode.Success, response.StatusCode);
        return WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(
            new ArraySegment<byte>(response.Payload));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private sealed record TransportHarness(
        GatewayTransportHandler Handler,
        GatewayBackgroundTaskQueue BackgroundTasks);

    private sealed class TestTransportSession : IGatewayTransportSession
    {
        public TestTransportSession(long connectionId, string accountId, string roomId)
        {
            ConnectionId = connectionId;
            Context = new GatewaySessionContext(connectionId)
            {
                AccountId = accountId,
                RoomId = roomId
            };
        }

        public long ConnectionId { get; }
        public string TransportName => "IntegrationTest";
        public GatewaySessionContext Context { get; }
        public bool IsConnected => true;

        public Task SendResponseAsync(
            uint opCode,
            uint seq,
            byte[] payload,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendServerPushAsync(
            uint opCode,
            byte[] payload,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
