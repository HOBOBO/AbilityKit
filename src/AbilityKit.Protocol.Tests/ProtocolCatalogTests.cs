using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.Generated;
using Xunit;

namespace AbilityKit.Protocol.Tests;

public sealed class ProtocolCatalogTests
{
    [Fact]
    public void Validator_AcceptsDistinctCatalogsAcrossProjects()
    {
        var catalogs = new[]
        {
            Catalog("project-a.room", "project-a", Message("login.request", 100)),
            Catalog("project-b.room", "project-b", Message("login.request", 100))
        };

        var result = ProtocolCatalogValidator.Validate(catalogs);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validator_RejectsDuplicateCatalogMessageIdAndTransportKey()
    {
        var duplicateMessages = Catalog(
            "project-a.room",
            "project-a",
            Message("login.request", 100),
            Message("login.request", 100));

        var result = ProtocolCatalogValidator.Validate(new[] { duplicateMessages, duplicateMessages });

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP002");
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP012");
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP019");
    }

    [Fact]
    public void Validator_RejectsInvalidResponseSchemaRangeAndSampleRate()
    {
        var request = new ProtocolMessageDefinition(
            "login.request",
            100,
            ProtocolDirection.ClientToServer,
            ProtocolPacketKind.Request,
            "LoginRequest",
            "protobuf",
            responseId: "missing.response",
            minimumSchemaVersion: 2,
            maximumSchemaVersion: 1,
            captureSampleRate: 1.1d);

        var result = ProtocolCatalogValidator.Validate(Catalog("project-a.room", "project-a", request));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP016");
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP018");
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP023");
    }

    [Fact]
    public void BuiltInRegistry_DistinguishesRequestAndResponseSharingAnOpCode()
    {
        var registry = BuiltInProtocolCatalogs.CreateRegistry();
        var requestKey = new ProtocolMessageKey(
            "abilitykit.room",
            100,
            ProtocolDirection.ClientToServer,
            ProtocolPacketKind.Request);
        var responseKey = new ProtocolMessageKey(
            "abilitykit.room",
            100,
            ProtocolDirection.ServerToClient,
            ProtocolPacketKind.Response);

        Assert.True(registry.TryGetMessage(in requestKey, out var request));
        Assert.True(registry.TryGetMessage(in responseKey, out var response));
        Assert.Equal("guest-login.request", request!.Id);
        Assert.Equal("guest-login.response", response!.Id);
    }

    [Fact]
    public void DecoderRegistry_ReturnsDecodedValueAndContainsDecoderFailures()
    {
        var registry = new ProtocolPayloadDecoderRegistry();
        registry.Register("project-a.room", "login.response", payload => payload.Count);
        registry.Register("project-a.room", "broken.response", _ => throw new InvalidDataException("invalid payload"));

        var decoded = registry.Decode(
            "project-a.room",
            "login.response",
            new ArraySegment<byte>(new byte[] { 1, 2, 3 }));
        var failed = registry.Decode("project-a.room", "broken.response", default);
        var missing = registry.Decode("project-a.room", "missing.response", default);

        Assert.True(decoded.Success);
        Assert.Equal(3, decoded.Value);
        Assert.False(failed.Success);
        Assert.Equal("invalid payload", failed.Error);
        Assert.False(missing.Success);
        Assert.Equal("No payload decoder is registered.", missing.Error);
    }

    [Fact]
    public void DecoderRegistry_TryRegister_IsAtomicAndIdempotent()
    {
        var registry = new ProtocolPayloadDecoderRegistry();

        Assert.True(registry.TryRegister("project-a.room", "login.response", _ => "first"));
        Assert.False(registry.TryRegister("project-a.room", "login.response", _ => "second"));
        Assert.True(registry.IsRegistered("project-a.room", "login.response"));
        Assert.False(registry.IsRegistered("project-a.room", "missing.response"));

        var decoded = registry.Decode("project-a.room", "login.response", default);
        Assert.True(decoded.Success);
        Assert.Equal("first", decoded.Value);
    }

    private static ProtocolCatalogDefinition Catalog(
        string catalogId,
        string projectId,
        params ProtocolMessageDefinition[] messages) =>
        new(catalogId, projectId, "room", 1, "protobuf", messages);

    private static ProtocolMessageDefinition Message(string id, uint opCode) =>
        new(
            id,
            opCode,
            ProtocolDirection.ClientToServer,
            ProtocolPacketKind.Event,
            "Payload",
            "protobuf");
}
