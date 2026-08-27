using AbilityKit.Protocol.CatalogCompiler.Ir;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class WireSchemaParserTests
{
    [Fact]
    public void Example_ParsesFieldShapeAndReservedIds()
    {
        var root = RepoRoot();
        var path = Path.Combine(root, "Protocols", "WireSchemas", "example-payload.wire.yaml");
        var schema = new YamlWireSchemaParser().Parse(path, File.ReadAllText(path));

        Assert.Equal("RoomPresence", schema.Type);
        Assert.Equal(new uint[] { 4 }, schema.ReservedIds);
        Assert.Equal(3, schema.Fields.Count);
        Assert.True(schema.Fields[0].IsRequired);
        Assert.True(schema.Fields[1].IsOptional);
        Assert.True(schema.Fields[1].IsArray);
        Assert.Equal("uint64", schema.Fields[1].ScalarType);
    }

    [Fact]
    public void Parser_DefaultsUnqualifiedFieldToRequired()
    {
        var schema = new YamlWireSchemaParser().Parse("test.wire.yaml", Minimal("count", "int32"));
        Assert.True(schema.Fields[0].IsRequired);
        Assert.False(schema.Fields[0].IsOptional);
    }

    [Fact]
    public void Parser_AcceptsCustomTypeInsteadOfScalarType()
    {
        const string yaml = """
            schemaVersion: 1
            type: Test
            fields:
              - id: 1
                name: nested
                type: Project.Protocol.Nested
            """;
        var schema = new YamlWireSchemaParser().Parse("test.wire.yaml", yaml);
        Assert.True(schema.Fields[0].IsCustomType);
        Assert.Equal("Project.Protocol.Nested", schema.Fields[0].TypeName);
        Assert.Equal(string.Empty, schema.Fields[0].ScalarType);
    }

    [Fact]
    public void ShooterInputSchemas_PreserveLegacySequentialContract()
    {
        var root = RepoRoot();
        var parser = new YamlWireSchemaParser();
        var commandPath = Path.Combine(
            root, "Protocols", "WireSchemas", "shooter-player-command.wire.yaml");
        var payloadPath = Path.Combine(
            root, "Protocols", "WireSchemas", "shooter-input-payload.wire.yaml");

        var command = parser.Parse(commandPath, File.ReadAllText(commandPath));
        var payload = parser.Parse(payloadPath, File.ReadAllText(payloadPath));

        Assert.Equal(WireMemoryPackMode.Sequential, command.MemoryPackMode);
        Assert.Equal(WireDeclarationKind.Struct, command.DeclarationKind);
        Assert.Equal(WireMemberStyle.Field, command.MemberStyle);
        Assert.Equal(Enumerable.Range(0, 7).Select(value => (uint)value), command.Fields.Select(value => value.Id));
        Assert.Equal(
            new[] { "playerId", "moveX", "moveY", "aimX", "aimY", "fire", "attackSlot" },
            command.Fields.Select(value => value.Name));
        Assert.Equal(
            new[] { "int32", "float", "float", "float", "float", "bool", "int32" },
            command.Fields.Select(value => value.ScalarType));
        Assert.Equal(WireMemoryPackMode.Sequential, payload.MemoryPackMode);
        Assert.Equal(WireMemberStyle.Field, payload.MemberStyle);
        Assert.Equal(0u, payload.Fields[0].Id);
        Assert.Equal("AbilityKit.Protocol.Shooter.ShooterPlayerCommand", payload.Fields[0].TypeName);
        Assert.True(payload.Fields[0].IsArray);
    }

    [Fact]
    public void ShooterCommittedGeneratedSources_AreCurrent()
    {
        var root = RepoRoot();
        var parser = new YamlWireSchemaParser();
        var catalogPath = Path.Combine(root, "Protocols", "Catalogs", "shooter.protocol.yaml");
        var catalog = new YamlProtocolSourceParser().Parse(catalogPath, File.ReadAllText(catalogPath));
        var cases = new[]
        {
            ("shooter-bullet-snapshot.wire.yaml", "ShooterBulletSnapshot.MemoryPack.g.cs"),
            ("shooter-enemy-snapshot.wire.yaml", "ShooterEnemySnapshot.MemoryPack.g.cs"),
            ("shooter-event-snapshot.wire.yaml", "ShooterEventSnapshot.MemoryPack.g.cs"),
            ("shooter-input-payload.wire.yaml", "ShooterInputPayload.MemoryPack.g.cs"),
            ("shooter-player-command.wire.yaml", "ShooterPlayerCommand.MemoryPack.g.cs"),
            ("shooter-player-snapshot.wire.yaml", "ShooterPlayerSnapshot.MemoryPack.g.cs"),
            ("shooter-start-game-payload.wire.yaml", "ShooterStartGamePayload.MemoryPack.g.cs"),
            ("shooter-start-player.wire.yaml", "ShooterStartPlayer.MemoryPack.g.cs"),
            ("shooter-state-snapshot-payload.wire.yaml", "ShooterStateSnapshotPayload.MemoryPack.g.cs")
        };

        foreach (var (schemaFile, generatedFile) in cases)
        {
            var schemaPath = Path.Combine(root, "Protocols", "WireSchemas", schemaFile);
            var generatedPath = Path.Combine(
                root,
                "Unity",
                "Packages",
                "com.abilitykit.protocol.shooter",
                "Runtime",
                "Generated",
                generatedFile);
            var schema = parser.Parse(schemaPath, File.ReadAllText(schemaPath));
            var protocolMessage = MemoryPackBackendEmitter.ResolveProtocolMessage(schema, new[] { catalog });

            Assert.Equal(
                NormalizeLineEndings(MemoryPackWireEmitter.Emit(schema, protocolMessage)),
                NormalizeLineEndings(File.ReadAllText(generatedPath)));
        }
    }

    [Theory]
    [InlineData("duplicate id", "  - id: 1\n    name: a\n    scalarType: int32\n  - id: 1\n    name: b\n    scalarType: int32")]
    [InlineData("reserved conflict", "  - id: 1\n    name: a\n    scalarType: int32\nreservedIds: [1]")]
    [InlineData("presence conflict", "  - id: 1\n    name: a\n    scalarType: int32\n    optional: true\n    required: true")]
    [InlineData("type conflict", "  - id: 1\n    name: a\n    scalarType: int32\n    type: Project.Nested")]
    public void Parser_RejectsInvalidFieldContracts(string _, string fields)
    {
        var yaml = "schemaVersion: 1\ntype: Test\nfields:\n" + fields + "\n";
        Assert.Throws<InvalidDataException>(() => new YamlWireSchemaParser().Parse("test.wire.yaml", yaml));
    }

    [Theory]
    [InlineData("  - id: 1\n    name: value\n    scalarType: int32", "")]
    [InlineData("  - id: 0\n    name: value\n    scalarType: int32", "reservedIds: [1]")]
    public void Parser_RejectsNonContiguousOrReservedSequentialContracts(string fields, string suffix)
    {
        var yaml = $"schemaVersion: 1\ntype: Test\nmemoryPackMode: sequential\nfields:\n{fields}\n{suffix}\n";
        Assert.Throws<InvalidDataException>(() => new YamlWireSchemaParser().Parse("test.wire.yaml", yaml));
    }

    private static string Minimal(string name, string scalarType) => $"""
        schemaVersion: 1
        type: Test
        fields:
          - id: 1
            name: {name}
            scalarType: {scalarType}
        """;

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Protocols", "WireSchemas")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
