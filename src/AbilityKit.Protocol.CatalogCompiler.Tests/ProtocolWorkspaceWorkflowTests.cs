using System.Text.Json;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class ProtocolWorkspaceWorkflowTests
{
    [Fact]
    public void WorkspaceProjection_IsDeterministicAndCarriesProjectOwnership()
    {
        var catalog = Catalog();
        var schema = Schema();
        var workspace = ProtocolWorkspaceEmitter.Create(
            new[] { catalog },
            new[] { "catalogs/test.protocol.yaml" },
            new[] { schema },
            new[] { "schemas/TestPayload.wire.yaml" });

        Assert.Equal(new[] { "project.test" }, workspace.Projects);
        Assert.Equal("project.test", workspace.WireSchemas[0].ProjectId);
        Assert.Equal("battle", workspace.WireSchemas[0].GroupId);
        Assert.Equal("Project.Test.Protocol.TestPayload", workspace.WireSchemas[0].QualifiedType);
        Assert.Empty(workspace.Diagnostics);
        Assert.Equal(
            ProtocolWorkspaceEmitter.Serialize(workspace),
            ProtocolWorkspaceEmitter.Serialize(workspace));
    }

    [Fact]
    public void CatalogYaml_RoundTripsResolvedEditorValues()
    {
        var workspace = ProtocolWorkspaceEmitter.Create(
            new[] { Catalog() },
            new[] { "test.protocol.yaml" },
            Array.Empty<WireSchemaIr>(),
            Array.Empty<string>());
        var editorJson = JsonSerializer.Serialize(workspace.Catalogs[0], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var editorCatalog = ProtocolWorkspaceEmitter.DeserializeCatalog(editorJson);
        var yaml = ProtocolYamlEmitter.EmitCatalog(editorCatalog);
        var parsed = new YamlProtocolSourceParser().Parse("test.protocol.yaml", yaml);

        Assert.Contains("# yaml-language-server: $schema=../protocol-catalog.schema.json", yaml);
        Assert.Equal("project.test.battle", parsed.CatalogId);
        Assert.Equal(0.25d, parsed.Messages[0].CaptureSampleRate);
        Assert.Equal(4096, parsed.Messages[0].MaximumPayloadBytes);
        Assert.Equal("memorypack", parsed.Messages[0].Codec);
    }

    [Fact]
    public void WireSchemaYaml_RoundTripsOwnershipFieldsAndReservedIds()
    {
        var workspace = ProtocolWorkspaceEmitter.Create(
            Array.Empty<ProtocolCatalogIr>(),
            Array.Empty<string>(),
            new[] { Schema() },
            new[] { "TestPayload.wire.yaml" });
        var yaml = ProtocolYamlEmitter.EmitWireSchemaDocument(Document(Schema()));
        var parsed = new YamlWireSchemaParser().Parse("TestPayload.wire.yaml", yaml);

        Assert.Equal("project.test", parsed.ProjectId);
        Assert.Equal("Project.Test.Protocol", parsed.TargetNamespace);
        Assert.Equal(new uint[] { 3 }, parsed.ReservedIds);
        Assert.True(parsed.Fields[1].IsOptional);
        Assert.True(parsed.Fields[1].IsArray);
        Assert.Equal("Project.Test.Protocol.NestedPayload", parsed.Fields[2].TypeName);
    }

    [Fact]
    public void WireSchemaYaml_RoundTripsSequentialStructGenerationOptions()
    {
        var editorSchema = new ProtocolWorkspaceWireSchema
        {
            SourcePath = "Legacy.wire.yaml",
            SchemaVersion = WireSchemaFormatVersions.Current,
            ProjectId = "project.test",
            GroupId = "battle",
            Namespace = "Project.Test.Protocol",
            Type = "Legacy",
            MemoryPackMode = "sequential",
            Declaration = "struct",
            MemberStyle = "field",
            Fields = new[]
            {
                new ProtocolWorkspaceWireField { Id = 0, Name = "value", ScalarType = "int32" }
            }
        };

        var schema = ProtocolWorkspaceEmitter.ToWireSchemaIr(editorSchema);
        var yaml = ProtocolYamlEmitter.EmitWireSchemaDocument(new WireSchemaDocumentIr(
            WireSchemaFormatVersions.Current,
            schema.ProjectId,
            schema.TargetNamespace,
            schema.GroupId,
            schema.MemoryPackMode,
            schema.DeclarationKind,
            schema.MemberStyle,
            new[] { schema }));
        var parsed = new YamlWireSchemaParser().Parse("Legacy.wire.yaml", yaml);

        Assert.Contains("memoryPackMode: sequential", yaml);
        Assert.Contains("declaration: struct", yaml);
        Assert.Contains("memberStyle: field", yaml);
        Assert.Equal(WireMemoryPackMode.Sequential, parsed.MemoryPackMode);
        Assert.Equal(WireDeclarationKind.Struct, parsed.DeclarationKind);
        Assert.Equal(WireMemberStyle.Field, parsed.MemberStyle);
        Assert.Equal(0u, parsed.Fields[0].Id);
    }

    [Fact]
    public void WireSchemaYaml_RoundTripsExternalTypeOwnership()
    {
        var editorSchema = new ProtocolWorkspaceWireSchema
        {
            ProjectId = "project.test",
            GroupId = "battle",
            Namespace = "Project.Test.Protocol",
            Type = "Payload",
            MemoryPackMode = "sequential",
            Declaration = "struct",
            MemberStyle = "field",
            Fields = new[]
            {
                new ProtocolWorkspaceWireField
                {
                    Id = 0,
                    Name = "position",
                    TypeName = "Shared.Math.Vec3",
                    External = true
                }
            }
        };

        var schema = ProtocolWorkspaceEmitter.ToWireSchemaIr(editorSchema);
        var yaml = ProtocolYamlEmitter.EmitWireSchemaDocument(new WireSchemaDocumentIr(
            WireSchemaFormatVersions.Current,
            schema.ProjectId,
            schema.TargetNamespace,
            schema.GroupId,
            schema.MemoryPackMode,
            schema.DeclarationKind,
            schema.MemberStyle,
            new[] { schema }));
        var parsed = new YamlWireSchemaParser().Parse("Payload.wire.yaml", yaml);

        Assert.Contains("external: true", yaml);
        Assert.True(parsed.Fields[0].IsExternalReference);
    }

    [Fact]
    public void GroupedWireSchemaYaml_RoundTripsDefaultsAndTypeOverrides()
    {
        var command = new WireSchemaIr(
            2,
            "Command",
            new[] { new WireFieldIr(0, "value", "int32", false, false) },
            Array.Empty<uint>(),
            "project.test",
            "Project.Test.Protocol",
            WireMemoryPackMode.Sequential,
            WireDeclarationKind.Struct,
            WireMemberStyle.Field,
            "battle");
        var payload = Schema();
        var groupedPayload = new WireSchemaIr(
            2,
            payload.Type,
            payload.Fields,
            payload.ReservedIds,
            payload.ProjectId,
            payload.TargetNamespace,
            payload.MemoryPackMode,
            payload.DeclarationKind,
            payload.MemberStyle,
            "battle");
        var document = new WireSchemaDocumentIr(
            2,
            "project.test",
            "Project.Test.Protocol",
            "battle",
            WireMemoryPackMode.Sequential,
            WireDeclarationKind.Struct,
            WireMemberStyle.Field,
            new[] { command, groupedPayload });

        var yaml = ProtocolYamlEmitter.EmitWireSchemaDocument(document);
        var parsed = new YamlWireSchemaParser().ParseDocument("group.wire.yaml", yaml);

        Assert.Contains("schemaVersion: 2", yaml);
        Assert.Contains("groupId: battle", yaml);
        Assert.Contains("types:", yaml);
        Assert.Contains("name: Command", yaml);
        Assert.Equal(2, parsed.Schemas.Count);
        Assert.Equal(WireMemoryPackMode.Sequential, parsed.Schemas[0].MemoryPackMode);
        Assert.Equal(WireMemoryPackMode.VersionTolerant, parsed.Schemas[1].MemoryPackMode);
        Assert.Equal(WireDeclarationKind.Class, parsed.Schemas[1].DeclarationKind);
        Assert.Equal(WireMemberStyle.Property, parsed.Schemas[1].MemberStyle);
    }

    [Fact]
    public void MemoryPackEmitter_UsesStableFieldIdsAndVersionTolerantShape()
    {
        var schema = Schema();
        var output = MemoryPackWireEmitter.Emit(schema);

        Assert.Equal(output, MemoryPackWireEmitter.Emit(schema));
        Assert.Contains("[MemoryPackable(GenerateType.VersionTolerant)]", output);
        Assert.Contains("namespace Project.Test.Protocol", output);
        Assert.Contains("[MemoryPackOrder(1)]", output);
        Assert.Contains("public int Count { get; set; }", output);
        Assert.Contains("[MemoryPackOrder(2)]", output);
        Assert.Contains("public string[]? Labels { get; set; }", output);
        Assert.Contains("public Project.Test.Protocol.NestedPayload[] Children { get; set; }", output);
        Assert.Contains("Array.Empty<Project.Test.Protocol.NestedPayload>()", output);
    }

    [Fact]
    public void MemoryPackEmitter_CanPreserveLegacySequentialStructShape()
    {
        var schema = new WireSchemaIr(
            WireSchemaFormatVersions.Current,
            "LegacyPayload",
            new[] { new WireFieldIr(0, "value", "int32", false, false) },
            Array.Empty<uint>(),
            "project.test",
            "Project.Test.Protocol",
            WireMemoryPackMode.Sequential,
            WireDeclarationKind.Struct,
            WireMemberStyle.Field,
            "battle");

        var output = MemoryPackWireEmitter.Emit(schema);

        Assert.Contains("    [MemoryPackable]", output);
        Assert.DoesNotContain("GenerateType.VersionTolerant", output);
        Assert.Contains("public partial struct LegacyPayload", output);
        Assert.Contains("[MemoryPackOrder(0)]", output);
        Assert.Contains("public int Value;", output);
    }

    [Fact]
    public void ExportPlanner_IncludesNestedSchemasAndIgnoresSystemPrimitives()
    {
        var command = new WireSchemaIr(
            WireSchemaFormatVersions.Current,
            "Command",
            new[] { new WireFieldIr(0, "value", "int32", false, false) },
            Array.Empty<uint>(),
            "project.test",
            "Project.Test.Protocol",
            groupId: "battle");
        var payload = new WireSchemaIr(
            WireSchemaFormatVersions.Current,
            "Payload",
            new[]
            {
                new WireFieldIr(
                    1,
                    "commands",
                    string.Empty,
                    true,
                    false,
                    "Project.Test.Protocol.Command")
            },
            Array.Empty<uint>(),
            "project.test",
            "Project.Test.Protocol",
            groupId: "battle");

        var plan = MemoryPackExportPlanner.Create(
            new[] { "Project.Test.Protocol.Payload", "System.UInt64" },
            new[] { command, payload },
            includeUnreferenced: false);

        Assert.Equal(new[] { command, payload }, plan.Schemas);
        Assert.Empty(plan.MissingTypes);
    }

    [Fact]
    public void ExportPlanner_DoesNotGenerateExplicitExternalReferences()
    {
        var payload = new WireSchemaIr(
            WireSchemaFormatVersions.Current,
            "Payload",
            new[]
            {
                new WireFieldIr(
                    0,
                    "position",
                    string.Empty,
                    false,
                    false,
                    "Shared.Math.Vec3",
                    isExternalReference: true)
            },
            Array.Empty<uint>(),
            "project.test",
            "Project.Test.Protocol",
            groupId: "battle");

        var plan = MemoryPackExportPlanner.Create(
            new[] { "Project.Test.Protocol.Payload" },
            new[] { payload },
            includeUnreferenced: false);

        Assert.Equal(new[] { payload }, plan.Schemas);
        Assert.Empty(plan.MissingTypes);
    }

    private static ProtocolCatalogIr Catalog() => new(
        "project.test.battle",
        "project.test",
        "battle",
        1,
        "memorypack",
        new[]
        {
            new ProtocolMessageIr(
                "state.push",
                100,
                IrDirection.ServerToClient,
                IrPacketKind.Push,
                "Project.Test.Protocol.TestPayload",
                "memorypack",
                IrReliability.Realtime,
                null,
                1,
                1,
                4096,
                0.25d)
        });

    private static WireSchemaIr Schema() => new(
        WireSchemaFormatVersions.Current,
        "TestPayload",
        new[]
        {
            new WireFieldIr(1, "count", "int32", false, false),
            new WireFieldIr(2, "labels", "string", true, true),
            new WireFieldIr(
                4,
                "children",
                string.Empty,
                true,
                false,
                "Project.Test.Protocol.NestedPayload")
        },
        new uint[] { 3 },
        "project.test",
        "Project.Test.Protocol",
        groupId: "battle");

    private static WireSchemaDocumentIr Document(params WireSchemaIr[] schemas) => new(
        WireSchemaFormatVersions.Current,
        "project.test",
        "Project.Test.Protocol",
        "battle",
        WireMemoryPackMode.VersionTolerant,
        WireDeclarationKind.Class,
        WireMemberStyle.Property,
        schemas);
}
