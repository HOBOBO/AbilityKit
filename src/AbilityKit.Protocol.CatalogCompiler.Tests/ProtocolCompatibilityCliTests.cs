using AbilityKit.Protocol.CatalogCompiler.Compatibility;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using System.Text.Json;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class ProtocolCompatibilityCliTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "abilitykit-compat-cli-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public async Task BaselineCommand_CurrentArtifact_ReturnsZero()
    {
        var sources = WriteSources(revision: 1);
        var baseline = Path.Combine(_root, ProtocolCompatibilityBaseline.ArtifactFileName);

        Assert.Equal(0, await RunBaseline(sources, baseline, check: false));
        Assert.True(File.Exists(baseline));
        Assert.Equal(0, await RunBaseline(sources, baseline, check: true));
    }

    [Fact]
    public async Task BaselineCommand_StaleArtifact_ReturnsThree()
    {
        var sources = WriteSources(revision: 1);
        var baseline = Path.Combine(_root, ProtocolCompatibilityBaseline.ArtifactFileName);
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(baseline, "{}\n");

        Assert.Equal(3, await RunBaseline(sources, baseline, check: true));
    }

    [Fact]
    public async Task CompatibilityCommand_BreakingChangeWithoutRevisionBump_ReturnsFive()
    {
        var sources = WriteSources(revision: 1);
        var baseline = Path.Combine(_root, ProtocolCompatibilityBaseline.ArtifactFileName);
        Assert.Equal(0, await RunBaseline(sources, baseline, check: false));

        WriteWireSchema(sources.WireRoot, scalarType: "int64");

        Assert.Equal(5, await RunCompatibility(sources, baseline));
    }

    [Fact]
    public async Task CompatibilityCommand_RevisionCoveredBreakingChange_ReturnsZero()
    {
        var sources = WriteSources(revision: 1);
        var baseline = Path.Combine(_root, ProtocolCompatibilityBaseline.ArtifactFileName);
        Assert.Equal(0, await RunBaseline(sources, baseline, check: false));

        WriteCatalog(sources.CatalogRoot, revision: 2);
        WriteWireSchema(sources.WireRoot, scalarType: "int64");

        Assert.Equal(0, await RunCompatibility(sources, baseline));
    }

    [Fact]
    public async Task BaselineCommand_GroupedV2SourceExpandsEveryType()
    {
        var sources = WriteSources(revision: 1);
        WriteGroupedWireSchema(sources.WireRoot);
        var baselinePath = Path.Combine(_root, ProtocolCompatibilityBaseline.ArtifactFileName);

        Assert.Equal(0, await RunBaseline(sources, baselinePath, check: false));

        var baseline = ProtocolCompatibilityBaseline.Deserialize(
            await File.ReadAllTextAsync(baselinePath));
        Assert.Equal(
            new[] { "Test.Command", "Test.Payload" },
            baseline.WireSchemas.Select(value => value.QualifiedType));
    }

    [Fact]
    public async Task WireEditorCommand_UpdatesOneGroupedV2TypeAndPreservesItsSibling()
    {
        var sources = WriteSources(revision: 1);
        WriteGroupedWireSchema(sources.WireRoot);
        var sourcePath = Path.Combine(sources.WireRoot, "payload.wire.yaml");
        var editPath = Path.Combine(_root, "wire-edit.json");
        var schema = new ProtocolWorkspaceWireSchema
        {
            SourcePath = sourcePath,
            SchemaVersion = 2,
            SourceType = "Payload",
            ProjectId = "test",
            GroupId = "domain",
            Namespace = "Test",
            Type = "RenamedPayload",
            Fields = new[]
            {
                new ProtocolWorkspaceWireField { Id = 1, Name = "changed", ScalarType = "int64" }
            }
        };
        await File.WriteAllTextAsync(
            editPath,
            JsonSerializer.Serialize(schema, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

        var exitCode = await CatalogCompilerProgram.RunAsync(new[]
        {
            "--write-wire-schema", editPath,
            "--output", sourcePath
        });

        Assert.Equal(0, exitCode);
        var document = new YamlWireSchemaParser().ParseDocument(
            sourcePath,
            await File.ReadAllTextAsync(sourcePath));
        Assert.Equal(new[] { "Command", "RenamedPayload" }, document.Schemas.Select(value => value.Type));
        Assert.Equal("changed", document.Schemas[1].Fields[0].Name);
        Assert.Equal("int64", document.Schemas[1].Fields[0].ScalarType);
    }

    [Fact]
    public async Task WireEditorCommand_AppendsTypeToExistingGroupedV2Document()
    {
        var sources = WriteSources(revision: 1);
        WriteGroupedWireSchema(sources.WireRoot);
        var sourcePath = Path.Combine(sources.WireRoot, "payload.wire.yaml");
        var editPath = Path.Combine(_root, "wire-add.json");
        var schema = new ProtocolWorkspaceWireSchema
        {
            SourcePath = sourcePath,
            SchemaVersion = 2,
            ProjectId = "test",
            GroupId = "domain",
            Namespace = "Test",
            Type = "AddedPayload",
            MemoryPackMode = "version-tolerant",
            Declaration = "class",
            MemberStyle = "property",
            Fields = new[]
            {
                new ProtocolWorkspaceWireField { Id = 1, Name = "value", ScalarType = "uint8" }
            }
        };
        await File.WriteAllTextAsync(
            editPath,
            JsonSerializer.Serialize(schema, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

        var exitCode = await CatalogCompilerProgram.RunAsync(new[]
        {
            "--write-wire-schema", editPath,
            "--output", sourcePath
        });

        Assert.Equal(0, exitCode);
        var document = new YamlWireSchemaParser().ParseDocument(
            sourcePath,
            await File.ReadAllTextAsync(sourcePath));
        Assert.Equal(new[] { "Command", "Payload", "AddedPayload" }, document.Schemas.Select(value => value.Type));
        Assert.Equal("uint8", document.Schemas[2].Fields[0].ScalarType);
    }

    [Fact]
    public async Task WireEditorCommand_RejectsDuplicateTypeWhenAppendingGroupedV2Document()
    {
        var sources = WriteSources(revision: 1);
        WriteGroupedWireSchema(sources.WireRoot);
        var sourcePath = Path.Combine(sources.WireRoot, "payload.wire.yaml");
        var editPath = Path.Combine(_root, "wire-duplicate.json");
        var schema = new ProtocolWorkspaceWireSchema
        {
            SourcePath = sourcePath,
            SchemaVersion = 2,
            ProjectId = "test",
            GroupId = "domain",
            Namespace = "Test",
            Type = "Payload",
            Fields = Array.Empty<ProtocolWorkspaceWireField>()
        };
        await File.WriteAllTextAsync(
            editPath,
            JsonSerializer.Serialize(schema, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

        var exitCode = await CatalogCompilerProgram.RunAsync(new[]
        {
            "--write-wire-schema", editPath,
            "--output", sourcePath
        });

        Assert.Equal(1, exitCode);
        var document = new YamlWireSchemaParser().ParseDocument(
            sourcePath,
            await File.ReadAllTextAsync(sourcePath));
        Assert.Equal(2, document.Schemas.Count);
    }

    [Fact]
    public async Task BaselineCommand_RejectsDuplicateGroupIdWithinProject()
    {
        var sources = WriteSources(revision: 1);
        await File.WriteAllTextAsync(
            Path.Combine(sources.WireRoot, "duplicate.wire.yaml"),
            """
            schemaVersion: 2
            projectId: test
            groupId: domain
            namespace: Test.Other
            types:
              - name: OtherPayload
                fields: []
            """);

        var baselinePath = Path.Combine(_root, ProtocolCompatibilityBaseline.ArtifactFileName);
        Assert.Equal(1, await RunBaseline(sources, baselinePath, check: false));
    }

    private static Task<int> RunBaseline(SourceRoots sources, string baseline, bool check)
    {
        var args = new List<string>
        {
            "--input", sources.CatalogRoot,
            "--wire-input", sources.WireRoot,
            "--compatibility-baseline", baseline
        };
        if (check) args.Add("--check");
        return CatalogCompilerProgram.RunAsync(args.ToArray());
    }

    private static Task<int> RunCompatibility(SourceRoots sources, string baseline) =>
        CatalogCompilerProgram.RunAsync(new[]
        {
            "--input", sources.CatalogRoot,
            "--wire-input", sources.WireRoot,
            "--compatibility-check", baseline
        });

    private SourceRoots WriteSources(int revision)
    {
        var catalogRoot = Path.Combine(_root, "Catalogs");
        var wireRoot = Path.Combine(_root, "WireSchemas");
        Directory.CreateDirectory(catalogRoot);
        Directory.CreateDirectory(wireRoot);
        WriteCatalog(catalogRoot, revision);
        WriteWireSchema(wireRoot, "int32");
        return new SourceRoots(catalogRoot, wireRoot);
    }

    private static void WriteCatalog(string root, int revision) =>
        File.WriteAllText(
            Path.Combine(root, "test.protocol.yaml"),
            $$"""
            schemaVersion: 1
            catalogId: test.catalog
            projectId: test
            domain: test
            revision: {{revision}}
            defaultCodec: memorypack
            messages:
              - id: payload.push
                opCode: 1
                direction: s2c
                kind: push
                payloadType: Test.Payload
                maximumPayloadBytes: 1024

            """);

    private static void WriteWireSchema(string root, string scalarType) =>
        File.WriteAllText(
            Path.Combine(root, "payload.wire.yaml"),
            $$"""
            schemaVersion: 2
            projectId: test
            groupId: domain
            namespace: Test
            types:
              - name: Payload
                fields:
                  - id: 1
                    name: value
                    scalarType: {{scalarType}}
                    required: true

            """);

    private static void WriteGroupedWireSchema(string root) =>
        File.WriteAllText(
            Path.Combine(root, "payload.wire.yaml"),
            """
            schemaVersion: 2
            projectId: test
            groupId: domain
            namespace: Test
            defaults:
              memoryPackMode: version-tolerant
            types:
              - name: Command
                fields:
                  - id: 1
                    name: value
                    scalarType: int32
                    required: true
              - name: Payload
                fields:
                  - id: 1
                    name: command
                    type: Test.Command
                    required: true

            """);

    private sealed record SourceRoots(string CatalogRoot, string WireRoot);
}
