using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Generated;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkProtocolAgentTests
{
    [Fact]
    public async Task CatalogRequest_EncodesAndDecodesWithTheMappedOpcode()
    {
        var transport = new TestTransport();
        var response = new WireRoomGuestLoginRes { Success = true, SessionToken = "session-1" };
        transport.Response = WireRoomGatewayBinary.Serialize(in response);
        using var agent = new NetworkProtocolAgent(transport, new RoomCodec());
        var catalog = BuiltInProtocolCatalogs.CreateRegistry();
        var request = new WireRoomGuestLoginReq { GuestId = "guest-1" };
        using var deadline = new CancellationTokenSource();
        var timeout = TimeSpan.FromSeconds(2);

        var result = await agent.RequestAsync<WireRoomGuestLoginReq, WireRoomGuestLoginRes>(
            catalog, "abilitykit.room", "guest-login.request", request, timeout, deadline.Token);

        Assert.True(result.Success);
        Assert.Equal("session-1", result.SessionToken);
        Assert.Equal(WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginReq>(transport.Request).GuestId,
            request.GuestId);
        Assert.Equal(100u, transport.OpCode);
        Assert.Equal(timeout, transport.Timeout);
        Assert.Equal(deadline.Token, transport.CancellationToken);
    }

    [Fact]
    public async Task CatalogRequest_RejectsMismatchedResponseBeforeSending()
    {
        var transport = new TestTransport();
        using var agent = new NetworkProtocolAgent(transport, new RoomCodec());
        var catalog = BuiltInProtocolCatalogs.CreateRegistry();
        var request = new WireRoomGuestLoginReq { GuestId = "guest-1" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.RequestAsync<WireRoomGuestLoginReq, WireRoomAccountLoginRes>(
                catalog, "abilitykit.room", "guest-login.request", request));
        Assert.Equal(0, transport.SendCount);
    }

    [Fact]
    public void PushSubscriptions_AreScopedAndIndependent()
    {
        var transport = new TestTransport();
        using var agent = new NetworkProtocolAgent(transport, new RoomCodec());
        var catalog = BuiltInProtocolCatalogs.CreateRegistry();
        var first = 0;
        var second = 0;
        var firstSubscription = agent.Subscribe<WireRoomFramePush>(
            catalog, "abilitykit.room", "frame-sync-frame.push", _ => first++);
        using var secondSubscription = agent.Subscribe<WireRoomFramePush>(
            catalog, "abilitykit.room", "frame-sync-frame.push", _ => second++);
        var push = new WireRoomFramePush();
        var payload = WireRoomGatewayBinary.Serialize(in push);

        Assert.True(catalog.TryGetMessage("abilitykit.room", "frame-sync-frame.push", out var message));
        var opCode = message!.OpCode;
        transport.Push(opCode, payload);
        firstSubscription.Dispose();
        firstSubscription.Dispose();
        transport.Push(opCode, payload);

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        agent.Dispose();
        transport.Push(opCode, payload);
        Assert.Equal(2, second);
        Assert.Equal(1, transport.SubscribeCount);
        Assert.Equal(1, transport.UnsubscribeCount);
    }

    [Fact]
    public void FailedPushHandler_DoesNotBlockOtherSubscribers()
    {
        var transport = new TestTransport();
        using var agent = new NetworkProtocolAgent(transport, new RoomCodec());
        Exception? failure = null;
        var delivered = 0;
        agent.PushDispatchFailed += (_, exception) => failure = exception;
        using var failing = agent.Subscribe<WireRoomFramePush>(9006u,
            _ => throw new InvalidOperationException("handler failed"));
        using var succeeding = agent.Subscribe<WireRoomFramePush>(9006u, _ => delivered++);
        var push = new WireRoomFramePush();

        transport.Push(9006u, WireRoomGatewayBinary.Serialize(in push));

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(1, delivered);
    }

    private sealed class RoomCodec : INetworkProtocolCodec
    {
        public ArraySegment<byte> Serialize<T>(in T value) => WireRoomGatewayBinary.Serialize(in value);
        public T Deserialize<T>(ArraySegment<byte> payload) => WireRoomGatewayBinary.Deserialize<T>(payload);
    }

    private sealed class TestTransport : INetworkProtocolTransport
    {
        private event Action<uint, ArraySegment<byte>>? _push;
        public int SubscribeCount { get; private set; }
        public int UnsubscribeCount { get; private set; }
        public int SendCount { get; private set; }
        public uint OpCode { get; private set; }
        public ArraySegment<byte> Request { get; private set; }
        public ArraySegment<byte> Response { get; set; }
        public TimeSpan? Timeout { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public event Action<uint, ArraySegment<byte>>? ServerPushReceived
        {
            add { SubscribeCount++; _push += value; }
            remove { UnsubscribeCount++; _push -= value; }
        }

        public Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            SendCount++;
            OpCode = opCode;
            Request = payload;
            Timeout = timeout;
            CancellationToken = cancellationToken;
            return Task.FromResult(Response);
        }

        public void Push(uint opCode, ArraySegment<byte> payload) => _push?.Invoke(opCode, payload);
    }
}
