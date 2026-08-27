using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class ProtocolMetadataEmitterTests
{
    [Fact]
    public void EmitterProducesIndependentDescriptorsOpcodesAndSourceMap()
    {
        var catalog = new YamlProtocolSourceParser().Parse("room.protocol.yaml", """
            schemaVersion: 1
            catalogId: test.room
            projectId: test
            domain: room
            revision: 1
            defaultCodec: protobuf
            messages:
              - id: login.request
                opCode: 100
                direction: c2s
                kind: request
                payloadType: Test.LoginReq
            """);

        var output = ProtocolMetadataEmitter.Emit(
            new[] { catalog }, new[] { "room.protocol.yaml" }, "AbilityKit.Protocol.Generated", "ProtocolMetadata");

        Assert.Contains("public static class ProtocolMetadata", output);
        Assert.Contains("public const uint test_room_login_request = 100u;", output);
        Assert.Contains("[\"test.room/login.request\"] = \"room.protocol.yaml\"", output);
        Assert.Contains("ProtocolStaticRegistry.Create(Messages)", output);
        Assert.Contains("ProtocolStaticRegistry.Create(catalogs, SourceMap)", output);
        Assert.DoesNotContain("BuiltInProtocolCatalogs", output);
        Assert.Equal(output, ProtocolMetadataEmitter.Emit(
            new[] { catalog }, new[] { "room.protocol.yaml" }, "AbilityKit.Protocol.Generated", "ProtocolMetadata"));
    }

    [Fact]
    public void StaticRegistrySupportsGeneratedMetadataLookup()
    {
        var registry = ProtocolStaticRegistry.Create(new[]
        {
            new ProtocolMessageMetadata(
                "test.room", "login.request", 100, ProtocolDirection.ClientToServer,
                ProtocolPacketKind.Request, "Test.LoginReq", "protobuf", ProtocolReliability.Reliable,
                null, "room.protocol.yaml")
        });

        Assert.True(registry.TryGet("test.room", "login.request", out var metadata));
        Assert.Equal("room.protocol.yaml", metadata!.Source);
        Assert.Single(registry.FindByOpCode(100));
    }
}
