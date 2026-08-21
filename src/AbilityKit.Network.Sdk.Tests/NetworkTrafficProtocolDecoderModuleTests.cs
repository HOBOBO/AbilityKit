using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.Generated;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkTrafficProtocolDecoderModuleTests
{
    [Theory]
    [InlineData(RoomProtocolDecoderModule.CatalogId)]
    [InlineData(MobaProtocolDecoderModule.CatalogId)]
    [InlineData(ShooterProtocolDecoderModule.CatalogId)]
    public void Modules_CoverEveryBuiltInCatalogMessage(string catalogId)
    {
        var registry = new ProtocolPayloadDecoderRegistry();
        RoomProtocolDecoderModule.Register(registry);
        MobaProtocolDecoderModule.Register(registry);
        ShooterProtocolDecoderModule.Register(registry);
        var catalogs = BuiltInProtocolCatalogs.CreateRegistry();

        Assert.True(catalogs.TryGetCatalog(catalogId, out var catalog));
        Assert.NotNull(catalog);
        Assert.All(catalog!.Messages, message =>
            Assert.True(
                registry.IsRegistered(catalogId, message.Id),
                $"Missing decoder for {catalogId}/{message.Id}."));
    }

    [Fact]
    public void RoomModule_DecodesLoginAndCanBeRegisteredTwice()
    {
        var registry = new ProtocolPayloadDecoderRegistry();
        RoomProtocolDecoderModule.Register(registry);
        RoomProtocolDecoderModule.Register(registry);
        var value = new WireRoomGuestLoginRes
        {
            Success = true,
            SessionToken = "secret",
            AccountId = "account-1",
            Message = "ok"
        };
        var payload = WireRoomGatewayBinary.Serialize(in value);

        var result = registry.Decode(
            RoomProtocolDecoderModule.CatalogId,
            "guest-login.response",
            payload);

        Assert.True(result.Success, result.Error);
        var decoded = Assert.IsType<WireRoomGuestLoginRes>(result.Value);
        Assert.True(decoded.Success);
        Assert.Equal("secret", decoded.SessionToken);
    }

    [Fact]
    public void MobaModule_DecodesMovePayload()
    {
        var registry = new ProtocolPayloadDecoderRegistry();
        MobaProtocolDecoderModule.Register(registry);
        var payload = MobaMoveCodec.Serialize(1.5f, -2.25f);

        var result = registry.Decode(
            MobaProtocolDecoderModule.CatalogId,
            "move-input.event",
            new ArraySegment<byte>(payload));

        Assert.True(result.Success, result.Error);
        var decoded = Assert.IsType<MobaMovePayload>(result.Value);
        Assert.Equal(1.5f, decoded.X);
        Assert.Equal(-2.25f, decoded.Z);
    }

    [Fact]
    public void ShooterModule_DecodesCompatibilityAwarePackedSnapshot()
    {
        var registry = new ProtocolPayloadDecoderRegistry();
        ShooterProtocolDecoderModule.Register(registry);
        var expected = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            42ul,
            120,
            9000,
            ShooterPackedSnapshotFlags.Full,
            123u,
            0,
            Array.Empty<byte>(),
            Array.Empty<ShooterPackedComponentChunk>());
        var payload = ShooterPackedSnapshotCodec.Serialize(in expected);

        var result = registry.Decode(
            ShooterProtocolDecoderModule.CatalogId,
            "packed-state.push",
            new ArraySegment<byte>(payload));

        Assert.True(result.Success, result.Error);
        var decoded = Assert.IsType<ShooterPackedSnapshotPayload>(result.Value);
        Assert.Equal(42ul, decoded.WorldId);
        Assert.Equal(120, decoded.Frame);
        Assert.Equal(123u, decoded.StateHash);
    }
}
