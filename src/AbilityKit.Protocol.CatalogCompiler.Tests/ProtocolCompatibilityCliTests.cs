using AbilityKit.Protocol.CatalogCompiler.Compatibility;
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
            schemaVersion: 1
            projectId: test
            namespace: Test
            type: Payload
            memoryPackMode: version-tolerant
            fields:
              - id: 1
                name: value
                scalarType: {{scalarType}}
                required: true

            """);

    private sealed record SourceRoots(string CatalogRoot, string WireRoot);
}
