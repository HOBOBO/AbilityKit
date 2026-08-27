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
        var yaml = ProtocolYamlEmitter.EmitWireSchema(workspace.WireSchemas[0]);
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
            ProjectId = "project.test",
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

        var yaml = ProtocolYamlEmitter.EmitWireSchema(editorSchema);
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
            1,
            "LegacyPayload",
            new[] { new WireFieldIr(0, "value", "int32", false, false) },
            Array.Empty<uint>(),
            "project.test",
            "Project.Test.Protocol",
            WireMemoryPackMode.Sequential,
            WireDeclarationKind.Struct,
            WireMemberStyle.Field);

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
            1,
            "Command",
            new[] { new WireFieldIr(0, "value", "int32", false, false) },
            Array.Empty<uint>(),
            "project.test",
            "Project.Test.Protocol");
        var payload = new WireSchemaIr(
            1,
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
            "Project.Test.Protocol");

        var plan = MemoryPackExportPlanner.Create(
            new[] { "Project.Test.Protocol.Payload", "System.UInt64" },
            new[] { command, payload },
            includeUnreferenced: false);

        Assert.Equal(new[] { command, payload }, plan.Schemas);
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
        1,
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
        "Project.Test.Protocol");
}
