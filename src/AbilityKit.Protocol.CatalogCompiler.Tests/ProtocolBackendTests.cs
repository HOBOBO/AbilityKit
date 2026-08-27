using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class ProtocolBackendTests
{
    [Fact]
    public async Task ProtobufCli_ExportsAndChecksDeterministicFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "abilitykit-protobuf-" + Guid.NewGuid().ToString("N"));
        var wireDirectory = Path.Combine(root, "wire");
        var outputDirectory = Path.Combine(root, "generated");
        Directory.CreateDirectory(wireDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(wireDirectory, "test.wire.yaml"),
            """
            schemaVersion: 1
            projectId: project.test
            namespace: Project.Test.Protocol
            type: TestPayload
            fields:
              - id: 0
                name: count
                scalarType: int32
                required: true
            """);

        try
        {
            var exportCode = await CatalogCompilerProgram.RunAsync(new[]
            {
                "--wire-input", wireDirectory,
                "--project", "project.test",
                "--export-protobuf", outputDirectory
            });
            var checkCode = await CatalogCompilerProgram.RunAsync(new[]
            {
                "--wire-input", wireDirectory,
                "--project", "project.test",
                "--export-protobuf", outputDirectory,
                "--check"
            });

            Assert.Equal(0, exportCode);
            Assert.Equal(0, checkCode);
            Assert.Contains("int32 count = 1;", await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "TestPayload.proto")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Registry_ResolvesBackendsCaseInsensitivelyAndRejectsDuplicates()
    {
        var memoryPack = new MemoryPackProtocolBackend();
        var protobuf = new ProtobufProtocolBackend();
        var registry = new ProtocolBackendRegistry(new IProtocolBackend[] { memoryPack, protobuf });

        Assert.Same(memoryPack, registry.Resolve("MEMORYPACK"));
        Assert.Same(protobuf, registry.Resolve("protobuf"));
        Assert.Equal(new[] { "memorypack", "protobuf" }, registry.Codecs);
        Assert.Throws<InvalidDataException>(() =>
            new ProtocolBackendRegistry(new IProtocolBackend[] { protobuf, new ProtobufProtocolBackend() }));
    }

    [Fact]
    public void MemoryPackAdapter_PreservesEstablishedEmitterOutput()
    {
        var schema = Schema();
        var context = new ProtocolBackendSchemaContext(schema, null);

        var output = Assert.Single(new MemoryPackProtocolBackend().EmitSchema(context));

        Assert.Equal("TestPayload.MemoryPack.g.cs", output.FileName);
        Assert.Equal(MemoryPackWireEmitter.Emit(schema), output.Content);
    }

    [Fact]
    public void ProtobufBackend_EmitsStableIdsScalarsRepeatedOptionalReservedAndCustomTypes()
    {
        var schema = new WireSchemaIr(
            1,
            "TestPayload",
            new[]
            {
                new WireFieldIr(0, "enabled", "bool", false, false),
                new WireFieldIr(1, "count", "int32", false, false),
                new WireFieldIr(2, "labels", "string", true, false),
                new WireFieldIr(4, "token", "string", false, true),
                new WireFieldIr(5, "blob", "bytes", false, false),
                new WireFieldIr(6, "child", string.Empty, false, true, "Project.Test.Protocol.ChildPayload")
            },
            new uint[] { 3, 8 },
            "project.test",
            "Project.Test.Protocol");
        var backend = new ProtobufProtocolBackend();

        var first = Assert.Single(backend.EmitSchema(new ProtocolBackendSchemaContext(schema, null)));
        var second = Assert.Single(backend.EmitSchema(new ProtocolBackendSchemaContext(schema, null)));

        Assert.Equal(first, second);
        Assert.Equal("TestPayload.proto", first.FileName);
        Assert.Contains("syntax = \"proto3\";", first.Content);
        Assert.Contains("package project.test.protocol;", first.Content);
        Assert.Contains("reserved 4, 9;", first.Content);
        Assert.Contains("bool enabled = 1;", first.Content);
        Assert.Contains("int32 count = 2;", first.Content);
        Assert.Contains("repeated string labels = 3;", first.Content);
        Assert.Contains("optional string token = 5;", first.Content);
        Assert.Contains("bytes blob = 6;", first.Content);
        Assert.Contains("optional Project.Test.Protocol.ChildPayload child = 7;", first.Content);
        Assert.DoesNotContain("MemoryPack", first.Content);
    }

    [Fact]
    public void ProtobufBackend_RejectsIdsThatMapToProtobufReservedRange()
    {
        var schema = new WireSchemaIr(
            1,
            "InvalidPayload",
            new[] { new WireFieldIr(18_999, "value", "int32", false, false) },
            Array.Empty<uint>(),
            "project.test",
            "Project.Test.Protocol");

        Assert.Throws<InvalidDataException>(() =>
            new ProtobufProtocolBackend().EmitSchema(new ProtocolBackendSchemaContext(schema, null)));
    }

    private static WireSchemaIr Schema() => new(
        1,
        "TestPayload",
        new[] { new WireFieldIr(0, "count", "int32", false, false) },
        Array.Empty<uint>(),
        "project.test",
        "Project.Test.Protocol");
}
