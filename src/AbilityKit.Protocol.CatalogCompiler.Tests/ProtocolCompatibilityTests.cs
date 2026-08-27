using System.Text;
using AbilityKit.Protocol.CatalogCompiler.Compatibility;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using Xunit;

namespace AbilityKit.Protocol.CatalogCompiler.Tests;

public sealed class ProtocolCompatibilityTests
{
    private const string CatalogInputRoot = "Protocols/Catalogs";
    private const string WireInputRoot = "Protocols/WireSchemas";
    private const string BaselineArtifactPath = "Protocols/Compatibility/protocol-compatibility-baseline.json";
    private const string UpdateBaselineVariable = "AK_UPDATE_PROTOCOL_COMPAT_BASELINE";

    [Fact]
    public void Baseline_CommittedArtifactMatchesCurrentSources()
    {
        var baseline = CaptureCurrentSources(out var repo);
        var actual = ProtocolCompatibilityBaseline.Serialize(baseline);
        var artifactPath = Path.Combine(repo, BaselineArtifactPath);

        if (Environment.GetEnvironmentVariable(UpdateBaselineVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            File.WriteAllText(artifactPath, actual, new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(artifactPath),
            $"Compatibility baseline artifact is missing. Run the tests with {UpdateBaselineVariable}=1 to create it.");
        Assert.Equal(File.ReadAllText(artifactPath, Encoding.UTF8), actual);
    }

    [Fact]
    public void Baseline_SerializeDeserializeRoundTripsByteStable()
    {
        var baseline = CaptureCurrentSources(out _);

        var serialized = ProtocolCompatibilityBaseline.Serialize(baseline);
        var roundTripped = ProtocolCompatibilityBaseline.Deserialize(serialized);

        Assert.Equal(serialized, ProtocolCompatibilityBaseline.Serialize(roundTripped));
        Assert.Equal(baseline.Catalogs.Count, roundTripped.Catalogs.Count);
        Assert.Equal(baseline.WireSchemas.Count, roundTripped.WireSchemas.Count);
    }

    [Fact]
    public void Baseline_RejectsUnsupportedSchemaVersion()
    {
        var json = "{\"schemaVersion\":99,\"generatorVersion\":\"1.0\",\"catalogs\":[],\"wireSchemas\":[]}";
        Assert.Throws<InvalidDataException>(() => ProtocolCompatibilityBaseline.Deserialize(json));
    }

    [Fact]
    public void Check_IdenticalSources_AreCompatibleAndPolicySatisfied()
    {
        var baseline = BaselineOf(
            new[] { Catalog(revision: 1, Message()) },
            new[] { Wire() });

        var result = ProtocolCompatibilityCheck.Check(baseline, new[] { Catalog(revision: 1, Message()) }, new[] { Wire() });

        Assert.Empty(result.Report.Changes);
        Assert.True(result.Report.IsCompatible);
        Assert.True(result.RevisionPolicy.IsSatisfied);
        Assert.True(result.IsCompatible);
    }

    [Fact]
    public void Diff_MessageAdded_IsCompatibleWithoutRevisionBump()
    {
        var baseline = BaselineOf(new[] { Catalog(revision: 1, Message()) }, Array.Empty<WireSchemaIr>());
        var current = new[]
        {
            Catalog(revision: 1, Message(),
                Message(id: "logout.request", opCode: 101, payloadType: "Test.LogoutReq"))
        };

        var result = ProtocolCompatibilityCheck.Check(baseline, current, Array.Empty<WireSchemaIr>());

        Assert.True(result.Report.IsCompatible);
        Assert.True(result.RevisionPolicy.IsSatisfied);
        Assert.Contains(result.Report.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.MessageAdded &&
            change.Severity == ProtocolCompatibilitySeverity.Compatible);
    }

    [Fact]
    public void Diff_MessageRemoved_IsBreaking()
    {
        var baseline = BaselineOf(new[] { Catalog(revision: 1, Message(), Message(id: "logout.request", opCode: 101)) }, Array.Empty<WireSchemaIr>());
        var current = new[] { Catalog(revision: 2, Message()) };

        var report = ProtocolCompatibilityDiff.Compare(baseline, current, Array.Empty<WireSchemaIr>());

        var removal = Assert.Single(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.MessageRemoved &&
            change.MessageId == "logout.request");
        Assert.Equal(ProtocolCompatibilitySeverity.Breaking, removal.Severity);
    }

    public static TheoryData<string, ProtocolMessageIr, string> BreakingMessageChanges => new()
    {
        {
            "opcode",
            Message(opCode: 101),
            nameof(ProtocolCompatibilityChangeKind.MessageOpCodeChanged)
        },
        {
            "direction",
            Message(direction: IrDirection.ServerToClient),
            nameof(ProtocolCompatibilityChangeKind.MessageDirectionChanged)
        },
        {
            "kind",
            Message(kind: IrPacketKind.Push),
            nameof(ProtocolCompatibilityChangeKind.MessageKindChanged)
        },
        {
            "payload",
            Message(payloadType: "Test.OtherReq"),
            nameof(ProtocolCompatibilityChangeKind.MessagePayloadChanged)
        },
        {
            "codec",
            Message(codec: "memorypack"),
            nameof(ProtocolCompatibilityChangeKind.MessageCodecChanged)
        },
        {
            "response",
            Message(response: "login.response"),
            nameof(ProtocolCompatibilityChangeKind.MessageResponseChanged)
        },
        {
            "reliability",
            Message(reliability: IrReliability.Realtime),
            nameof(ProtocolCompatibilityChangeKind.MessageReliabilityChanged)
        }
    };

    [Theory]
    [MemberData(nameof(BreakingMessageChanges))]
    public void Diff_MessageContractChanges_AreBreakingAndRequireRevisionBump(
        string label,
        ProtocolMessageIr changed,
        string expectedKindName)
    {
        var expectedKind = Enum.Parse<ProtocolCompatibilityChangeKind>(expectedKindName);
        var baseline = BaselineOf(new[] { Catalog(revision: 1, Message()) }, Array.Empty<WireSchemaIr>());

        var unchangedResult = ProtocolCompatibilityCheck.Check(
            baseline, new[] { Catalog(revision: 1, changed) }, Array.Empty<WireSchemaIr>());
        Assert.False(unchangedResult.Report.IsCompatible, label);
        Assert.False(unchangedResult.RevisionPolicy.IsSatisfied, label);
        Assert.Contains(unchangedResult.Report.BreakingChanges, change => change.Kind == expectedKind);
        Assert.Contains(unchangedResult.RevisionPolicy.Violations, violation =>
            violation.CatalogId == "test.room" &&
            violation.Rule == ProtocolRevisionPolicy.BreakingRequiresRevisionBump);

        var bumpedResult = ProtocolCompatibilityCheck.Check(
            baseline, new[] { Catalog(revision: 2, changed) }, Array.Empty<WireSchemaIr>());
        Assert.False(bumpedResult.Report.IsCompatible, label);
        Assert.True(bumpedResult.RevisionPolicy.IsSatisfied, label);
    }

    [Fact]
    public void Diff_ResponsePairingRemoved_IsBreaking()
    {
        var baseline = BaselineOf(
            new[] { Catalog(revision: 1, Message(response: "login.response")) }, Array.Empty<WireSchemaIr>());
        var current = new[] { Catalog(revision: 2, Message(response: null)) };

        var report = ProtocolCompatibilityDiff.Compare(baseline, current, Array.Empty<WireSchemaIr>());

        Assert.Contains(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.MessageResponseChanged);
    }

    [Fact]
    public void Diff_OpCodeReassignedToDifferentMessage_IsBreaking()
    {
        var baseline = BaselineOf(
            new[]
            {
                Catalog(revision: 1,
                    Message(id: "login.request", opCode: 100),
                    Message(id: "logout.request", opCode: 200))
            },
            Array.Empty<WireSchemaIr>());
        // login.request moved off opcode 100 and logout.request took it over: a peer still
        // sending 100 now reaches a different handler.
        var current = new[]
        {
            Catalog(revision: 2,
                Message(id: "login.request", opCode: 101),
                Message(id: "logout.request", opCode: 100))
        };

        var report = ProtocolCompatibilityDiff.Compare(baseline, current, Array.Empty<WireSchemaIr>());

        var reassignment = Assert.Single(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.MessageOpCodeReassigned);
        Assert.Contains("100", reassignment.Detail, StringComparison.Ordinal);
        Assert.Contains("login.request", reassignment.Detail, StringComparison.Ordinal);
        Assert.Contains("logout.request", reassignment.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Diff_CompatibleObservations_DoNotRequireRevisionBump()
    {
        var baseline = BaselineOf(
            new[]
            {
                Catalog(revision: 1,
                    Message(minimumSchemaVersion: 1, maximumSchemaVersion: 1, maximumPayloadBytes: 1024))
            },
            Array.Empty<WireSchemaIr>());
        var current = new[]
        {
            Catalog(revision: 1, defaultCodec: "memorypack",
                Message(codec: "protobuf", minimumSchemaVersion: 1, maximumSchemaVersion: 3, maximumPayloadBytes: 4096))
        };

        var result = ProtocolCompatibilityCheck.Check(baseline, current, Array.Empty<WireSchemaIr>());

        Assert.True(result.Report.IsCompatible);
        Assert.True(result.RevisionPolicy.IsSatisfied);
        Assert.Contains(result.Report.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.MessageSchemaWindowChanged);
        Assert.Contains(result.Report.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.MessageBudgetChanged);
    }

    [Fact]
    public void Policy_RevisionDecrease_IsViolationEvenWithoutOtherChanges()
    {
        var baseline = BaselineOf(new[] { Catalog(revision: 3, Message()) }, Array.Empty<WireSchemaIr>());
        var current = new[] { Catalog(revision: 2, Message()) };

        var report = ProtocolCompatibilityDiff.Compare(baseline, current, Array.Empty<WireSchemaIr>());
        var policy = ProtocolRevisionPolicy.Evaluate(baseline, current, report);

        Assert.Empty(report.BreakingChanges);
        var violation = Assert.Single(policy.Violations);
        Assert.Equal(ProtocolRevisionPolicy.RevisionMustNotDecrease, violation.Rule);
    }

    [Fact]
    public void Diff_WireFieldAddedRequired_IsBreakingOptionalIsCompatible()
    {
        var baseline = BaselineOf(Array.Empty<ProtocolCatalogIr>(), new[] { Wire(Field(0, "userId")) });

        var optionalAdded = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(1, "note", optional: true)) });
        Assert.True(optionalAdded.IsCompatible);
        Assert.Contains(optionalAdded.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldAdded &&
            change.Severity == ProtocolCompatibilitySeverity.Compatible);

        var requiredAdded = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(1, "note")) });
        Assert.False(requiredAdded.IsCompatible);
        Assert.Contains(requiredAdded.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldAdded);
    }

    [Fact]
    public void Diff_WireFieldAddedInSequentialMode_IsAlwaysBreaking()
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(mode: WireMemoryPackMode.Sequential, fields: new[] { Field(0, "userId") }) });

        var report = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[]
            {
                Wire(mode: WireMemoryPackMode.Sequential,
                    fields: new[] { Field(0, "userId"), Field(1, "note", optional: true) })
            });

        Assert.False(report.IsCompatible);
        Assert.Contains(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldAdded);
    }

    [Fact]
    public void Diff_WireFieldRemoved_IsBreakingUnlessIdBecomesReserved()
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(1, "legacy")) });

        var unreserved = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId")) });
        Assert.False(unreserved.IsCompatible);
        Assert.Contains(unreserved.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldRemoved);

        var reserved = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), reservedIds: new uint[] { 1 }) });
        Assert.True(reserved.IsCompatible);
        Assert.Contains(reserved.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldRemoved &&
            change.Severity == ProtocolCompatibilitySeverity.Compatible);
        Assert.Contains(reserved.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireReservationChanged);
    }

    [Fact]
    public void Diff_WireFieldIdChanged_IsBreakingWithoutAddRemoveNoise()
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(1, "moveX")) });

        var report = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(2, "moveX")) });

        Assert.Contains(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldIdChanged);
        Assert.DoesNotContain(report.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldAdded ||
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldRemoved);
    }

    [Fact]
    public void Diff_WireFieldRenamedSameId_IsCompatible()
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId")) });

        var report = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "playerId")) });

        Assert.True(report.IsCompatible);
        Assert.Contains(report.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldRenamed);
    }

    public static TheoryData<string, WireFieldIr> BreakingWireTypeChanges => new()
    {
        { "scalar", Field(1, "moveX", scalarType: "int64") },
        { "array", Field(1, "moveX", isArray: true) },
        { "custom", Field(1, "moveX", typeName: "Test.Vec2") }
    };

    [Theory]
    [MemberData(nameof(BreakingWireTypeChanges))]
    public void Diff_WireFieldTypeChanges_AreBreaking(string label, WireFieldIr changed)
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(1, "moveX", scalarType: "float")) });

        var report = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), changed) });

        Assert.False(report.IsCompatible, label);
        Assert.Contains(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldTypeChanged);
    }

    [Fact]
    public void Diff_WireFieldTightenedToRequired_IsBreakingLoosenedIsCompatible()
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(1, "note", optional: true)) });

        var tightened = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(1, "note")) });
        Assert.False(tightened.IsCompatible);
        Assert.Contains(tightened.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldRequirednessChanged);

        var loosened = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId", optional: true), Field(1, "note", optional: true)) });
        Assert.True(loosened.IsCompatible);
        Assert.Contains(loosened.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireFieldRequirednessChanged &&
            change.Severity == ProtocolCompatibilitySeverity.Compatible);
    }

    [Fact]
    public void Diff_ReservedIdConsumedByField_IsBreaking()
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), reservedIds: new uint[] { 1, 2 }) });

        var report = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(Field(0, "userId"), Field(1, "resurrected"), reservedIds: new uint[] { 2 }) });

        Assert.Contains(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireReservedIdConsumed);
    }

    [Fact]
    public void Diff_WireMemoryPackModeChanged_IsBreaking()
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(mode: WireMemoryPackMode.VersionTolerant, fields: new[] { Field(0, "userId") }) });

        var report = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(mode: WireMemoryPackMode.Sequential, fields: new[] { Field(0, "userId") }) });

        Assert.False(report.IsCompatible);
        Assert.Contains(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireMemoryPackModeChanged);
    }

    [Fact]
    public void Diff_WireSchemaRemovedAndAdded_AreClassified()
    {
        var baseline = BaselineOf(
            Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(type: "Test.LoginReq"), Wire(type: "Test.LegacyPayload") });

        var report = ProtocolCompatibilityDiff.Compare(
            baseline, Array.Empty<ProtocolCatalogIr>(),
            new[] { Wire(type: "Test.LoginReq"), Wire(type: "Test.NewPayload") });

        Assert.Contains(report.BreakingChanges, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireSchemaRemoved &&
            change.WireType == "Test.LegacyPayload");
        Assert.Contains(report.Changes, change =>
            change.Kind == ProtocolCompatibilityChangeKind.WireSchemaAdded &&
            change.Severity == ProtocolCompatibilitySeverity.Compatible &&
            change.WireType == "Test.NewPayload");
    }

    [Fact]
    public void Policy_WireBreakingChange_RequiresBumpOnReferencingCatalog()
    {
        var baseline = BaselineOf(
            new[] { Catalog(revision: 4, Message(payloadType: "Test.LoginReq")) },
            new[] { Wire(Field(0, "userId")) });

        var sameRevision = new[]
        {
            Catalog(revision: 4, Message(payloadType: "Test.LoginReq"))
        };
        var changedWire = new[] { Wire(Field(0, "userId"), Field(1, "note")) };

        var unchanged = ProtocolCompatibilityCheck.Check(baseline, sameRevision, changedWire);
        Assert.False(unchanged.RevisionPolicy.IsSatisfied);
        Assert.Contains(unchanged.RevisionPolicy.Violations, violation =>
            violation.CatalogId == "test.room" &&
            violation.Rule == ProtocolRevisionPolicy.WireBreakingRequiresRevisionBump);

        var bumped = ProtocolCompatibilityCheck.Check(
            baseline, new[] { Catalog(revision: 5, Message(payloadType: "Test.LoginReq")) }, changedWire);
        Assert.False(bumped.Report.IsCompatible);
        Assert.True(bumped.RevisionPolicy.IsSatisfied);
    }

    [Fact]
    public void Policy_WireBreakingChange_WithoutReferencingCatalog_HasNoAttribution()
    {
        var baseline = BaselineOf(
            new[] { Catalog(revision: 1, Message()) },
            new[] { Wire(type: "Test.OrphanPayload", fields: new[] { Field(0, "userId") }) });
        var changedWire = new[]
        {
            Wire(type: "Test.OrphanPayload", fields: new[] { Field(0, "userId"), Field(1, "note") })
        };

        var result = ProtocolCompatibilityCheck.Check(
            baseline, new[] { Catalog(revision: 1, Message()) }, changedWire);

        Assert.False(result.Report.IsCompatible);
        Assert.True(result.RevisionPolicy.IsSatisfied);
    }

    [Fact]
    public void Policy_WireBreakingChange_AttributesByArrayElementType()
    {
        var baseline = BaselineOf(
            new[]
            {
                Catalog(revision: 1,
                    Message(id: "events.push", opCode: 200, direction: IrDirection.ServerToClient,
                        kind: IrPacketKind.Push, payloadType: "Test.LoginReq[]"))
            },
            new[] { Wire(Field(0, "userId")) });

        var result = ProtocolCompatibilityCheck.Check(
            baseline,
            new[]
            {
                Catalog(revision: 1,
                    Message(id: "events.push", opCode: 200, direction: IrDirection.ServerToClient,
                        kind: IrPacketKind.Push, payloadType: "Test.LoginReq[]"))
            },
            new[] { Wire(Field(0, "userId"), Field(1, "note")) });

        Assert.Contains(result.RevisionPolicy.Violations, violation =>
            violation.CatalogId == "test.room" &&
            violation.Rule == ProtocolRevisionPolicy.WireBreakingRequiresRevisionBump);
    }

    private static ProtocolCompatibilityBaselineDocument CaptureCurrentSources(out string repo)
    {
        repo = RepoRoot();
        var catalogRoot = Path.Combine(repo, CatalogInputRoot);
        var wireRoot = Path.Combine(repo, WireInputRoot);

        var catalogParser = new YamlProtocolSourceParser();
        var wireParser = new YamlWireSchemaParser();
        var catalogs = Directory
            .EnumerateFiles(catalogRoot, "*.protocol.yaml", SearchOption.AllDirectories)
            .OrderBy(path => path.Replace('\\', '/'), StringComparer.Ordinal)
            .Select(path => catalogParser.Parse(path, File.ReadAllText(path, Encoding.UTF8)))
            .ToArray();
        var wireSchemas = Directory
            .EnumerateFiles(wireRoot, "*.wire.yaml", SearchOption.AllDirectories)
            .OrderBy(path => path.Replace('\\', '/'), StringComparer.Ordinal)
            .Select(path => wireParser.Parse(path, File.ReadAllText(path, Encoding.UTF8)))
            .ToArray();

        return ProtocolCompatibilityBaseline.Capture(catalogs, wireSchemas);
    }

    private static ProtocolCompatibilityBaselineDocument BaselineOf(
        IReadOnlyList<ProtocolCatalogIr> catalogs,
        IReadOnlyList<WireSchemaIr> wireSchemas) =>
        ProtocolCompatibilityBaseline.Deserialize(
            ProtocolCompatibilityBaseline.Serialize(ProtocolCompatibilityBaseline.Capture(catalogs, wireSchemas)));

    private static ProtocolMessageIr Message(
        string id = "login.request",
        uint opCode = 100,
        IrDirection direction = IrDirection.ClientToServer,
        IrPacketKind kind = IrPacketKind.Request,
        string payloadType = "Test.LoginReq",
        string codec = "protobuf",
        IrReliability reliability = IrReliability.Reliable,
        string? response = null,
        int minimumSchemaVersion = 1,
        int maximumSchemaVersion = 1,
        int maximumPayloadBytes = 1048576) =>
        new(id, opCode, direction, kind, payloadType, codec, reliability, response,
            minimumSchemaVersion, maximumSchemaVersion, maximumPayloadBytes, captureSampleRate: 1d);

    private static ProtocolCatalogIr Catalog(int revision, params ProtocolMessageIr[] messages) =>
        new("test.room", "test", "room", revision, "protobuf", messages);

    private static ProtocolCatalogIr Catalog(
        int revision,
        string defaultCodec,
        params ProtocolMessageIr[] messages) =>
        new("test.room", "test", "room", revision, defaultCodec, messages);

    private static WireFieldIr Field(
        uint id,
        string name,
        string scalarType = "int32",
        bool isArray = false,
        bool optional = false,
        string? typeName = null) =>
        new(id, name, scalarType, isArray, optional, typeName);

    private static WireSchemaIr Wire(
        WireFieldIr first,
        WireFieldIr? second = null,
        IReadOnlyList<uint>? reservedIds = null) =>
        Wire(
            reservedIds: reservedIds,
            fields: second == null ? new[] { first } : new[] { first, second });

    private static WireSchemaIr Wire(
        string type = "Test.LoginReq",
        WireMemoryPackMode mode = WireMemoryPackMode.VersionTolerant,
        IReadOnlyList<uint>? reservedIds = null,
        params WireFieldIr[] fields) =>
        CreateWire(type, mode, reservedIds, fields);

    private static WireSchemaIr CreateWire(
        string type,
        WireMemoryPackMode mode,
        IReadOnlyList<uint>? reservedIds,
        IReadOnlyList<WireFieldIr> fields)
    {
        var separator = type.LastIndexOf('.');
        var targetNamespace = separator < 0 ? "Test" : type[..separator];
        var typeName = separator < 0 ? type : type[(separator + 1)..];
        return new WireSchemaIr(
            schemaVersion: 1,
            type: typeName,
            fields: fields,
            reservedIds: reservedIds ?? Array.Empty<uint>(),
            projectId: "test",
            targetNamespace: targetNamespace,
            memoryPackMode: mode);
    }

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
