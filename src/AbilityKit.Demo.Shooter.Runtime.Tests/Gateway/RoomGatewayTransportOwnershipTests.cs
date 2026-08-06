using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class RoomGatewayTransportOwnershipTests
{
    [Fact]
    public async Task InjectedTransportHandlesRequestsAndRemainsOwnedByCaller()
    {
        var transport = new ObservableRoomTransport();
        var client = new RoomGatewayWireSessionClient(transport, transport);

        var result = await client.CreateRoomAsync(new RoomGatewayCreateRequest(
            "session-1",
            "cn",
            "server-a",
            "shooter",
            "Borrowed Transport Room",
            true,
            8));

        Assert.Equal(RoomGatewayOpCodes.CreateRoom, transport.LastOpCode);
        Assert.Equal("session-1", transport.LastCreateRequest.SessionToken);
        Assert.Equal("Borrowed Transport Room", transport.LastCreateRequest.Title);
        Assert.True(result.Success);
        Assert.Equal("room-borrowed", result.RoomId);
        Assert.Equal(1, transport.PushSubscriberCount);

        client.Dispose();

        Assert.False(transport.IsDisposed);
        Assert.Equal(0, transport.PushSubscriberCount);
        var response = await transport.SendRequestAsync(
            RoomGatewayOpCodes.CreateRoom,
            WireRoomGatewayBinary.Serialize(new WireCreateRoomReq()));
        Assert.True(response.Count > 0);
    }

    [Fact]
    public void InjectedPushSourceStopsUpdatingClientAfterDispose()
    {
        var transport = new ObservableRoomTransport();
        var client = new RoomGatewayWireSessionClient(transport, transport);
        var changeCount = 0;
        client.SnapshotChanged += _ => changeCount++;

        transport.PushSnapshot(1L);
        Assert.Equal(1, changeCount);
        Assert.Equal(1L, client.Current!.RoomRevision);

        client.Dispose();
        transport.PushSnapshot(2L);

        Assert.Equal(1, changeCount);
        Assert.Null(client.Current);
        Assert.Equal(0, transport.PushSubscriberCount);
    }

    [Fact]
    public async Task DisposedClientRejectsRequestsWithoutCallingBorrowedTransport()
    {
        var transport = new ObservableRoomTransport();
        var client = new RoomGatewayWireSessionClient(transport, transport);
        client.Dispose();
        var callsBeforeRequest = transport.RequestCount;

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.CreateRoomAsync(
            new RoomGatewayCreateRequest(
                "session-1",
                "cn",
                "server-a",
                "shooter",
                "Disposed Room",
                true,
                8)));
        Assert.Equal(callsBeforeRequest, transport.RequestCount);
        Assert.False(transport.IsDisposed);
    }

    private sealed class ObservableRoomTransport :
        IRoomGatewayRequestTransport,
        IRoomGatewayPushSource,
        IDisposable
    {
        private Action<uint, ArraySegment<byte>>? _serverPushReceived;

        public uint LastOpCode { get; private set; }

        public WireCreateRoomReq LastCreateRequest { get; private set; }

        public int RequestCount { get; private set; }

        public int PushSubscriberCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public event Action<uint, ArraySegment<byte>>? ServerPushReceived
        {
            add
            {
                _serverPushReceived += value;
                PushSubscriberCount++;
            }
            remove
            {
                _serverPushReceived -= value;
                PushSubscriberCount--;
            }
        }

        public Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            LastOpCode = opCode;
            RequestCount++;
            if (opCode != RoomGatewayOpCodes.CreateRoom)
            {
                throw new InvalidOperationException("Unexpected opcode: " + opCode);
            }

            LastCreateRequest = WireRoomGatewayBinary.Deserialize<WireCreateRoomReq>(payload);
            var response = new WireCreateRoomRes
            {
                Success = true,
                RoomId = "room-borrowed",
                NumericRoomId = 1002ul,
                Message = "created"
            };
            return Task.FromResult(WireRoomGatewayBinary.Serialize(in response));
        }

        public void PushSnapshot(long revision)
        {
            var push = new WireRoomStateChangedPush
            {
                RoomId = "room-borrowed",
                Snapshot = new WireRoomSnapshot
                {
                    Summary = new WireRoomSummary
                    {
                        RoomId = "room-borrowed",
                        OwnerAccountId = "owner-1"
                    },
                    RoomRevision = revision
                }
            };
            _serverPushReceived?.Invoke(
                RoomGatewayOpCodes.RoomStateChanged,
                WireRoomGatewayBinary.Serialize(in push));
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}

public sealed class RoomGatewaySessionCapabilityTests
{
    [Fact]
    public async Task ConfigureLoadoutRequiresHeroPickCapability()
    {
        var flow = new RoomGatewaySessionFlow(new BaseOnlyRoomClient());

        var error = await Assert.ThrowsAsync<NotSupportedException>(() => flow.ConfigureLoadoutAsync(
            new RoomGatewayPickHeroRequest(
                "session-1",
                "room-1",
                1,
                1,
                1,
                1,
                1,
                1,
                Array.Empty<int>())));

        Assert.Contains("hero pick", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeginLoadingRequiresStagedLoadingCapability()
    {
        var flow = new RoomGatewaySessionFlow(new BaseOnlyRoomClient());

        var error = await Assert.ThrowsAsync<NotSupportedException>(() => flow.BeginLoadingAsync(
            new RoomGatewayBeginLoadingRequest(
                "session-1",
                "room-1",
                expectedRevision: null,
                "command-1")));

        Assert.Contains("staged loading", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubscribeRequiresStateSyncCapability()
    {
        var flow = new RoomGatewaySessionFlow(new BaseOnlyRoomClient());

        var error = await Assert.ThrowsAsync<NotSupportedException>(() => flow.SubscribeStateSyncAsync(
            "session-1",
            "battle-1",
            "room-1"));

        Assert.Contains("state-sync subscription", error.Message, StringComparison.Ordinal);
    }

    private sealed class BaseOnlyRoomClient : IRoomGatewaySessionClientBase
    {
        public Task<RoomGatewayCreateResult> CreateRoomAsync(
            RoomGatewayCreateRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(default(RoomGatewayCreateResult));

        public Task<RoomGatewayJoinResult> JoinRoomAsync(
            RoomGatewayJoinRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(default(RoomGatewayJoinResult));

        public Task<RoomGatewayLeaveResult> LeaveRoomAsync(
            RoomGatewayLeaveRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(default(RoomGatewayLeaveResult));

        public Task<RoomGatewayReadyResult> SetReadyAsync(
            RoomGatewayReadyRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(default(RoomGatewayReadyResult));

        public Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(
            RoomGatewayRestoreRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(default(RoomGatewayRestoreRoomResult));

        public Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(
            RoomGatewayGetSnapshotRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(default(RoomGatewayGetSnapshotResult));
    }
}
