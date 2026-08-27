using System.Text;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using AbilityKit.Protocol.CatalogCompiler.Lowering;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class ProtocolCatalogIrTests
{
    private const string CatalogInputRoot = "Protocols/Catalogs";
    private const string ManifestOutput = "Protocols/Generated/protocol-manifest.json";
    private const string CSharpOutput = "Unity/Packages/com.abilitykit.protocol/Runtime/Generated/BuiltInProtocolCatalogs.g.cs";

    [Fact]
    public void Golden_EmittedManifestAndCSharp_AreByteStable()
    {
        var repo = RepoRoot();
        var inputRoot = Path.Combine(repo, CatalogInputRoot);

        var sourcePaths = Directory
            .EnumerateFiles(inputRoot, "*.protocol.yaml", SearchOption.AllDirectories)
            .OrderBy(path => path.Replace('\\', '/'), StringComparer.Ordinal)
            .ToArray();
        var sourceNames = sourcePaths
            .Select(path => Path.GetRelativePath(inputRoot, path).Replace('\\', '/'))
            .ToArray();

        var parser = new YamlProtocolSourceParser();
        var catalogs = new List<ProtocolCatalogIr>(sourcePaths.Length);
        foreach (var path in sourcePaths)
            catalogs.Add(parser.Parse(path, File.ReadAllText(path, Encoding.UTF8)));

        var manifest = ManifestEmitter.Emit(catalogs, sourceNames);
        var csharp = CSharpEmitter.Emit(catalogs, sourceNames, "AbilityKit.Protocol.Generated", "BuiltInProtocolCatalogs");

        var expectedManifest = File.ReadAllText(Path.Combine(repo, ManifestOutput), Encoding.UTF8);
        var expectedCsharp = File.ReadAllText(Path.Combine(repo, CSharpOutput), Encoding.UTF8);

        Assert.Equal(expectedManifest, manifest);
        Assert.Equal(expectedCsharp, csharp);
    }

    [Fact]
    public void Parser_AppliesPerMessageDefaults()
    {
        var parser = new YamlProtocolSourceParser();
        var catalog = parser.Parse("test.protocol.yaml", TestCatalogYaml);

        Assert.Equal("test.room", catalog.CatalogId);
        Assert.Equal("test", catalog.ProjectId);
        Assert.Equal("room", catalog.Domain);
        Assert.Equal(1, catalog.Revision);
        Assert.Equal("protobuf", catalog.DefaultCodec);
        Assert.Equal(2, catalog.Messages.Count);

        var request = catalog.Messages[0];
        Assert.Equal("login.request", request.Id);
        Assert.Equal(100u, request.OpCode);
        Assert.Equal(IrDirection.ClientToServer, request.Direction);
        Assert.Equal(IrPacketKind.Request, request.Kind);
        Assert.Equal("Test.LoginReq", request.PayloadType);
        Assert.Equal("protobuf", request.Codec); // inherited from catalog default
        Assert.Equal(IrReliability.Reliable, request.Reliability); // defaulted
        Assert.Equal("login.response", request.ResponseId);
        Assert.Equal(1, request.MinimumSchemaVersion); // defaulted
        Assert.Equal(1, request.MaximumSchemaVersion); // defaulted
        Assert.Equal(1048576, request.MaximumPayloadBytes); // defaulted
        Assert.Equal(1d, request.CaptureSampleRate); // defaulted
        Assert.Empty(request.SensitiveFields);

        var response = catalog.Messages[1];
        Assert.Equal(IrPacketKind.Response, response.Kind);
        Assert.Equal(IrDirection.ServerToClient, response.Direction);
        Assert.Equal(new[] { "token" }, response.SensitiveFields);
    }

    [Fact]
    public void Lowering_ProducesRuntimeModelThatValidatesCleanly()
    {
        var parser = new YamlProtocolSourceParser();
        var catalog = parser.Parse("test.protocol.yaml", TestCatalogYaml);

        var runtime = IrLowering.ToRuntime(new[] { catalog });

        Assert.Single(runtime);
        Assert.Equal("test.room", runtime[0].CatalogId);
        Assert.Equal("protobuf", runtime[0].DefaultCodec);

        var request = runtime[0].Messages[0];
        Assert.Equal(ProtocolPacketKind.Request, request.Kind);
        Assert.Equal(ProtocolDirection.ClientToServer, request.Direction);
        Assert.Equal("login.response", request.ResponseId);
        Assert.Equal("protobuf", request.Codec);

        var validation = ProtocolCatalogValidator.Validate(runtime);
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Diagnostics);
    }

    [Theory]
    [InlineData("sideways", "request", "reliable")]
    [InlineData("c2s", "teleport", "reliable")]
    [InlineData("c2s", "request", "maybe")]
    public void Parser_RejectsUnknownEnumValues(string direction, string kind, string reliability)
    {
        var yaml = BuildMessageYaml(direction, kind, reliability);
        var parser = new YamlProtocolSourceParser();

        Assert.Throws<InvalidDataException>(() => parser.Parse("test.protocol.yaml", yaml));
    }

    [Fact]
    public void Parser_RejectsUnsupportedSchemaVersion()
    {
        var yaml = TestCatalogYaml.Replace("schemaVersion: 1\n", "schemaVersion: 99\n", StringComparison.Ordinal);
        var parser = new YamlProtocolSourceParser();

        Assert.Throws<InvalidDataException>(() => parser.Parse("test.protocol.yaml", yaml));
    }

    private static string BuildMessageYaml(string direction, string kind, string reliability) =>
        $"""
        schemaVersion: 1
        catalogId: test.room
        projectId: test
        domain: room
        revision: 1
        defaultCodec: protobuf
        messages:
          - id: login.request
            opCode: 100
            direction: {direction}
            kind: {kind}
            reliability: {reliability}
            payloadType: Test.LoginReq
        """;

    private const string TestCatalogYaml =
        """
        schemaVersion: 1
        catalogId: test.room
        projectId: test
        domain: room
        revision: 1
        defaultCodec: protobuf
        messages:
          - id: login.request
            opCode: 100
            direction: c2s
            kind: request
            payloadType: Test.LoginReq
            response: login.response
          - id: login.response
            opCode: 100
            direction: s2c
            kind: response
            payloadType: Test.LoginRes
            sensitiveFields: [token]
        """;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, CatalogInputRoot)))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root (looking for '{CatalogInputRoot}') from '{AppContext.BaseDirectory}'.");
    }
}
