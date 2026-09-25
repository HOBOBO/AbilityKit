using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class RoomFrameSyncWireTests
{
    [Fact]
    public void RoomPushCarriesAuthoritativeInputsAndHash()
    {
        var source = new FramePushedEvent(7, 9, 12,
            new List<FrameInputItem> { new(3, 1, new byte[] { 1, 2, 3 }) }, 0x12345678);
        var payload = GatewayFrameSyncSubscriptionManager.SerializeRoomFrame(source);
        var wire = WireRoomGatewayBinary.Deserialize<WireRoomFramePush>(new ArraySegment<byte>(payload));

        Assert.Equal((ulong)7, wire.RoomId);
        Assert.Equal((ulong)9, wire.WorldId);
        Assert.Equal(12, wire.Frame);
        Assert.Equal(0x12345678u, wire.StateHash);
        Assert.Single(wire.Inputs);
        Assert.Equal(new byte[] { 1, 2, 3 }, wire.Inputs[0].Payload);
    }
}
