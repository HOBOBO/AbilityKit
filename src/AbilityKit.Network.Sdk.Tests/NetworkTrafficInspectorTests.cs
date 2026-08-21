using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Network.Sdk.Observability;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkTrafficInspectorTests
{
    [Fact]
    public void Inspect_ResolvesAndDecodesCompletePayload()
    {
        var catalogs = new ProtocolCatalogRegistry();
        catalogs.Register(new ProtocolCatalogDefinition(
            "project.battle", "project", "battle", 1, "memorypack",
            new[] { new ProtocolMessageDefinition(
                "cast.request", 901, ProtocolDirection.ClientToServer,
                ProtocolPacketKind.Request, "CastRequest", "memorypack") }));
        var decoders = new ProtocolPayloadDecoderRegistry();
        decoders.Register("project.battle", "cast.request", payload => payload.Array![payload.Offset]);
        var buffer = new NetworkTrafficRingBuffer(4);
        var probe = new NetworkTrafficProbeMiddleware(
            new NetworkTrafficConnectionContext("battle", 1, "battle", "project.battle", "host:1", "tcp"),
            buffer,
            maximumPayloadPreviewBytes: 8);

        probe.OnOutbound(null!, new NetworkPacketHeader(NetworkPacketFlags.Request, 901, 1, 1),
            new ArraySegment<byte>(new byte[] { 7 }), static (_, _) => { });

        var row = new NetworkTrafficInspector(catalogs, decoders).Inspect(buffer)[0];
        Assert.True(row.IsKnown);
        Assert.False(row.IsAmbiguous);
        Assert.Equal("cast.request", row.Message!.Id);
        Assert.True(row.Decode.Success);
        Assert.Equal((byte)7, row.Decode.Value);
    }

    [Fact]
    public void Inspect_ReportsAmbiguousIdentity()
    {
        var catalogs = new ProtocolCatalogRegistry();
        catalogs.Register(new ProtocolCatalogDefinition(
            "project.room", "project", "room", 1, "memorypack",
            new[] {
                new ProtocolMessageDefinition("request", 77, ProtocolDirection.ClientToServer,
                    ProtocolPacketKind.Request, "Request", "memorypack"),
                new ProtocolMessageDefinition("event", 77, ProtocolDirection.ClientToServer,
                    ProtocolPacketKind.Event, "Event", "memorypack")
            }));
        var buffer = new NetworkTrafficRingBuffer(4);
        var probe = new NetworkTrafficProbeMiddleware(
            new NetworkTrafficConnectionContext("room", 1, "room", "project.room", "host:1", "tcp"),
            buffer,
            maximumPayloadPreviewBytes: 1);
        probe.OnOutbound(null!, new NetworkPacketHeader(NetworkPacketFlags.None, 77, 1, 2),
            new ArraySegment<byte>(new byte[] { 1, 2 }), static (_, _) => { });

        var row = new NetworkTrafficInspector(catalogs, new ProtocolPayloadDecoderRegistry())
            .Inspect(buffer)[0];
        Assert.True(row.IsAmbiguous);
        Assert.False(row.Decode.Success);
        Assert.Contains("ambiguous", row.Decode.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_DoesNotDecodeTruncatedPayload()
    {
        var catalogs = new ProtocolCatalogRegistry();
        catalogs.Register(new ProtocolCatalogDefinition(
            "project.battle", "project", "battle", 1, "memorypack",
            new[] { new ProtocolMessageDefinition(
                "cast.request", 901, ProtocolDirection.ClientToServer,
                ProtocolPacketKind.Request, "CastRequest", "memorypack") }));
        var decodeCount = 0;
        var decoders = new ProtocolPayloadDecoderRegistry();
        decoders.Register("project.battle", "cast.request", _ => ++decodeCount);
        var buffer = new NetworkTrafficRingBuffer(4);
        var probe = new NetworkTrafficProbeMiddleware(
            new NetworkTrafficConnectionContext("battle", 1, "battle", "project.battle", "host:1", "tcp"),
            buffer,
            maximumPayloadPreviewBytes: 1);

        probe.OnOutbound(null!, new NetworkPacketHeader(NetworkPacketFlags.Request, 901, 1, 2),
            new ArraySegment<byte>(new byte[] { 7, 8 }), static (_, _) => { });

        var row = new NetworkTrafficInspector(catalogs, decoders).Inspect(buffer)[0];
        Assert.True(row.IsKnown);
        Assert.False(row.Decode.Success);
        Assert.Contains("truncated", row.Decode.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, decodeCount);
    }
}
