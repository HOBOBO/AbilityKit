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
        var path = Path.Combine(root, "Protocols", "WireSchemas", "shared-common.wire.yaml");
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
    public void Parser_AcceptsUint8ForExistingByteWireFields()
    {
        var schema = new YamlWireSchemaParser().Parse("test.wire.yaml", Minimal("version", "uint8"));

        Assert.Equal("uint8", schema.Fields[0].ScalarType);
        Assert.Contains("public byte Version", MemoryPackWireEmitter.Emit(schema));
    }

    [Fact]
    public void Parser_AcceptsCustomTypeInsteadOfScalarType()
    {
        const string yaml = """
            schemaVersion: 2
            projectId: project.test
            groupId: domain
            namespace: Project.Protocol
            types:
              - name: Test
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
    public void Parser_AcceptsExplicitExternalCustomType()
    {
        const string yaml = """
            schemaVersion: 2
            projectId: project.test
            groupId: domain
            namespace: Project.Protocol
            types:
              - name: Test
                fields:
                  - id: 0
                    name: position
                    type: Shared.Math.Vec3
                    external: true
            """;

        var field = new YamlWireSchemaParser().Parse("test.wire.yaml", yaml).Fields[0];

        Assert.True(field.IsCustomType);
        Assert.True(field.IsExternalReference);
        Assert.Equal("Shared.Math.Vec3", field.TypeName);
    }

    [Fact]
    public void Parser_RejectsExternalScalarType()
    {
        var yaml = Minimal("count", "int32").Replace("scalarType: int32", "scalarType: int32\n        external: true");
        Assert.Throws<InvalidDataException>(() => new YamlWireSchemaParser().Parse("test.wire.yaml", yaml));
    }

    [Fact]
    public void Parser_V2ExpandsGroupedTypesAndAppliesDocumentDefaults()
    {
        const string yaml = """
            schemaVersion: 2
            projectId: project.test
            groupId: battle
            namespace: Project.Test.Protocol
            defaults:
              memoryPackMode: sequential
              declaration: struct
              memberStyle: field
            types:
              - name: Command
                fields:
                  - id: 0
                    name: value
                    scalarType: int32
              - name: Payload
                memoryPackMode: version-tolerant
                declaration: class
                memberStyle: property
                fields:
                  - id: 1
                    name: commands
                    type: Project.Test.Protocol.Command
                    array: true
                    required: true
                reservedIds: [2]
            """;

        var document = new YamlWireSchemaParser().ParseDocument("test.wire.yaml", yaml);

        Assert.Equal(WireSchemaFormatVersions.Current, document.SchemaVersion);
        Assert.Equal("project.test", document.ProjectId);
        Assert.Equal("battle", document.GroupId);
        Assert.Equal("Project.Test.Protocol", document.TargetNamespace);
        Assert.Equal(2, document.Schemas.Count);
        Assert.Equal("Command", document.Schemas[0].Type);
        Assert.Equal(WireMemoryPackMode.Sequential, document.Schemas[0].MemoryPackMode);
        Assert.Equal(WireDeclarationKind.Struct, document.Schemas[0].DeclarationKind);
        Assert.Equal(WireMemberStyle.Field, document.Schemas[0].MemberStyle);
        Assert.Equal(WireMemoryPackMode.VersionTolerant, document.Schemas[1].MemoryPackMode);
        Assert.Equal("Project.Test.Protocol.Command", document.Schemas[1].Fields[0].TypeName);
        Assert.Equal(new uint[] { 2 }, document.Schemas[1].ReservedIds);
    }

    [Fact]
    public void Parser_V2RejectsDuplicateTypeNames()
    {
        const string yaml = """
            schemaVersion: 2
            projectId: project.test
            groupId: battle
            namespace: Project.Test.Protocol
            types:
              - name: Payload
                fields: []
              - name: Payload
                fields: []
            """;

        Assert.Throws<InvalidDataException>(
            () => new YamlWireSchemaParser().ParseDocument("test.wire.yaml", yaml));
    }

    [Fact]
    public void Parser_V2RequiresFieldsOnEveryType()
    {
        const string yaml = """
            schemaVersion: 2
            projectId: project.test
            groupId: battle
            namespace: Project.Test.Protocol
            types:
              - name: Payload
            """;

        Assert.Throws<InvalidDataException>(
            () => new YamlWireSchemaParser().ParseDocument("test.wire.yaml", yaml));
    }

    [Fact]
    public void Parser_SingleTypeEntryPointRejectsMultiTypeV2Document()
    {
        const string yaml = """
            schemaVersion: 2
            projectId: project.test
            groupId: battle
            namespace: Project.Test.Protocol
            types:
              - name: First
                fields: []
              - name: Second
                fields: []
            """;

        Assert.Throws<InvalidDataException>(() => new YamlWireSchemaParser().Parse("test.wire.yaml", yaml));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Battle")]
    [InlineData("battle_input")]
    public void Parser_RequiresCanonicalGroupId(string groupId)
    {
        var yaml = Minimal("count", "int32").Replace("groupId: domain", $"groupId: {groupId}");
        Assert.Throws<InvalidDataException>(
            () => new YamlWireSchemaParser().ParseDocument("test.wire.yaml", yaml));
    }

    [Fact]
    public void Parser_RejectsRetiredSingleTypeFormat()
    {
        const string yaml = """
            schemaVersion: 1
            projectId: project.test
            namespace: Project.Test.Protocol
            type: Payload
            fields: []
            """;

        Assert.Throws<InvalidDataException>(
            () => new YamlWireSchemaParser().ParseDocument("test.wire.yaml", yaml));
    }

    [Fact]
    public void ShooterInputSchemas_PreserveLegacySequentialContract()
    {
        var root = RepoRoot();
        var parser = new YamlWireSchemaParser();
        var path = Path.Combine(root, "Protocols", "WireSchemas", "shooter-battle.wire.yaml");
        var schemas = parser.ParseDocument(path, File.ReadAllText(path)).Schemas;
        var command = schemas.Single(value => value.Type == "ShooterPlayerCommand");
        var payload = schemas.Single(value => value.Type == "ShooterInputPayload");

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
        var schemaPath = Path.Combine(root, "Protocols", "WireSchemas", "shooter-battle.wire.yaml");
        var schemas = parser.ParseDocument(schemaPath, File.ReadAllText(schemaPath)).Schemas;
        var cases = new[]
        {
            ("ShooterBulletSnapshot", "ShooterBulletSnapshot.MemoryPack.g.cs"),
            ("ShooterEnemySnapshot", "ShooterEnemySnapshot.MemoryPack.g.cs"),
            ("ShooterEventSnapshot", "ShooterEventSnapshot.MemoryPack.g.cs"),
            ("ShooterInputPayload", "ShooterInputPayload.MemoryPack.g.cs"),
            ("ShooterPlayerCommand", "ShooterPlayerCommand.MemoryPack.g.cs"),
            ("ShooterPlayerSnapshot", "ShooterPlayerSnapshot.MemoryPack.g.cs"),
            ("ShooterStartGamePayload", "ShooterStartGamePayload.MemoryPack.g.cs"),
            ("ShooterStartPlayer", "ShooterStartPlayer.MemoryPack.g.cs"),
            ("ShooterStateSnapshotPayload", "ShooterStateSnapshotPayload.MemoryPack.g.cs")
        };

        foreach (var (typeName, generatedFile) in cases)
        {
            var generatedPath = Path.Combine(
                root,
                "Unity",
                "Packages",
                "com.abilitykit.protocol.shooter",
                "Runtime",
                "Generated",
                generatedFile);
            var schema = schemas.Single(value => value.Type == typeName);
            var protocolMessage = MemoryPackBackendEmitter.ResolveProtocolMessage(schema, new[] { catalog });

            Assert.Equal(
                NormalizeLineEndings(MemoryPackWireEmitter.Emit(schema, protocolMessage)),
                NormalizeLineEndings(File.ReadAllText(generatedPath)));
        }
    }

    [Fact]
    public void MobaCommittedGeneratedSources_AreCurrentAndComplete()
    {
        var root = RepoRoot();
        var parser = new YamlWireSchemaParser();
        var catalogPath = Path.Combine(root, "Protocols", "Catalogs", "moba.protocol.yaml");
        var catalog = new YamlProtocolSourceParser().Parse(catalogPath, File.ReadAllText(catalogPath));
        var schemas = Directory.GetFiles(
                Path.Combine(root, "Protocols", "WireSchemas"),
                "*.wire.yaml",
                SearchOption.AllDirectories)
            .SelectMany(path => parser.ParseDocument(path, File.ReadAllText(path)).Schemas)
            .Where(schema => schema.ProjectId == "abilitykit.moba")
            .OrderBy(MemoryPackExportPlanner.QualifiedType, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(26, schemas.Length);
        Assert.Equal(new[] { "input", "room", "state-sync" },
            schemas.Select(schema => schema.GroupId).Distinct().OrderBy(value => value, StringComparer.Ordinal));

        var generatedDirectory = Path.Combine(
            root,
            "Unity",
            "Packages",
            "com.abilitykit.protocol.moba",
            "Runtime",
            "Generated",
            "MemoryPack");
        foreach (var schema in schemas)
        {
            var generatedPath = Path.Combine(generatedDirectory, schema.Type + ".MemoryPack.g.cs");
            var protocolMessage = MemoryPackBackendEmitter.ResolveProtocolMessage(schema, new[] { catalog });

            Assert.True(File.Exists(generatedPath), $"Missing generated source for {MemoryPackExportPlanner.QualifiedType(schema)}.");
            Assert.Equal(
                NormalizeLineEndings(MemoryPackWireEmitter.Emit(schema, protocolMessage)),
                NormalizeLineEndings(File.ReadAllText(generatedPath)));
        }
    }

    [Theory]
    [InlineData("duplicate id", "      - id: 1\n        name: a\n        scalarType: int32\n      - id: 1\n        name: b\n        scalarType: int32")]
    [InlineData("reserved conflict", "      - id: 1\n        name: a\n        scalarType: int32\n    reservedIds: [1]")]
    [InlineData("presence conflict", "      - id: 1\n        name: a\n        scalarType: int32\n        optional: true\n        required: true")]
    [InlineData("type conflict", "      - id: 1\n        name: a\n        scalarType: int32\n        type: Project.Nested")]
    public void Parser_RejectsInvalidFieldContracts(string _, string typeBody)
    {
        var yaml = GroupHeader("    fields:\n" + typeBody);
        Assert.Throws<InvalidDataException>(() => new YamlWireSchemaParser().Parse("test.wire.yaml", yaml));
    }

    [Theory]
    [InlineData("      - id: 1\n        name: value\n        scalarType: int32", "")]
    [InlineData("      - id: 0\n        name: value\n        scalarType: int32", "    reservedIds: [1]")]
    public void Parser_RejectsNonContiguousOrReservedSequentialContracts(string fields, string suffix)
    {
        var yaml = GroupHeader($"    memoryPackMode: sequential\n    fields:\n{fields}\n{suffix}");
        Assert.Throws<InvalidDataException>(() => new YamlWireSchemaParser().Parse("test.wire.yaml", yaml));
    }

    private static string Minimal(string name, string scalarType) => $"""
        schemaVersion: 2
        projectId: project.test
        groupId: domain
        namespace: Project.Test.Protocol
        types:
          - name: Test
            fields:
              - id: 1
                name: {name}
                scalarType: {scalarType}
        """;

    private static string GroupHeader(string typeBody) => $$"""
        schemaVersion: 2
        projectId: project.test
        groupId: domain
        namespace: Project.Test.Protocol
        types:
          - name: Test
        {{typeBody}}
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
