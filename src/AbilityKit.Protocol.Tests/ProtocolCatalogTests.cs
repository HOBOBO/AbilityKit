using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.Generated;
using Xunit;

namespace AbilityKit.Protocol.Tests;

public sealed class ProtocolCatalogTests
{
    [Fact]
    public void Validator_AcceptsDistinctOpcodesAcrossProjects()
    {
        var catalogs = new[]
        {
            Catalog("project-a.room", "project-a", Message("login.request", 100)),
            Catalog("project-b.room", "project-b", Message("login.request", 101))
        };

        var result = ProtocolCatalogValidator.Validate(catalogs);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validator_RejectsSharedTransportPushConflictAcrossProjects()
    {
        var catalogs = new[]
        {
            Catalog("project-a.room", "project-a", Push("snapshot.push", 9002)),
            Catalog("project-b.battle", "project-b", Push("catch-up.push", 9002))
        };

        var result = ProtocolCatalogValidator.Validate(catalogs);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP030");
    }

    [Fact]
    public void Validator_AllowsRequestAndResponseToShareOpCode()
    {
        var request = Request("login.request", 100, "login.response");
        var response = new ProtocolMessageDefinition(
            "login.response",
            100,
            ProtocolDirection.ServerToClient,
            ProtocolPacketKind.Response,
            "Payload",
            "protobuf");

        var result = ProtocolCatalogValidator.Validate(
            Catalog("project-a.room", "project-a", request, response));

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

    [Fact]
    public void BuiltInCatalogs_ValidateCleanly()
    {
        var result = ProtocolCatalogValidator.Validate(BuiltInProtocolCatalogs.All);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validator_RejectsCrossCatalogOpCodeConflictWithinSameProject()
    {
        var battle = Catalog("project-a.battle", "project-a", Message("login.event", 100));
        var room = Catalog("project-a.room", "project-a", Message("login.event", 100));

        var result = ProtocolCatalogValidator.Validate(new[] { battle, room });

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP030");
    }

    [Fact]
    public void Validator_RejectsUnknownDefaultAndMessageCodec()
    {
        var message = new ProtocolMessageDefinition(
            "login.event",
            100,
            ProtocolDirection.ClientToServer,
            ProtocolPacketKind.Event,
            "Payload",
            "json");
        var catalog = new ProtocolCatalogDefinition("project-a.room", "project-a", "room", 1, "json", new[] { message });

        var result = ProtocolCatalogValidator.Validate(catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP031");
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP032");
    }

    [Fact]
    public void Validator_AcceptsCodecRegisteredInOptions()
    {
        var options = new ProtocolCatalogValidationOptions(new[] { "avro" });
        var message = new ProtocolMessageDefinition(
            "login.event",
            100,
            ProtocolDirection.ClientToServer,
            ProtocolPacketKind.Event,
            "Payload",
            "avro");
        var catalog = new ProtocolCatalogDefinition("project-a.room", "project-a", "room", 1, "avro", new[] { message });

        var result = ProtocolCatalogValidator.Validate(catalog, options);

        Assert.True(result.IsValid);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validator_RejectsResponseIdOnNonRequest()
    {
        var push = new ProtocolMessageDefinition(
            "state.push",
            100,
            ProtocolDirection.ServerToClient,
            ProtocolPacketKind.Push,
            "Payload",
            "protobuf",
            responseId: "someone.response");

        var result = ProtocolCatalogValidator.Validate(Catalog("project-a.room", "project-a", push));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP025");
    }

    [Fact]
    public void Validator_RejectsOrphanResponse()
    {
        var response = new ProtocolMessageDefinition(
            "login.response",
            100,
            ProtocolDirection.ServerToClient,
            ProtocolPacketKind.Response,
            "Payload",
            "protobuf");

        var result = ProtocolCatalogValidator.Validate(Catalog("project-a.room", "project-a", response));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP026");
    }

    [Fact]
    public void Validator_RejectsResponseSharedByMultipleRequests()
    {
        var response = new ProtocolMessageDefinition(
            "login.response",
            200,
            ProtocolDirection.ServerToClient,
            ProtocolPacketKind.Response,
            "Payload",
            "protobuf");
        var catalog = Catalog(
            "project-a.room",
            "project-a",
            Request("login-a.request", 100, "login.response"),
            Request("login-b.request", 101, "login.response"),
            response);

        var result = ProtocolCatalogValidator.Validate(catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP026");
    }

    [Fact]
    public void Validator_RejectsNonOverlappingResponseSchemaVersions()
    {
        var request = new ProtocolMessageDefinition(
            "login.request",
            100,
            ProtocolDirection.ClientToServer,
            ProtocolPacketKind.Request,
            "Payload",
            "protobuf",
            responseId: "login.response",
            minimumSchemaVersion: 1,
            maximumSchemaVersion: 1);
        var response = new ProtocolMessageDefinition(
            "login.response",
            100,
            ProtocolDirection.ServerToClient,
            ProtocolPacketKind.Response,
            "Payload",
            "protobuf",
            minimumSchemaVersion: 2,
            maximumSchemaVersion: 3);

        var result = ProtocolCatalogValidator.Validate(Catalog("project-a.room", "project-a", request, response));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP027");
    }

    [Fact]
    public void MetadataRegistry_CatalogBackedViewProjectsCanonicalDefinitions()
    {
        var message = new ProtocolMessageDefinition(
            "state.push",
            101,
            ProtocolDirection.ServerToClient,
            ProtocolPacketKind.Push,
            "Project.StatePush",
            "protobuf",
            ProtocolReliability.Realtime,
            minimumSchemaVersion: 2,
            maximumSchemaVersion: 4,
            maximumPayloadBytes: 4096,
            captureSampleRate: 0.25d,
            sensitiveFields: new[] { "token" });
        var catalogs = new ProtocolCatalogRegistry();
        catalogs.Register(Catalog("project-a.room", "project-a", message));
        var metadata = ProtocolStaticRegistry.Create(
            catalogs,
            new Dictionary<string, string>
            {
                ["project-a.room/state.push"] = "room.protocol.yaml"
            });

        Assert.True(metadata.IsCatalogBacked);
        Assert.True(metadata.TryGet("project-a.room", "state.push", out var projected));
        Assert.Equal("Project.StatePush", projected!.PayloadType);
        Assert.Equal(ProtocolReliability.Realtime, projected.Reliability);
        Assert.Equal(2, projected.MinimumSchemaVersion);
        Assert.Equal(4, projected.MaximumSchemaVersion);
        Assert.Equal(4096, projected.MaximumPayloadBytes);
        Assert.Equal(0.25d, projected.CaptureSampleRate);
        Assert.Equal(new[] { "token" }, projected.SensitiveFields);
        Assert.Equal("room.protocol.yaml", projected.Source);
        Assert.Single(metadata.FindByOpCode(101));
        Assert.Single(metadata.All);
    }

    [Fact]
    public void MetadataRegistry_CatalogBackedViewTracksLaterCatalogRegistrationsAndIsReadOnly()
    {
        var catalogs = new ProtocolCatalogRegistry();
        var metadata = ProtocolStaticRegistry.Create(catalogs);

        catalogs.Register(Catalog("project-a.room", "project-a", Message("state.event", 102)));

        Assert.True(metadata.TryGet("project-a.room", "state.event", out _));
        Assert.Throws<InvalidOperationException>(() => metadata.Register(
            new ProtocolMessageMetadata(
                "project-a.room",
                "other.event",
                103,
                ProtocolDirection.ClientToServer,
                ProtocolPacketKind.Event,
                "Payload",
                "protobuf",
                ProtocolReliability.Reliable,
                null,
                string.Empty)));
    }

    [Fact]
    public void Validator_RejectsMalformedPayloadType()
    {
        var message = new ProtocolMessageDefinition(
            "login.event",
            100,
            ProtocolDirection.ClientToServer,
            ProtocolPacketKind.Event,
            "Payload Type",
            "protobuf");

        var result = ProtocolCatalogValidator.Validate(Catalog("project-a.room", "project-a", message));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, item => item.Code == "AKP033");
    }

    private static ProtocolMessageDefinition Request(string id, uint opCode, string responseId) =>
        new(
            id,
            opCode,
            ProtocolDirection.ClientToServer,
            ProtocolPacketKind.Request,
            "Payload",
            "protobuf",
            responseId: responseId);

    private static ProtocolMessageDefinition Push(string id, uint opCode) =>
        new(
            id,
            opCode,
            ProtocolDirection.ServerToClient,
            ProtocolPacketKind.Push,
            "Payload",
            "protobuf");

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
