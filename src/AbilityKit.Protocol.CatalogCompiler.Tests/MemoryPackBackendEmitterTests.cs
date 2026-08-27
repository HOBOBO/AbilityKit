using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class MemoryPackBackendEmitterTests
{
    [Fact]
    public void BackendEmitter_ProducesCodecFacadeAndRegistrationGlue()
    {
        var catalog = Catalog();
        var schema = Schema();

        var output = MemoryPackBackendEmitter.Emit(
            new[] { catalog },
            new[] { schema },
            "Project.Test.Protocol");

        Assert.Equal(output, MemoryPackBackendEmitter.Emit(
            new[] { catalog }, new[] { schema }, "Project.Test.Protocol"));
        Assert.Contains("using MemoryPack;", output);
        Assert.Contains("using AbilityKit.Protocol.Catalog;", output);
        Assert.Contains("namespace Project.Test.Protocol", output);
        Assert.Contains("public static class ProjectMemoryPackCodecs", output);

        // Codec facade wraps MemoryPackSerializer behind typed helpers.
        Assert.Contains("MemoryPackSerializer.Serialize(value)", output);
        Assert.Contains("MemoryPackSerializer.Deserialize<T>(bytes)", output);

        // Registration glue wires the catalog message to a generated DTO decoder.
        Assert.Contains(
            "registry.TryRegister(\"project.test.battle\", \"state.push\", Decode<Project.Test.Protocol.TestPayload>);",
            output);
    }

    [Fact]
    public void BackendEmitter_SkipsNonMemoryPackAndSchemaLessMessages()
    {
        var catalog = new ProtocolCatalogIr(
            "project.test.battle",
            "project.test",
            "battle",
            1,
            "memorypack",
            new[]
            {
                new ProtocolMessageIr(
                    "state.push", 100, IrDirection.ServerToClient, IrPacketKind.Push,
                    "Project.Test.Protocol.TestPayload", "memorypack", IrReliability.Realtime,
                    null, 1, 1, 4096, 0.25d),
                new ProtocolMessageIr(
                    "custom.push", 101, IrDirection.ServerToClient, IrPacketKind.Push,
                    "Project.Test.Protocol.CustomPayload", "custom-binary", IrReliability.Realtime,
                    null, 1, 1, 4096, 0.25d),
                new ProtocolMessageIr(
                    "hash.push", 102, IrDirection.ServerToClient, IrPacketKind.Push,
                    "System.UInt64", "memorypack", IrReliability.Realtime,
                    null, 1, 1, 4096, 0.25d)
            });

        var output = MemoryPackBackendEmitter.Emit(
            new[] { catalog }, new[] { Schema() }, "Project.Test.Protocol");

        Assert.Contains("state.push", output);
        Assert.DoesNotContain("custom.push", output);
        Assert.DoesNotContain("hash.push", output);
    }

    [Fact]
    public void ResolveProtocolMessage_ReturnsUniqueMemoryPackMessage()
    {
        var catalog = Catalog();
        var message = MemoryPackBackendEmitter.ResolveProtocolMessage(Schema(), new[] { catalog });

        Assert.NotNull(message);
        Assert.Equal(100u, message!.OpCode);
        Assert.Equal(IrDirection.ServerToClient, message.Direction);
    }

    [Fact]
    public void ResolveProtocolMessage_ReturnsNullWhenAmbiguousOrUnreferenced()
    {
        var shared = new ProtocolMessageIr(
            "shared.push", 200, IrDirection.ServerToClient, IrPacketKind.Push,
            "Project.Test.Protocol.Shared", "memorypack", IrReliability.Realtime,
            null, 1, 1, 4096, 0.25d);
        var second = new ProtocolMessageIr(
            "shared.again", 201, IrDirection.ServerToClient, IrPacketKind.Push,
            "Project.Test.Protocol.Shared", "memorypack", IrReliability.Realtime,
            null, 1, 1, 4096, 0.25d);
        var ambiguousCatalog = new ProtocolCatalogIr(
            "project.test.battle", "project.test", "battle", 1, "memorypack",
            new[] { shared, second });

        var sharedSchema = new WireSchemaIr(
            1, "Shared", new[] { new WireFieldIr(1, "value", "int32", false, false) },
            Array.Empty<uint>(), "project.test", "Project.Test.Protocol");

        Assert.Null(MemoryPackBackendEmitter.ResolveProtocolMessage(sharedSchema, new[] { ambiguousCatalog }));
        Assert.Null(MemoryPackBackendEmitter.ResolveProtocolMessage(Schema(), new[] { ambiguousCatalog }));
    }

    [Fact]
    public void WireEmitter_AnnotatesDtoWithProtocolOpCodeOnlyWhenMessageResolved()
    {
        var schema = Schema();
        var message = new ProtocolMessageIr(
            "state.push", 100, IrDirection.ServerToClient, IrPacketKind.Push,
            "Project.Test.Protocol.TestPayload", "memorypack", IrReliability.Realtime,
            null, 1, 1, 4096, 0.25d);

        var annotated = MemoryPackWireEmitter.Emit(schema, message);
        var bare = MemoryPackWireEmitter.Emit(schema);

        Assert.Contains("using AbilityKit.Protocol;", annotated);
        Assert.Contains(
            "[ProtocolOpCode(100u, ProtocolDirection.ServerToClient, nameof(TestPayload))]",
            annotated);
        Assert.DoesNotContain("using AbilityKit.Protocol;", bare);
        Assert.DoesNotContain("ProtocolOpCode", bare);
    }

    [Fact]
    public void NonMemoryPackBackend_IsNotUnconditionallyReferenced()
    {
        var catalog = Catalog();
        var schema = Schema();
        var csharp = CSharpEmitter.Emit(
            new[] { catalog },
            new[] { "test.protocol.yaml" },
            "Project.Test.Protocol",
            "ProjectProtocolCatalogs");

        // The always-compiled built-in catalog never references the MemoryPack namespace.
        Assert.DoesNotContain("using MemoryPack", csharp);
        Assert.DoesNotContain("MemoryPackSerializer", csharp);
        Assert.DoesNotContain("[MemoryPack", csharp);
        // It still carries the codec name as an opaque string, proving this is a memorypack catalog.
        Assert.Contains("memorypack", csharp);

        // The MemoryPack-specific surfaces are the only place the namespace/type is referenced.
        Assert.Contains("using MemoryPack;", MemoryPackWireEmitter.Emit(schema));
        Assert.Contains("using MemoryPack;", MemoryPackBackendEmitter.Emit(
            new[] { catalog }, new[] { schema }, "Project.Test.Protocol"));
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
                "state.push", 100, IrDirection.ServerToClient, IrPacketKind.Push,
                "Project.Test.Protocol.TestPayload", "memorypack", IrReliability.Realtime,
                null, 1, 1, 4096, 0.25d)
        });

    private static WireSchemaIr Schema() => new(
        1,
        "TestPayload",
        new[]
        {
            new WireFieldIr(1, "count", "int32", false, false),
            new WireFieldIr(2, "labels", "string", true, true)
        },
        Array.Empty<uint>(),
        "project.test",
        "Project.Test.Protocol");
}
