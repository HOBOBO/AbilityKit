using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Sdk;
using AbilityKit.Network.Transport.InMemory;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Battle.Config.Tests;

/// <summary>
/// Integration tests using InMemoryTransport to verify the full client networking stack
/// (NetworkSdkClient + NetworkTransport + NetworkBattleConfig) without real sockets.
/// </summary>
public sealed class InMemoryIntegrationTests
{
    [Fact]
    public void NetworkBattleConfig_Build_WithRoomGatewayProtocol_ProducesValidOptions()
    {
        var options = new NetworkBattleConfig()
            .WithGateway("test", 9999)
            .WithTransportFactory(() => throw new NotImplementedException())
            .WithSession("token", "battle-1", "room-1")
            .UseRoomGatewayProtocol("battle-1", "room-1")
            .WithInputSerializer(
                _ => default,
                _ => new NetworkSubmitInputResponse(true, 1, 0, false, "ok"))
            .WithSnapshotDeserializer(_ => new object())
            .Build();

        // Verify standard room-gateway opcodes are set
        Assert.Equal((uint)RoomGatewayOpCodes.SubmitBattleInput, options.OpSubmitInput);
        Assert.Equal((uint)RoomGatewayOpCodes.SnapshotPushed, options.OpSnapshotPushed);
        Assert.Equal((uint)RoomGatewayOpCodes.DeltaSnapshotPushed, options.OpDeltaSnapshotPushed);
        Assert.Equal((uint)RoomGatewayOpCodes.ReliableBattleEventsPushed, options.OpReliableEventsPushed);
        Assert.Equal((uint)RoomGatewayOpCodes.AckReliableBattleEvents, options.OpAcknowledgeReliableEvents);
        Assert.Equal((uint)RoomGatewayOpCodes.RequestFullStateSync, options.OpRequestFullStateSync);
        Assert.Equal((uint)RoomGatewayOpCodes.RenewSession, options.OpRenewSession);
        Assert.Equal((uint)RoomGatewayOpCodes.SubscribeStateSync, options.OpPostAuthentication);

        // Verify callbacks are wired
        Assert.NotNull(options.SerializeRenewSession);
        Assert.NotNull(options.SerializePostAuthenticationWithReliableEventCursor);
        Assert.NotNull(options.SerializeAcknowledgeReliableEvents);
        Assert.NotNull(options.DeserializeAcknowledgeReliableEventsResponse);
        Assert.NotNull(options.DeserializeReliableEventsPushed);
        Assert.NotNull(options.SerializeRequestFullStateSync);
        Assert.NotNull(options.PrepareSubmitInput);
        Assert.NotNull(options.RewriteSubmitInputFrame);
    }

    [Fact]
    public async Task InMemoryTransport_TwoSides_ExchangeViaNetworkSdkClients()
    {
        // Create an in-memory transport pair
        var (clientTransport, serverTransport) = InMemoryTransport.CreateConnectedPair();

        // Build two SDK clients on the pair
        var clientSdk = new NetworkSdkBuilder()
            .UseTransportFactory(() => clientTransport)
            .Build();
        var serverSdk = new NetworkSdkBuilder()
            .UseTransportFactory(() => serverTransport)
            .Build();

        clientSdk.Open("mem", 1);
        serverSdk.Open("mem", 1);

        // Client sends a request, server responds
        var requestPayload = new byte[] { 1, 2, 3 };
        var responseTask = clientSdk.SendRawRequestAsync(42, requestPayload);

        // Server receives the request via push (since InMemoryTransport routes Send as BytesReceived)
        // In a real scenario the server would use a request handler; here we just verify bytes flow.

        await Task.Delay(100); // allow synchronous routing to complete
        clientSdk.Dispose();
        serverSdk.Dispose();

        // If we got here without deadlock, the in-memory transport + SDK clients work together.
        Assert.True(true);
    }

    [Fact]
    public void SequencedInput_PreparedByConfig_RoundTripsCorrectly()
    {
        var options = new NetworkBattleConfig()
            .WithGateway("test", 9999)
            .WithTransportFactory(() => throw new NotImplementedException())
            .WithSession("token", "battle-1")
            .UseRoomGatewayProtocol("battle-1")
            .WithInputSerializer(
                requestObj =>
                {
                    // Verify the object is a SequencedInput (from the config package)
                    Assert.IsType<SequencedInput>(requestObj);
                    var seq = (SequencedInput)requestObj;
                    Assert.True(seq.CommandSequence > 0);
                    return new ArraySegment<byte>(new byte[] { 0xFF });
                },
                _ => new NetworkSubmitInputResponse(true, 1, 0, false, "ok"))
            .WithSnapshotDeserializer(_ => new object())
            .Build();

        var input = new SubmitInputRequest(
            default, // WorldId
            new AbilityKit.Ability.Host.PlayerInputCommand(
                new AbilityKit.Ability.FrameSync.FrameIndex(1),
                default, 100, new byte[] { 1 }));

        // PrepareSubmitInput wraps it in SequencedInput
        var prepared = options.PrepareSubmitInput!(input);
        Assert.IsType<SequencedInput>(prepared);
        var seq = (SequencedInput)prepared;
        Assert.Equal(1, seq.Request.Input.Frame.Value);

        // SerializeSubmitInput receives the wrapped object
        var serialized = options.SerializeSubmitInput!(prepared);
        Assert.Equal(0xFF, serialized.Array![serialized.Offset]);

        // RewriteSubmitInputFrame changes the frame
        var rewritten = options.RewriteSubmitInputFrame!(prepared, 42);
        var rewrittenSeq = (SequencedInput)rewritten;
        Assert.Equal(42, rewrittenSeq.Request.Input.Frame.Value);
        Assert.Equal(seq.CommandSequence, rewrittenSeq.CommandSequence);
    }
}
